using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Npgsql;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.PostgreSql;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests;

public sealed class PostgreSqlNativeDockerIntegrationTests : IAsyncLifetime
{
    const string ConnectionVariable = "IFM_GATE6_DOCKER_POSTGRES_CONNECTION";
    const string Password = "gate6-disposable-password";
    readonly string _id = Guid.NewGuid().ToString("N")[..12];
    readonly string _root = Path.Combine(Path.GetTempPath(), "ifm-gate6-native", Guid.NewGuid().ToString("N"));
    string SourceContainer => $"ifm-gate6-source-{_id}";
    string TargetContainer => $"ifm-gate6-target-{_id}";
    int _sourcePort;
    int _targetPort;

    public async Task InitializeAsync()
    {
        _sourcePort = FreePort();
        _targetPort = FreePort();
        await DockerAsync(["run", "--detach", "--name", SourceContainer,
            "--publish", $"127.0.0.1:{_sourcePort}:5432",
            "--env", $"POSTGRES_PASSWORD={Password}", "postgres:latest"]);
        var connectionString = $"Host=127.0.0.1;Port={_sourcePort};Database=postgres;Username=postgres;Password={Password};SSL Mode=Disable;Pooling=false";
        Environment.SetEnvironmentVariable(ConnectionVariable, connectionString);
        await WaitForPostgreSqlAsync(connectionString);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE gate6_restore_probe(id integer primary key, payload text not null); INSERT INTO gate6_restore_probe VALUES (1, 'native-restore-ok'); CHECKPOINT;";
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    [Trait("Category", "Gate6NativeIntegration")]
    public async Task Physical_base_backup_is_verified_and_boots_as_an_isolated_fresh_target()
    {
        var options = Options();
        var runner = new DockerPostgreSqlNativeRunner(SourceContainer, TargetContainer, Password, _targetPort);
        var validator = new LiveSyntheticTargetValidator();
        var capability = new PostgreSqlBackupCapability(options, runner, validator);
        var backupOperation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        await capability.ValidateAsync(CancellationToken.None);

        var boundary = await capability.CreateBaseBackupAsync(
            new PostgreSqlBackupRequest(backupOperation, new DatabaseProtectionSetId("core-postgresql")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);
        var restartedCapability = new PostgreSqlBackupCapability(options, runner, validator);
        await restartedCapability.ValidateAsync(CancellationToken.None);
        var recoveredBoundary = await restartedCapability.CreateBaseBackupAsync(
            new PostgreSqlBackupRequest(backupOperation, new DatabaseProtectionSetId("core-postgresql")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);
        var verification = await restartedCapability.VerifyAsync(
            new PostgreSqlVerificationRequest(backupOperation, recoveredBoundary.SafeBoundaryReference), CancellationToken.None);
        var restore = await restartedCapability.RestoreToFreshTargetAsync(
            new PostgreSqlRestoreRequest(
                new DatabaseRecoveryOperationId(Guid.NewGuid()),
                new DatabaseRestorePointId(backupOperation.Format()),
                new DatabaseFreshTargetDescriptor("docker-validation", "gate6-native")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);

        recoveredBoundary.SafeBoundaryReference.Should().Be(boundary.SafeBoundaryReference);
        recoveredBoundary.WalContinuity.Should().Be(boundary.WalContinuity);
        boundary.NativeMajorVersion.Should().Be(17);
        boundary.WalContinuity!.RequiredWalPresent.Should().BeTrue();
        boundary.WalContinuity.RequiredSegmentCount.Should().BeGreaterThan(0);
        verification.Succeeded.Should().BeTrue();
        restore.Succeeded.Should().BeTrue();
        restore.SourceSystemIdentifier.Should().Be(restore.RestoredSystemIdentifier);
        validator.SyntheticValue.Should().Be("native-restore-ok");
        runner.BaseBackupExecutionCount.Should().Be(1);
    }

    PostgreSqlBackupOptions Options() => new()
    {
        ToolDirectory = Path.Combine(_root, "container-tools"),
        BackupRoot = Path.Combine(_root, "backup"),
        RestoreRoot = Path.Combine(_root, "restore"),
        ConnectionStringEnvironmentVariable = ConnectionVariable,
        AllowedProtectionSets = ["core-postgresql"],
        FreshTargetProfiles = new Dictionary<string, PostgreSqlFreshTargetProfileOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["docker-validation"] = new()
            {
                Host = "127.0.0.1",
                Port = _targetPort,
                Database = "postgres",
                AllowedLogicalTargets = ["gate6-native"],
                StartupTimeout = TimeSpan.FromMinutes(1)
            }
        },
        MinimumMajorVersion = 17,
        MaximumMajorVersion = 17,
        ProcessTimeout = TimeSpan.FromMinutes(5),
        RequirePersistentBackupRoot = false
    };

    public async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable(ConnectionVariable, null);
        await RemoveContainerAsync(TargetContainer);
        await RemoveContainerAsync(SourceContainer);
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    static async Task WaitForPostgreSqlAsync(string connectionString)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(1);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                return;
            }
            catch (NpgsqlException)
            {
                await Task.Delay(250);
            }
        }
        throw new TimeoutException("The disposable PostgreSQL source did not become ready.");
    }

