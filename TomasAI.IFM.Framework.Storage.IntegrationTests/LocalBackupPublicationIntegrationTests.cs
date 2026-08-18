using System.Security.Cryptography;
using FluentAssertions;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Publication;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests;

public sealed class LocalBackupPublicationIntegrationTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "ifm-gate8", Guid.NewGuid().ToString("N"));

    [Fact]
    [Trait("Category", "Gate8Integration")]
    public async Task Published_artifact_identity_cannot_be_overwritten()
    {
        var fixture = await CreateFixtureAsync(offline: false);
        var operation = await CreateNativeArtifactAsync(fixture, "no-overwrite");
        var published = await PublishAsync(fixture, operation, offline: false);
        var visible = await fixture.Repository.ResolveAsync(
            published.RestorePointId, fixture.OnlineReplica, CancellationToken.None);

        var overwrite = async () => await ((ILocalBackupVault)fixture.Repository).PublishAsync(
            new DatabaseReplicaPublicationRequest(visible.Manifest, operation, fixture.OnlineReplica),
            CancellationToken.None);

        await overwrite.Should().ThrowAsync<IOException>()
            .WithMessage("*already exists*");
        var traversal = () => fixture.Paths.Resolve(
            fixture.Paths.GetReplicaRoot(fixture.OnlineReplica), "../escape");
        traversal.Should().Throw<InvalidOperationException>().WithMessage("*traversal*");
    }

    [Fact]
    [Trait("Category", "Gate8Integration")]
    public async Task Attached_offline_media_must_match_the_enrolled_expected_identity()
    {
        var fixture = await CreateFixtureAsync(offline: true);
        fixture.Options.OfflineMedia.ExpectedMediaId = "unexpected-media-b";

        var validate = async () => await fixture.Repository.ValidateAttachedMediaAsync(CancellationToken.None);

        await validate.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*wrong or untrusted*");
    }

    [Fact]
    [Trait("Category", "Gate8Integration")]
    public async Task Catalog_resolution_rejects_tampered_artifact_content()
    {
        var fixture = await CreateFixtureAsync(offline: false);
        var operation = await CreateNativeArtifactAsync(fixture, "before-tamper");
        var published = await PublishAsync(fixture, operation, offline: false);
        var visible = await fixture.Repository.ResolveAsync(
            published.RestorePointId, fixture.OnlineReplica, CancellationToken.None);
        var manifestPath = fixture.Paths.Resolve(fixture.Paths.GetReplicaRoot(fixture.OnlineReplica),
            visible.Entry.ManifestRelativePath);
        var originalManifest = await File.ReadAllBytesAsync(manifestPath);
        await File.AppendAllTextAsync(manifestPath, " ");
        var signedResolve = async () => await fixture.Repository.ResolveAsync(
            published.RestorePointId, fixture.OnlineReplica, CancellationToken.None);
        await signedResolve.Should().ThrowAsync<CryptographicException>()
            .WithMessage("*signature is invalid*");
        await File.WriteAllBytesAsync(manifestPath, originalManifest);
        var artifact = fixture.Paths.Resolve(fixture.Paths.GetReplicaRoot(fixture.OnlineReplica),
            visible.Manifest.Artifacts.Single().RelativePath);
        await File.AppendAllTextAsync(artifact, "tamper");

        var resolve = async () => await fixture.Repository.ResolveAsync(
            published.RestorePointId, fixture.OnlineReplica, CancellationToken.None);

        await resolve.Should().ThrowAsync<CryptographicException>()
            .WithMessage("*digest verification*");
    }

    [Fact]
    [Trait("Category", "Gate8Integration")]
    public async Task Publication_fails_before_copy_when_capacity_reserve_is_not_available()
    {
        var fixture = await CreateFixtureAsync(offline: false, rejectCapacity: true);
        var operation = await CreateNativeArtifactAsync(fixture, "capacity");

        var publish = async () => await PublishAsync(fixture, operation, offline: false);

        await publish.Should().ThrowAsync<IOException>()
            .WithMessage("*capacity*");
    }

    [Fact]
    [Trait("Category", "Gate8Integration")]
    public async Task Retention_plan_protects_dependencies_of_a_retained_restore_point()
    {
        var fixture = await CreateFixtureAsync(offline: false);
        var baseOperation = await CreateNativeArtifactAsync(fixture, "base");
        var basePoint = await PublishAsync(fixture, baseOperation, offline: false);
        var expiredOperation = await CreateNativeArtifactAsync(fixture, "expired-independent");
        var expiredPoint = await PublishAsync(fixture, expiredOperation, offline: false);
        var childOperation = await CreateNativeArtifactAsync(fixture, "child");
        var childPoint = await PublishAsync(fixture, childOperation, offline: false, [basePoint.RestorePointId]);
        var governance = fixture.Governance;
        var planId = new DatabaseRetentionPlanId(Guid.NewGuid());

        var plan = await governance.CreatePlanAsync(
            new DatabaseRetentionEvaluationRequest(
                planId,
                1,
                DateTimeOffset.UtcNow.AddMinutes(1),
                fixture.OnlineReplica,
                [childPoint.RestorePointId],
                [],
                []),
            CancellationToken.None);

        plan.Entries.Select(static entry => entry.RestorePointId).Should().Equal(expiredPoint.RestorePointId);
        plan.DependencyProtectedRestorePoints.Should().Equal(basePoint.RestorePointId);
        var executed = await governance.ExecuteAsync(
            new DatabaseRetentionExecutionRequest(planId, 1, fixture.OnlineReplica, "approved-gate8"),
            CancellationToken.None);
        executed.DeletedRestorePointCount.Should().Be(1);
        _ = await fixture.Repository.ResolveAsync(basePoint.RestorePointId, fixture.OnlineReplica, CancellationToken.None);
        _ = await fixture.Repository.ResolveAsync(childPoint.RestorePointId, fixture.OnlineReplica, CancellationToken.None);
        var expired = async () => await fixture.Repository.ResolveAsync(
            expiredPoint.RestorePointId, fixture.OnlineReplica, CancellationToken.None);
        await expired.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    [Trait("Category", "Gate8Integration")]
    public async Task Restore_source_is_verified_staged_and_drill_evidence_is_immutable()
    {
        var fixture = await CreateFixtureAsync(offline: true);
        var backupOperation = await CreateNativeArtifactAsync(fixture, "restore-drill");
        var published = await PublishAsync(fixture, backupOperation, offline: true);
        var online = await fixture.Repository.ResolveAsync(
            published.RestorePointId, fixture.OnlineReplica, CancellationToken.None);
        await File.AppendAllTextAsync(
            fixture.Paths.Resolve(fixture.Paths.GetReplicaRoot(fixture.OnlineReplica),
                online.Manifest.Artifacts.Single().RelativePath),
            "online-corruption");
        Directory.Delete(Path.Combine(fixture.PostgreSql.ResolveBackupRoot(), backupOperation.Format()), recursive: true);
        var restoreOperation = new DatabaseRecoveryOperationId(Guid.NewGuid());

        var prepared = await fixture.Repository.PrepareAsync(
            new DatabaseRestoreSourceRequest(
                restoreOperation, published.RestorePointId, DatabaseEngine.PostgreSql),
            CancellationToken.None);
        var now = DateTimeOffset.UtcNow;
        var evidence = new DatabaseRestoreDrillEvidence(
            restoreOperation,
            published.RestorePointId,
            prepared.ReplicaId,
            DatabaseEngine.PostgreSql,
            now.AddSeconds(-2),
            now,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(2),
            true,
            true,
            "fresh-target-gate8");
        var relative = await fixture.Governance.WriteDrillEvidenceAsync(evidence, CancellationToken.None);

        prepared.ReplicaId.Should().Be(fixture.OfflineReplica);
        File.Exists(fixture.Paths.Resolve(fixture.Paths.GetReplicaRoot(prepared.ReplicaId), relative)).Should().BeTrue();
        File.ReadAllText(Path.Combine(fixture.PostgreSql.ResolveBackupRoot(), published.RestorePointId.Value, "payload.txt"))
            .Should().Be("restore-drill");
        var overwrite = async () => await fixture.Governance.WriteDrillEvidenceAsync(evidence, CancellationToken.None);
        await overwrite.Should().ThrowAsync<IOException>();
    }

    [Fact]
    [Trait("Category", "Gate8Integration")]
    public async Task Signed_offline_break_glass_record_reconciles_without_Core_or_NATS()
    {
        var fixture = await CreateFixtureAsync(offline: true);
        var backupOperation = await CreateNativeArtifactAsync(fixture, "break-glass");
        var published = await PublishAsync(fixture, backupOperation, offline: true);
        var operation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;
        var record = new DatabaseBreakGlassRecoveryRecord
        {
            RecoveryOperationId = operation,
            RestorePointId = published.RestorePointId,
            ReplicaId = fixture.OfflineReplica,
            MediaId = fixture.Options.OfflineMedia.ExpectedMediaId,
            AuthorizationReference = "external-approval-gate8",
            OperatorIdentity = "recovery-operator",
            RecoveryHostId = "replacement-workstation",
            ManifestId = published.ManifestId,
            ArtifactVersions = [published.ManifestId],
            StartedUtc = now.AddMinutes(-3),
            CompletedUtc = now,
            AchievedRpo = TimeSpan.FromMinutes(2),
            AchievedRto = TimeSpan.FromMinutes(3),
            NativeValidationSucceeded = true,
            ApplicationValidationSucceeded = true,
            CutoverDecision = "AwaitingApproval"
        };

        _ = await fixture.Governance.WriteBreakGlassRecordAsync(record, CancellationToken.None);
        var reconciled = await fixture.Governance.ReconcileBreakGlassRecordAsync(operation, CancellationToken.None);

        reconciled.Should().BeEquivalentTo(record);
    }

    async Task<Fixture> CreateFixtureAsync(bool offline, bool rejectCapacity = false)
    {
        Directory.CreateDirectory(_root);
        var keyDirectory = Path.Combine(_root, "keys");
        Directory.CreateDirectory(keyDirectory);
        var privateKey = Path.Combine(keyDirectory, "private.pem");
        var publicKey = Path.Combine(keyDirectory, "public.pem");
        using (var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256))
        {
            await File.WriteAllTextAsync(privateKey, ecdsa.ExportECPrivateKeyPem());
            await File.WriteAllTextAsync(publicKey, ecdsa.ExportSubjectPublicKeyInfoPem());
        }
        var options = new DatabaseBackupPublicationOptions
        {
            EnvironmentId = "gate8",
            OnlineVault = new OnlineVaultOptions
            {
                Root = Path.Combine(_root, "online"),
                ReplicaId = "online-vault",
                MediaId = "gate8-online",
                MinimumFreeBytes = 0
            },
            OfflineMedia = new OfflineMediaOptions
            {
                Enabled = offline,
                Root = Path.Combine(_root, "offline"),
                ReplicaId = "offline-media-a",
                ExpectedMediaId = "gate8-media-a",
                RotationSlot = "A",
                MinimumFreeBytes = 0
            },
            RestoreWorkspace = new RestoreWorkspaceOptions
            {
                Root = Path.Combine(_root, "workspace"),
                MinimumFreeBytes = 0
            },
            Manifest = new DatabaseManifestOptions
            {
                KeyId = "gate8-key",
                PrivateKeyPemFile = privateKey,
                PublicKeyPemFile = publicKey
            }
        };
        var postgreSql = new PostgreSqlBackupOptions
        {
            BackupRoot = Path.Combine(_root, "postgres-native"),
            RestoreRoot = Path.Combine(_root, "postgres-restore"),
            RequirePersistentBackupRoot = false
        };
        var scylla = new ScyllaBackupOptions
        {
            BackupRoot = Path.Combine(_root, "scylla-native"),
            RestoreRoot = Path.Combine(_root, "scylla-restore"),
            RequirePersistentBackupRoot = false
        };
        var paths = new LocalBackupPathPolicy(options, postgreSql, scylla);
        var signatures = new EcdsaManifestSignatureService(options.Manifest);
        var checksums = new Sha256ArtifactChecksumService(paths);
        ILocalBackupCapacityReader capacity = rejectCapacity
            ? new RejectingCapacityReader()
            : new LocalBackupCapacityReader();
        var manifests = new LocalBackupManifestStore(paths, signatures, options);
        var repository = new LocalBackupRepository(
            options, postgreSql, scylla, paths, checksums, signatures, manifests, manifests, capacity);
        var onlineReplica = new DatabaseArtifactReplicaId(options.OnlineVault.ReplicaId);
        await ((ILocalBackupVault)repository).EnrollAsync(
            new DatabaseMediaEnrollmentRequest(
                options.OnlineVault.MediaId, onlineReplica, options.EnvironmentId, "online", DateTimeOffset.UtcNow),
            CancellationToken.None);
        var offlineReplica = new DatabaseArtifactReplicaId(options.OfflineMedia.ReplicaId);
        if (offline)
            await ((IOfflineBackupMediaProvider)repository).EnrollAsync(
                new DatabaseMediaEnrollmentRequest(
                    options.OfflineMedia.ExpectedMediaId, offlineReplica, options.EnvironmentId,
                    options.OfflineMedia.RotationSlot, DateTimeOffset.UtcNow),
                CancellationToken.None);
        var governance = new LocalBackupGovernanceStore(options, paths, signatures, repository);
        return new Fixture(options, postgreSql, paths, repository, governance, onlineReplica, offlineReplica);
    }

    static async Task<DatabaseRecoveryOperationId> CreateNativeArtifactAsync(Fixture fixture, string content)
    {
        var operation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var root = Path.Combine(fixture.PostgreSql.ResolveBackupRoot(), operation.Format());
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "payload.txt"), content);
        return operation;
    }

    static ValueTask<DatabaseBackupPublicationResult> PublishAsync(
        Fixture fixture,
        DatabaseRecoveryOperationId operation,
        bool offline,
        DatabaseRestorePointId[]? dependencies = null)
        => fixture.Repository.PublishAsync(
            new DatabaseBackupPublicationRequest(
                operation,
                new DatabaseProtectionSetId("core-postgresql"),
                DatabaseEngine.PostgreSql,
                $"boundary-{operation.Value:N}",
                offline
                    ? [new DatabaseLogicalDestination(fixture.OnlineReplica.Value, true),
                        new DatabaseLogicalDestination(fixture.OfflineReplica.Value, true)]
                    : [new DatabaseLogicalDestination(fixture.OnlineReplica.Value, true)],
                Dependencies: dependencies,
                BackupLineage: dependencies is { Length: > 0 }
                    ? new DatabaseBackupLineage
                    {
                        RequestedMode = DatabaseBackupMode.Incremental,
                        ResolvedMode = DatabaseBackupMode.Incremental,
                        NativeKind = DatabaseNativeBackupKind.PostgreSqlIncremental,
                        BaseRestorePointId = dependencies[0],
                        ParentRestorePointId = dependencies[0],
                        ChainDepth = 1,
                        NativeIdentity = "gate8-postgresql"
                    }
                    : new DatabaseBackupLineage
                    {
                        RequestedMode = DatabaseBackupMode.Full,
                        ResolvedMode = DatabaseBackupMode.Full,
                        NativeKind = DatabaseNativeBackupKind.PostgreSqlBase,
                        BaseRestorePointId = new DatabaseRestorePointId(operation.Format()),
                        NativeIdentity = "gate8-postgresql"
                    }),
            CancellationToken.None);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    sealed record Fixture(
        DatabaseBackupPublicationOptions Options,
        PostgreSqlBackupOptions PostgreSql,
        LocalBackupPathPolicy Paths,
        LocalBackupRepository Repository,
        LocalBackupGovernanceStore Governance,
        DatabaseArtifactReplicaId OnlineReplica,
        DatabaseArtifactReplicaId OfflineReplica);

    sealed class RejectingCapacityReader : ILocalBackupCapacityReader
    {
        public long GetAvailableBytes(DatabaseApprovedStorageRoot approvedRoot) => 0;
        public void EnsureCapacity(DatabaseApprovedStorageRoot approvedRoot, long requiredBytes, long reserveBytes)
            => throw new IOException("The approved storage root lacks capacity.");
    }
}
