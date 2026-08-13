using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.PostgreSql;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests;

public sealed class PostgreSqlBackupCapabilityIntegrationTests : IDisposable
{
    const string ConnectionVariable = "IFM_GATE6_POSTGRES_CONNECTION";
    readonly string _root = Path.Combine(Path.GetTempPath(), "ifm-gate6", Guid.NewGuid().ToString("N"));

    public PostgreSqlBackupCapabilityIntegrationTests()
        => Environment.SetEnvironmentVariable(ConnectionVariable,
            "Host=127.0.0.1;Port=5432;Database=postgres;Username=gate6;Password=gate6-test-secret;SSL Mode=Disable");

    [Fact]
    [Trait("Category", "Gate6Integration")]
    public async Task Backup_restart_native_verification_and_fresh_target_restore_preserve_synthetic_data()
    {
        var options = Options();
        var native = new DeterministicPostgreSqlNativeRunner();
        var validator = new SyntheticPostgreSqlTargetValidator("synthetic-gate6-row");
        var backupOperation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var firstHost = new PostgreSqlBackupCapability(options, native, validator);

        await firstHost.ValidateAsync(CancellationToken.None);
        var captured = await firstHost.CreateBaseBackupAsync(
            new PostgreSqlBackupRequest(backupOperation, new DatabaseProtectionSetId("core-postgresql")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);

        var restartedHost = new PostgreSqlBackupCapability(options, native, validator);
        await restartedHost.ValidateAsync(CancellationToken.None);
        var recovered = await restartedHost.CreateBaseBackupAsync(
            new PostgreSqlBackupRequest(backupOperation, new DatabaseProtectionSetId("core-postgresql")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);
        var verified = await restartedHost.VerifyAsync(
            new PostgreSqlVerificationRequest(backupOperation, recovered.SafeBoundaryReference), CancellationToken.None);
        var restoreOperation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var restored = await restartedHost.RestoreToFreshTargetAsync(
            new PostgreSqlRestoreRequest(
                restoreOperation,
                new DatabaseRestorePointId(backupOperation.Format()),
                new DatabaseFreshTargetDescriptor("disposable-validation", "gate6")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);
        var replay = await restartedHost.RestoreToFreshTargetAsync(
            new PostgreSqlRestoreRequest(
                restoreOperation,
                new DatabaseRestorePointId(backupOperation.Format()),
                new DatabaseFreshTargetDescriptor("disposable-validation", "gate6")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);

        captured.SafeBoundaryReference.Should().Be(recovered.SafeBoundaryReference);
        captured.WalContinuity.Should().NotBeNull();
        captured.WalContinuity!.RequiredWalPresent.Should().BeTrue();
        captured.WalContinuity.StartLsn.Should().Be("0/1000028");
        captured.WalContinuity.EndLsn.Should().Be("0/2000000");
        captured.Statistics!.SourceBytes.Should().BeGreaterThan(0);
        verified.Succeeded.Should().BeTrue();
        verified.Level.Should().Be(DatabaseVerificationLevel.Native);
        restored.Succeeded.Should().BeTrue();
        restored.SourceSystemIdentifier.Should().Be(restored.RestoredSystemIdentifier);
        restored.ValidationRevision.Should().Be(150_010);
        replay.Should().Be(restored);
        native.Invocations.Count(static value => value.Tool == PostgreSqlNativeTool.BaseBackup
            && value.Arguments.Contains("--pgdata")).Should().Be(1);
        native.Invocations.Count(static value => value.Tool == PostgreSqlNativeTool.Control && value.Arguments[0] == "start").Should().Be(1);
        native.Invocations.SelectMany(static value => value.Arguments).Should().NotContain("gate6-test-secret");
        native.Invocations.Single(static value => value.Tool == PostgreSqlNativeTool.BaseBackup
            && value.Arguments.Contains("--pgdata"))
            .Environment["PGPASSWORD"].Should().Be("gate6-test-secret");
        File.ReadAllText(Path.Combine(
            options.ResolveRestoreRoot(), "disposable-validation", "gate6", restoreOperation.Format(),
            "data", "base", "synthetic-row.txt")).Should().Be("synthetic-gate6-row");
    }

    [Fact]
    [Trait("Category", "Gate6Integration")]
    public async Task Native_verification_rejects_a_tampered_base_backup_before_publication()
    {
        var options = Options();
        var native = new DeterministicPostgreSqlNativeRunner();
        var capability = new PostgreSqlBackupCapability(options, native, new SyntheticPostgreSqlTargetValidator("synthetic-gate6-row"));
        var operation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var boundary = await capability.CreateBaseBackupAsync(
            new PostgreSqlBackupRequest(operation, new DatabaseProtectionSetId("core-postgresql")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);
        var dataFile = Path.Combine(options.ResolveBackupRoot(), operation.Format() + ".inprogress", "data", "base", "synthetic-row.txt");
        await File.AppendAllTextAsync(dataFile, "tampered");

        Func<Task> verify = async () => await capability.VerifyAsync(
            new PostgreSqlVerificationRequest(operation, boundary.SafeBoundaryReference), CancellationToken.None);

        await verify.Should().ThrowAsync<PostgreSqlNativeOperationException>();
        Directory.Exists(Path.Combine(options.ResolveBackupRoot(), operation.Format())).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Gate6Integration")]
    public async Task Non_allowlisted_protection_sets_and_fresh_targets_are_rejected_before_native_execution()
    {
        var options = Options();
        var native = new DeterministicPostgreSqlNativeRunner();
        var capability = new PostgreSqlBackupCapability(options, native, new SyntheticPostgreSqlTargetValidator("synthetic-gate6-row"));
        var operation = new DatabaseRecoveryOperationId(Guid.NewGuid());

        Func<Task> backup = async () => await capability.CreateBaseBackupAsync(
            new PostgreSqlBackupRequest(operation, new DatabaseProtectionSetId("production-unapproved")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);
        Func<Task> restore = async () => await capability.RestoreToFreshTargetAsync(
            new PostgreSqlRestoreRequest(operation, new DatabaseRestorePointId("missing"),
                new DatabaseFreshTargetDescriptor("disposable-validation", "not-allowed")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);

        await backup.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not allowlisted*");
        await restore.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not allowlisted*");
        native.Invocations.Should().BeEmpty();
    }

    PostgreSqlBackupOptions Options() => new()
    {
        ToolDirectory = Path.Combine(_root, "tools"),
        BackupRoot = Path.Combine(_root, "backup"),
        RestoreRoot = Path.Combine(_root, "restore"),
        ConnectionStringEnvironmentVariable = ConnectionVariable,
        AllowedProtectionSets = ["core-postgresql"],
        FreshTargetProfiles = new Dictionary<string, PostgreSqlFreshTargetProfileOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["disposable-validation"] = new()
            {
                Host = "127.0.0.1",
                Port = 55432,
                Database = "postgres",
                AllowedLogicalTargets = ["gate6"],
                StartupTimeout = TimeSpan.FromSeconds(5)
            }
        },
        MinimumMajorVersion = 15,
        MaximumMajorVersion = 15,
        ProcessTimeout = TimeSpan.FromMinutes(1),
        RequirePersistentBackupRoot = false
    };

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(ConnectionVariable, null);
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    sealed class SyntheticPostgreSqlTargetValidator(string expectedValue) : IPostgreSqlFreshTargetValidator
    {
        public ValueTask<PostgreSqlFreshTargetValidation> ValidateAsync(
            PostgreSqlFreshTargetProfileOptions profile,
            IReadOnlyDictionary<string, string?> sourceEnvironment,
            string expectedSystemIdentifier,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new PostgreSqlFreshTargetValidation(expectedSystemIdentifier, 150_010));
        }
    }

    sealed class DeterministicPostgreSqlNativeRunner : IPostgreSqlNativeProcessRunner
    {
        public List<PostgreSqlNativeInvocation> Invocations { get; } = [];

        public async ValueTask<PostgreSqlNativeResult> RunAsync(
            PostgreSqlNativeInvocation invocation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Invocations.Add(invocation);
            if (invocation.Arguments.SequenceEqual(["--version"]))
                return new PostgreSqlNativeResult(0, "native (PostgreSQL) 15.10", "", TimeSpan.FromMilliseconds(1));
            switch (invocation.Tool)
            {
                case PostgreSqlNativeTool.BaseBackup:
                    await CreateBackupAsync(ValueAfter(invocation.Arguments, "--pgdata"), cancellationToken);
                    break;
                case PostgreSqlNativeTool.VerifyBackup:
                    await VerifyAsync(invocation.Arguments[^1], cancellationToken);
                    break;
                case PostgreSqlNativeTool.Control:
                case PostgreSqlNativeTool.ControlData:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return new PostgreSqlNativeResult(0, "", "", TimeSpan.FromMilliseconds(20));
        }

        static async Task CreateBackupAsync(string data, CancellationToken cancellationToken)
        {
            var baseDirectory = Directory.CreateDirectory(Path.Combine(data, "base")).FullName;
            var walDirectory = Directory.CreateDirectory(Path.Combine(data, "pg_wal")).FullName;
            var file = Path.Combine(baseDirectory, "synthetic-row.txt");
            await File.WriteAllTextAsync(file, "synthetic-gate6-row", cancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(walDirectory, "000000010000000000000001"), new byte[128], cancellationToken);
            var content = await File.ReadAllBytesAsync(file, cancellationToken);
            var manifest = new
            {
                PostgreSQL_Backup_Manifest_Version = 2,
                System_Identifier = "7543210987654321000",
                Files = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["Path"] = "base/synthetic-row.txt",
                        ["Size"] = content.LongLength,
                        ["Last-Modified"] = DateTimeOffset.UtcNow.ToString("O"),
                        ["Checksum-Algorithm"] = "SHA256",
                        ["Checksum"] = Convert.ToHexString(SHA256.HashData(content))
                    }
                },
                WAL_Ranges = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["Timeline"] = 1,
                        ["Start-LSN"] = "0/1000028",
                        ["End-LSN"] = "0/2000000"
                    }
                }
            };
            var json = JsonSerializer.Serialize(manifest)
                .Replace("PostgreSQL_Backup_Manifest_Version", "PostgreSQL-Backup-Manifest-Version", StringComparison.Ordinal)
                .Replace("System_Identifier", "System-Identifier", StringComparison.Ordinal)
                .Replace("WAL_Ranges", "WAL-Ranges", StringComparison.Ordinal);
            await File.WriteAllTextAsync(Path.Combine(data, "backup_manifest"), json, cancellationToken);
        }

        static async Task VerifyAsync(string data, CancellationToken cancellationToken)
        {
            using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(data, "backup_manifest"), cancellationToken));
            foreach (var item in manifest.RootElement.GetProperty("Files").EnumerateArray())
            {
                var file = Path.Combine(data, item.GetProperty("Path").GetString()!.Replace('/', Path.DirectorySeparatorChar));
                var bytes = await File.ReadAllBytesAsync(file, cancellationToken);
                var checksum = Convert.ToHexString(SHA256.HashData(bytes));
                if (!string.Equals(checksum, item.GetProperty("Checksum").GetString(), StringComparison.Ordinal))
                    throw new PostgreSqlNativeOperationException(PostgreSqlNativeTool.VerifyBackup, 1);
            }
        }

        static string ValueAfter(IReadOnlyList<string> values, string name)
        {
            var index = values.IndexOf(name);
            return values[index + 1];
        }
    }
}

file static class PostgreSqlTestListExtensions
{
    public static int IndexOf<T>(this IReadOnlyList<T> values, T expected)
    {
        for (var index = 0; index < values.Count; index++)
            if (EqualityComparer<T>.Default.Equals(values[index], expected)) return index;
        return -1;
    }
}
