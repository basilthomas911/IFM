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
        var preparation = new RecordingFreshTarget(runner);
        var client = new ScyllaManagerCliAdministrationClient(options, runner, preparation);
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
        runner.Invocations.Should().Contain(value => value.Operation == ScyllaManagerOperation.BackupProgress && value.Arguments.Contains(capture.ManagerTaskReference));
        runner.Invocations.Should().NotContain(value => value.Operation == ScyllaManagerOperation.BackupList);
        capture.ManagerTaskReference.Should().StartWith("backup/");
        capture.Topology.LiveNodeCount.Should().Be(3);
        capture.Topology.SchemaAgreement.Should().BeTrue();
        capture.ScyllaVersion.Should().Be("2025.1.4");
        capture.ManagerVersion.Should().Contain("3.11.2");
        capture.ArtifactReferences.Should().HaveCount(3);
        capture.KeyspaceCount.Should().Be(1);
        capture.TableCount.Should().Be(1);
        verification.Succeeded.Should().BeTrue();
        restore.Succeeded.Should().BeTrue();
        preparation.Prepared.Should().BeTrue();
        runner.Invocations.Should().Contain(static value =>
            value.Operation == ScyllaManagerOperation.Backup
            && value.Arguments.Contains("--num-retries=0")
            && value.Arguments.Contains("s3:gate7-backups"));
        runner.Invocations.Should().Contain(static value =>
            value.Operation == ScyllaManagerOperation.RestoreSchema
            && value.Arguments.Contains("--restore-schema")
            && !value.Arguments.Contains("--keyspace"));
        runner.Invocations.Should().Contain(static value =>
            value.Operation == ScyllaManagerOperation.RestoreTables
            && value.Arguments.Contains("--restore-tables")
            && value.Arguments.Contains("--keyspace"));
        runner.Invocations.Should().Contain(static value =>
            value.Operation == ScyllaManagerOperation.Tasks
            && value.Arguments.SequenceEqual(new[] { "tasks", "--cluster", "gate7-source" }));
        runner.Invocations.SelectMany(static value => value.Arguments)
            .Should().NotContain(static argument => argument.Contains(';') || argument.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "Gate11")]
    public async Task Manager_adapter_rejects_a_completed_snapshot_missing_a_live_node()
    {
        var options = Options();
        var client = new ScyllaManagerCliAdministrationClient(options, new DeterministicManagerRunner(omitThirdNode: true));
        var native = Directory.CreateDirectory(Path.Combine(_root, "incomplete-native")).FullName;

        await client.ValidateAsync(CancellationToken.None);
        var action = async () => await client.CaptureAsync(
            new DatabaseRecoveryOperationId(Guid.NewGuid()), options.ProtectionSets["read-model-scylla"], native,
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidDataException>().WithMessage("*does not cover every live topology node*");
    }

    [Fact]
    public async Task Native_two_column_manifest_preserves_every_artifact_and_counts_real_tables()
    {
        var options = Options();
        string[] nodes = ["11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222", "33333333-3333-3333-3333-333333333333"];
        var lines = Enumerable.Range(0, 6500).Select(i =>
            $"s3://gate7-backups/backup/sst/cluster/test/dc/dc1/node/{nodes[i % 3]}/keyspace/gate7_keyspace/table/probe/me-{i}-Data.db | gate7_keyspace/probe-1234").ToArray();
        var manifest = string.Join('\n', lines);
        manifest.Length.Should().BeGreaterThan(64 * 1024);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(manifest));
        using var reader = new StreamReader(stream);
        var captured = await ScyllaManagerProcessRunner.ReadBoundedAsync(reader,
            ScyllaManagerProcessRunner.MaximumManifestCharacters, CancellationToken.None);
        var client = new ScyllaManagerCliAdministrationClient(options,
            new DeterministicManagerRunner(manifestOverride: captured));
        var capture = await client.CaptureAsync(new DatabaseRecoveryOperationId(Guid.NewGuid()),
            options.ProtectionSets["read-model-scylla"], Path.Combine(_root, "native-two-column"),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);
        capture.ArtifactReferences.Should().HaveCount(6500);
        capture.KeyspaceCount.Should().Be(1);
        capture.TableCount.Should().Be(1);
        capture.Topology.LiveNodeCount.Should().Be(3);
    }

    [Fact]
    public async Task Oversized_manager_output_is_rejected_instead_of_silently_used_as_partial_evidence()
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(new string('x', 65537)));
        using var reader = new StreamReader(stream);
        var action = async () => await ScyllaManagerProcessRunner.ReadBoundedAsync(reader, 65536, CancellationToken.None);
        await action.Should().ThrowAsync<InvalidDataException>().WithMessage("*incomplete output*");
        (await reader.ReadAsync(new char[1].AsMemory(), CancellationToken.None)).Should().Be(0);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Restore_manifest_accepts_bucket_relocation_but_rejects_changed_object_keys(bool changeObjectKey)
    {
        var options = Options();
        var runner = new DeterministicManagerRunner();
        var client = new ScyllaManagerCliAdministrationClient(options, runner);
        var native = Path.Combine(_root, "relocated-native");
        var capture = await client.CaptureAsync(new DatabaseRecoveryOperationId(Guid.NewGuid()),
            options.ProtectionSets["read-model-scylla"], native, new Progress<DatabaseNativeProgress>(), CancellationToken.None);
        runner.ManifestBucket = "gate7-restore";
        runner.ChangeObjectKey = changeObjectKey;
        var relocated = new ScyllaProtectionSetOptions
        {
            ManagerCluster = "gate7-source", BackupLocation = "s3:gate7-restore",
            Keyspaces = ["gate7_keyspace"], RequiredLiveNodes = 3
        };
        var result = await client.VerifyAsync(relocated, capture, native, CancellationToken.None);
        result.Succeeded.Should().Be(!changeObjectKey);
        if (!changeObjectKey) result.NativeManifestSha256.Should().Be(capture.NativeManifestSha256);
        capture.ArtifactReferences.Should().OnlyContain(line => line.StartsWith("s3://gate7-backups/"));
    }

    [Fact]
    public void Manifest_relocation_rejects_an_object_from_an_unexpected_bucket()
    {
        var action = () => ScyllaManagerCliAdministrationClient.RebaseManifestArtifacts(
            ["s3://other/backup/data.db | keyspace/table"], "s3:expected", "original");
        action.Should().Throw<InvalidDataException>().WithMessage("*unexpected bucket*");
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
                BackupLocation = "s3:gate7-backups",
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

    sealed class RecordingFreshTarget(DeterministicManagerRunner runner) : IScyllaFreshTargetDataPreparation
    {
        public bool Prepared { get; private set; }
        public void Validate(ScyllaProtectionSetOptions source, ScyllaFreshTargetProfileOptions target) { }
        public ValueTask PrepareAsync(ScyllaProtectionSetOptions source, ScyllaFreshTargetProfileOptions target, string workspace, CancellationToken token)
        {
            runner.Invocations.Should().NotContain(value => value.Operation == ScyllaManagerOperation.RestoreSchema);
            runner.Invocations.Should().NotContain(value => value.Operation == ScyllaManagerOperation.RestoreTables);
            Prepared = true;
            return ValueTask.CompletedTask;
        }
    }

    [Theory]
    [InlineData("172.30.20.10")]
    [InlineData("::ffff:172.30.20.10")]
    public void Fresh_target_preparation_rejects_source_endpoint_including_mapped_addresses(string targetAddress)
    {
        var source = new ScyllaProtectionSetOptions { ManagerCluster = "source", CqlContactPoints = ["172.30.20.10"] };
        var target = new ScyllaFreshTargetProfileOptions { ManagerCluster = "fresh", CqlContactPoints = [targetAddress], DiscardExistingApplicationData = true };
        var action = () => ScyllaFreshTargetDataPreparation.ValidateTarget(source, target);
        action.Should().Throw<InvalidOperationException>().WithMessage("*overlaps*");
    }

    [Fact]
    public void Fresh_target_preparation_requires_explicit_discard_permission()
    {
        var source = new ScyllaProtectionSetOptions { ManagerCluster = "source", CqlContactPoints = ["172.30.20.10"] };
        var target = new ScyllaFreshTargetProfileOptions { ManagerCluster = "fresh", CqlContactPoints = ["172.30.20.11"] };
        var action = () => ScyllaFreshTargetDataPreparation.ValidateTarget(source, target);
        action.Should().Throw<InvalidOperationException>().WithMessage("*explicit disposable-target*");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    sealed class DeterministicManagerRunner(bool omitThirdNode = false, string? manifestOverride = null) : IScyllaManagerProcessRunner
    {
        public List<ScyllaManagerInvocation> Invocations { get; } = [];
        public string ManifestBucket { get; set; } = "gate7-backups";
        public bool ChangeObjectKey { get; set; }

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
                ScyllaManagerOperation.Tasks => Tasks,
                ScyllaManagerOperation.BackupList => "sm_20250812010203UTC",
                ScyllaManagerOperation.BackupProgress => "Snapshot Tag: sm_20260812010203UTC",
                ScyllaManagerOperation.BackupFiles => (manifestOverride ?? (omitThirdNode ? IncompleteArtifacts : Artifacts))
                    .Replace("s3://gate7-backups/", "s3://" + ManifestBucket + "/", StringComparison.Ordinal)
                    .Replace("me-1-big-Data.db", ChangeObjectKey ? "changed-Data.db" : "me-1-big-Data.db", StringComparison.Ordinal),
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
            s3://gate7-backups/backup/schema/node/11111111-1111-1111-1111-111111111111/schema.cql|gate7_keyspace|probe
            s3://gate7-backups/backup/sst/node/22222222-2222-2222-2222-222222222222/me-1-big-Data.db|gate7_keyspace|probe
            s3://gate7-backups/backup/sst/node/33333333-3333-3333-3333-333333333333/me-2-big-Data.db|gate7_keyspace|probe
            """;

        const string IncompleteArtifacts = """
            s3://gate7-backups/backup/schema/node/11111111-1111-1111-1111-111111111111/schema.cql|gate7_keyspace|probe
            s3://gate7-backups/backup/sst/node/22222222-2222-2222-2222-222222222222/me-1-big-Data.db|gate7_keyspace|probe
            """;

        const string Tasks = """
            | Task                                         | Status | Next |
            | backup/11111111-1111-1111-1111-111111111111 | DONE   |      |
            | restore/22222222-2222-2222-2222-222222222222| DONE   |      |
            | restore/33333333-3333-3333-3333-333333333333| DONE   |      |
            """;
    }
}
