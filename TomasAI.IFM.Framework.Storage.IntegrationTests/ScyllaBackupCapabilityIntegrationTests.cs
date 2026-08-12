using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Scylla;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests;

public sealed class ScyllaBackupCapabilityIntegrationTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "ifm-gate7", Guid.NewGuid().ToString("N"));

    [Fact]
    [Trait("Category", "Gate7Integration")]
    public async Task Backup_restart_native_verification_and_fresh_target_restore_preserve_synthetic_data()
    {
        var options = Options();
        var native = new DeterministicScyllaAdministrationClient("synthetic-gate7-row");
        var backupOperation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var firstHost = new ScyllaBackupCapability(options, native);

        await firstHost.ValidateAsync(CancellationToken.None);
        var captured = await firstHost.CreateBackupAsync(
            new ScyllaBackupRequest(backupOperation, new DatabaseProtectionSetId("read-model-scylla")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);

        var restartedHost = new ScyllaBackupCapability(options, native);
        await restartedHost.ValidateAsync(CancellationToken.None);
        var recovered = await restartedHost.CreateBackupAsync(
            new ScyllaBackupRequest(backupOperation, new DatabaseProtectionSetId("read-model-scylla")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);
        var verified = await restartedHost.VerifyAsync(
            new ScyllaVerificationRequest(backupOperation, recovered.SafeBoundaryReference), CancellationToken.None);
        var restoreOperation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var request = new ScyllaRestoreRequest(
            restoreOperation,
            new DatabaseRestorePointId(backupOperation.Format()),
            new DatabaseFreshTargetDescriptor("disposable-validation", "gate7"));
        var restored = await restartedHost.RestoreToFreshTargetAsync(
            request, new Progress<DatabaseNativeProgress>(), CancellationToken.None);
        var replay = await restartedHost.RestoreToFreshTargetAsync(
            request, new Progress<DatabaseNativeProgress>(), CancellationToken.None);

        recovered.Should().Be(captured);
        captured.Topology!.LiveNodeCount.Should().Be(3);
        captured.Topology.SchemaAgreement.Should().BeTrue();
        captured.Snapshot!.KeyspaceCount.Should().Be(1);
        captured.Snapshot.TableCount.Should().Be(1);
        captured.Snapshot.ArtifactCount.Should().Be(2);
        captured.Snapshot.SchemaSha256.Should().HaveLength(64);
        captured.Snapshot.NativeManifestSha256.Should().HaveLength(64);
        captured.Statistics!.Engine.Should().Be(DatabaseEngine.ScyllaDb);
        verified.Succeeded.Should().BeTrue();
        verified.Level.Should().Be(DatabaseVerificationLevel.Native);
        restored.Succeeded.Should().BeTrue();
        restored.SourceClusterName.Should().Be("gate7-source");
        restored.RestoredClusterName.Should().Be("gate7-fresh");
        restored.Topology!.SchemaAgreement.Should().BeTrue();
        restored.ValidationRevision.Should().Be(700_001);
        restored.Statistics!.Engine.Should().Be(DatabaseEngine.ScyllaDb);
        replay.Should().Be(restored);
        native.CaptureCount.Should().Be(1);
        native.RestoreCount.Should().Be(1);
        native.RestoredPayload.Should().Be("synthetic-gate7-row");
    }

    [Fact]
    [Trait("Category", "Gate7Integration")]
    public async Task Native_verification_rejects_tampered_sstable_evidence()
    {
        var options = Options();
        var native = new DeterministicScyllaAdministrationClient("synthetic-gate7-row");
        var capability = new ScyllaBackupCapability(options, native);
        var operation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var boundary = await capability.CreateBackupAsync(
            new ScyllaBackupRequest(operation, new DatabaseProtectionSetId("read-model-scylla")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);
        var data = Path.Combine(options.ResolveBackupRoot(), operation.Format() + ".inprogress", "native", "probe-Data.db");
        await File.AppendAllTextAsync(data, "tampered");

        var result = await capability.VerifyAsync(
            new ScyllaVerificationRequest(operation, boundary.SafeBoundaryReference), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Gate7Integration")]
    public async Task Non_allowlisted_protection_sets_and_fresh_targets_are_rejected_before_native_execution()
    {
        var options = Options();
        var native = new DeterministicScyllaAdministrationClient("synthetic-gate7-row");
        var capability = new ScyllaBackupCapability(options, native);
        var operation = new DatabaseRecoveryOperationId(Guid.NewGuid());

        Func<Task> backup = async () => await capability.CreateBackupAsync(
            new ScyllaBackupRequest(operation, new DatabaseProtectionSetId("production-unapproved")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);
        Func<Task> restore = async () => await capability.RestoreToFreshTargetAsync(
            new ScyllaRestoreRequest(operation, new DatabaseRestorePointId("missing"),
                new DatabaseFreshTargetDescriptor("disposable-validation", "not-allowed")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);

        await backup.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not allowlisted*");
        await restore.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not allowlisted*");
        native.CaptureCount.Should().Be(0);
        native.RestoreCount.Should().Be(0);
    }

    ScyllaBackupOptions Options() => new()
    {
        ToolDirectory = Path.Combine(_root, "tools"),
        BackupRoot = Path.Combine(_root, "backup"),
        RestoreRoot = Path.Combine(_root, "restore"),
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
                ManagerCluster = "gate7-fresh",
                AllowedLogicalTargets = ["gate7"],
                RequiredLiveNodes = 1
            }
        },
        OperationTimeout = TimeSpan.FromMinutes(1),
        PollInterval = TimeSpan.FromMilliseconds(10),
        RequirePersistentBackupRoot = false
    };

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    sealed class DeterministicScyllaAdministrationClient(string payload) : IScyllaAdministrationClient
    {
        public int CaptureCount { get; private set; }
        public int RestoreCount { get; private set; }
        public string RestoredPayload { get; private set; } = string.Empty;

        public ValueTask ValidateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }

        public async ValueTask<ScyllaNativeCapture> CaptureAsync(
            DatabaseRecoveryOperationId operationId,
            ScyllaProtectionSetOptions protectionSet,
            string nativeDirectory,
            IProgress<DatabaseNativeProgress> progress,
            CancellationToken cancellationToken)
        {
            CaptureCount++;
            var schema = "CREATE KEYSPACE gate7_keyspace; CREATE TABLE gate7_keyspace.probe(id int PRIMARY KEY, payload text);";
            var schemaPath = Path.Combine(nativeDirectory, "schema.cql");
            var dataPath = Path.Combine(nativeDirectory, "probe-Data.db");
            await File.WriteAllTextAsync(schemaPath, schema, cancellationToken);
            await File.WriteAllTextAsync(dataPath, payload, cancellationToken);
            var schemaDigest = Sha256(schema);
            var manifestDigest = await ScyllaEvidenceSerializer.DirectoryManifestSha256Async(nativeDirectory, cancellationToken);
            return new ScyllaNativeCapture(
                "backup/gate7", "sm_20260812000000UTC",
                new ScyllaTopologyEvidence("gate7-source", 3, 768, true),
                schemaDigest, manifestDigest, ["schema.cql", "probe-Data.db"], 1, 1,
                new FileInfo(dataPath).Length, "2025.1.4", "3.11.2", TimeSpan.FromSeconds(1));
        }

        public async ValueTask<ScyllaNativeVerification> VerifyAsync(
            ScyllaProtectionSetOptions protectionSet,
            ScyllaNativeCapture capture,
            string nativeDirectory,
            CancellationToken cancellationToken)
        {
            var digest = await ScyllaEvidenceSerializer.DirectoryManifestSha256Async(nativeDirectory, cancellationToken);
            var bytes = Directory.EnumerateFiles(nativeDirectory).Sum(static file => new FileInfo(file).Length);
            return new ScyllaNativeVerification(
                string.Equals(digest, capture.NativeManifestSha256, StringComparison.Ordinal),
                capture.Topology, digest, bytes, TimeSpan.FromMilliseconds(20));
        }

        public async ValueTask<ScyllaNativeRestoreValidation> RestoreAsync(
            DatabaseRecoveryOperationId operationId,
            ScyllaProtectionSetOptions source,
            ScyllaFreshTargetProfileOptions target,
            ScyllaNativeCapture capture,
            string sourceNativeDirectory,
            string restoreWorkspace,
            IProgress<DatabaseNativeProgress> progress,
            CancellationToken cancellationToken)
        {
            RestoreCount++;
            Directory.CreateDirectory(restoreWorkspace);
            var restored = Path.Combine(restoreWorkspace, "probe-Data.db");
            File.Copy(Path.Combine(sourceNativeDirectory, "probe-Data.db"), restored);
            RestoredPayload = await File.ReadAllTextAsync(restored, cancellationToken);
            return new ScyllaNativeRestoreValidation(
                RestoredPayload == payload,
                "gate7-fresh",
                new ScyllaTopologyEvidence("gate7-fresh", 1, 256, true),
                700_001,
                new FileInfo(restored).Length,
                TimeSpan.FromSeconds(2));
        }

        static string Sha256(string value)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