    static async Task RemoveContainerAsync(string name)
    {
        try { await DockerAsync(["rm", "--force", name], allowFailure: true); }
        catch { }
    }

    static async Task<string> DockerAsync(IReadOnlyList<string> arguments, bool allowFailure = false)
    {
        var start = new ProcessStartInfo
        {
            FileName = "docker",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Docker could not be started.");
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (!allowFailure && process.ExitCode != 0)
            throw new InvalidOperationException($"The disposable Docker PostgreSQL operation failed with exit code {process.ExitCode}: {error}");
        return output.Trim();
    }

    sealed class LiveSyntheticTargetValidator : IPostgreSqlFreshTargetValidator
    {
        readonly PostgreSqlFreshTargetValidator _native = new();
        public string SyntheticValue { get; private set; } = string.Empty;

        public async ValueTask<PostgreSqlFreshTargetValidation> ValidateAsync(
            PostgreSqlFreshTargetProfileOptions profile,
            IReadOnlyDictionary<string, string?> sourceEnvironment,
            string expectedSystemIdentifier,
            CancellationToken cancellationToken)
        {
            var validation = await _native.ValidateAsync(
                profile, sourceEnvironment, expectedSystemIdentifier, cancellationToken);
            var connectionString = $"Host={profile.Host};Port={profile.Port};Database=postgres;Username=postgres;Password={Password};SSL Mode=Disable;Pooling=false";
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT payload FROM gate6_restore_probe WHERE id = 1";
            SyntheticValue = (string)(await command.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("The synthetic restored row is missing."));
            return validation;
        }
    }

    sealed class DockerPostgreSqlNativeRunner(
        string sourceContainer,
        string targetContainer,
        string password,
        int targetPort) : IPostgreSqlNativeProcessRunner
    {
        public int BaseBackupExecutionCount { get; private set; }

        public async ValueTask<PostgreSqlNativeResult> RunAsync(
            PostgreSqlNativeInvocation invocation,
            CancellationToken cancellationToken)
        {
            var started = Stopwatch.GetTimestamp();
            string output;
            if (invocation.Arguments.SequenceEqual(["--version"]))
            {
                output = await DockerAsync(["run", "--rm", "postgres:latest", Tool(invocation.Tool), "--version"]);
            }
            else if (invocation.Tool == PostgreSqlNativeTool.BaseBackup)
            {
                BaseBackupExecutionCount++;
                var data = ValueAfter(invocation.Arguments, "--pgdata");
                var translated = invocation.Arguments.Select(value => value == data ? "/backup" : value).ToArray();
                output = await DockerAsync([
                    "run", "--rm", "--network", $"container:{sourceContainer}",
                    "--mount", $"type=bind,source={Path.GetFullPath(data)},target=/backup",
                    "--env", "PGHOST=127.0.0.1", "--env", "PGPORT=5432",
                    "--env", "PGUSER=postgres", "--env", $"PGPASSWORD={password}", "--env", "PGDATABASE=postgres",
                    "postgres:latest", "pg_basebackup", .. translated]);
            }
            else if (invocation.Tool == PostgreSqlNativeTool.VerifyBackup)
            {
                var data = invocation.Arguments[^1];
                var translated = invocation.Arguments.Take(invocation.Arguments.Count - 1).Append("/backup").ToArray();
                output = await DockerAsync(["run", "--rm",
                    "--mount", $"type=bind,source={Path.GetFullPath(data)},target=/backup,readonly",
                    "postgres:latest", "pg_verifybackup", .. translated]);
            }
            else if (invocation.Tool == PostgreSqlNativeTool.Control && invocation.Arguments[0] == "start")
            {
                var data = ValueAfter(invocation.Arguments, "--pgdata");
                output = await DockerAsync(["run", "--detach", "--name", targetContainer,
                    "--publish", $"127.0.0.1:{targetPort}:5432",
                    "--mount", $"type=bind,source={Path.GetFullPath(data)},target=/var/lib/postgresql/data",
                    "--env", $"POSTGRES_PASSWORD={password}", "postgres:latest"]);
            }
            else
            {
                await DockerAsync(["stop", targetContainer]);
                output = await DockerAsync(["rm", targetContainer]);
            }
            return new PostgreSqlNativeResult(0, output, "", Stopwatch.GetElapsedTime(started));
        }

        static string Tool(PostgreSqlNativeTool tool) => tool switch
        {
            PostgreSqlNativeTool.BaseBackup => "pg_basebackup",
            PostgreSqlNativeTool.VerifyBackup => "pg_verifybackup",
            PostgreSqlNativeTool.Control => "pg_ctl",
            _ => throw new ArgumentOutOfRangeException(nameof(tool))
        };

        static string ValueAfter(IReadOnlyList<string> values, string expected)
        {
            for (var index = 0; index < values.Count - 1; index++)
                if (string.Equals(values[index], expected, StringComparison.Ordinal)) return values[index + 1];
            throw new InvalidOperationException($"Expected native argument '{expected}' was not present.");
        }
    }
}
