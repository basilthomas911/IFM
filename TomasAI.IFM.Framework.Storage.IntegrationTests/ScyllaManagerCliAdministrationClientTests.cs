using FluentAssertions;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Scylla;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests;

public sealed class ScyllaManagerCliAdministrationClientTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "ifm-gate7-manager", Guid.NewGuid().ToString("N"));

    [Fact]
    [Trait("Category", "Gate7Integration")]
    public async Task Manager_adapter_uses_allowlisted_typed_commands_and_captures_cluster_evidence()
    {
        var options = Options();
        var runner = new DeterministicManagerRunner();
        var client = new ScyllaManagerCliAdministrationClient(options, runner);
        var native = Directory.CreateDirectory(Path.Combine(_root, "native")).FullName;
        var operation = new DatabaseRecoveryOperationId(Guid.NewGuid());

        await client.ValidateAsync(CancellationToken.None);
        var capture = await client.CaptureAsync(
            operation, options.ProtectionSets["read-model-scylla"], native,
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);
        var verification = await client.VerifyAsync(
            options.ProtectionSets["read-model-scylla"], capture, native, CancellationToken.None);
        var restore = await client.RestoreAsync(
            new DatabaseRecoveryOperationId(Guid.NewGuid()),
            options.ProtectionSets["read-model-scylla"],
            options.FreshTargetProfiles["disposable-validation"],
            capture, native, Directory.CreateDirectory(Path.Combine(_root, "restore")).FullName,
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);

        capture.SnapshotTag.Should().Be("sm_20260812010203UTC");
        capture.ManagerTaskReference.Should().StartWith("backup/");
        capture.Topology.LiveNodeCount.Should().Be(3);
        capture.Topology.SchemaAgreement.Should().BeTrue();
        capture.ScyllaVersion.Should().Be("2025.1.4");
        capture.ManagerVersion.Should().Contain("3.11.2");
        capture.ArtifactReferences.Should().HaveCount(2);
        capture.KeyspaceCount.Should().Be(1);
        capture.TableCount.Should().Be(1);
        verification.Succeeded.Should().BeTrue();
        restore.Succeeded.Should().BeTrue();
        runner.Invocations.Should().Contain(static value =>
            value.Operation == ScyllaManagerOperation.Backup
            && value.Arguments.Contains("--num-retries=0")
            && value.Arguments.Contains("localstorage:gate7-backups"));
        runner.Invocations.Should().Contain(static value =>
            value.Operation == ScyllaManagerOperation.RestoreSchema
            && value.Arguments.Contains("--restore-schema"));
        runner.Invocations.Should().Contain(static value =>
            value.Operation == ScyllaManagerOperation.RestoreTables
            && value.Arguments.Contains("--restore-tables"));
        runner.Invocations.SelectMany(static value => value.Arguments)
            .Should().NotContain(static argument => argument.Contains(';') || argument.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    ScyllaBackupOptions Options() => new()
    {
        ToolDirectory = Path.Combine(_root, "tools"),
        BackupRoot = Path.Combine(_root, "backup"),
        RestoreRoot = Path.Combine(_root, "restore-root"),
        ManagerApiUrl = "http://127.0.0.1:5080/api/v1",
        ProtectionSets = new Dictionary<string, ScyllaProtectionSetOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["read-model-scylla"] = new()
            {
                ManagerCluster = "gate7-source",
                BackupLocation = "localstorage:gate7-backups",
                Keyspaces = ["gate7_keyspace"],
                RequiredLiveNodes = 3
            }
        },
        FreshTargetProfiles = new Dictionary<string, ScyllaFreshTargetProfileOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["disposable-validation"] = new()
            {
                ManagerCluster = "gate7-target",
                AllowedLogicalTargets = ["gate7"],
                RequiredLiveNodes = 1
            }
        },
        OperationTimeout = TimeSpan.FromMinutes(1),
        PollInterval = TimeSpan.FromMilliseconds(1),
        RequirePersistentBackupRoot = false
    };

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    sealed class DeterministicManagerRunner : IScyllaManagerProcessRunner
    {
        public List<ScyllaManagerInvocation> Invocations { get; } = [];

        public ValueTask<ScyllaManagerProcessResult> RunAsync(
            ScyllaManagerInvocation invocation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Invocations.Add(invocation);
            var output = invocation.Operation switch
            {
                ScyllaManagerOperation.Version => "sctool version 3.11.2",
                ScyllaManagerOperation.Status => Status,
                ScyllaManagerOperation.Backup => "backup/11111111-1111-1111-1111-111111111111",
                ScyllaManagerOperation.Progress => "Status: DONE\nProgress: 100%",
                ScyllaManagerOperation.BackupList => "sm_20260812010203UTC",
                ScyllaManagerOperation.BackupFiles => Artifacts,
                ScyllaManagerOperation.RestoreSchema => "restore/22222222-2222-2222-2222-222222222222",
                ScyllaManagerOperation.RestoreTables => "restore/33333333-3333-3333-3333-333333333333",
                _ => throw new ArgumentOutOfRangeException()
            };
            return ValueTask.FromResult(new ScyllaManagerProcessResult(output, string.Empty, TimeSpan.FromMilliseconds(10)));
        }

        const string Status = """
            |    | CQL | REST | Address | Scylla | Host ID                              |
            | UN | UP  | UP   | 10.0.0.1| 2025.1.4 | 11111111-1111-1111-1111-111111111111 |
            | UN | UP  | UP   | 10.0.0.2| 2025.1.4 | 22222222-2222-2222-2222-222222222222 |
            | UN | UP  | UP   | 10.0.0.3| 2025.1.4 | 33333333-3333-3333-3333-333333333333 |
            """;

        const string Artifacts = """
            localstorage/gate7/schema.cql|gate7_keyspace|probe
            localstorage/gate7/me-1-big-Data.db|gate7_keyspace|probe
            """;
    }
}
