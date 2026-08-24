using System.Diagnostics;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Scylla;

public sealed class ScyllaBackupCapability : IScyllaBackupCapability, IDatabaseNativeCapabilityValidation
{
    readonly ScyllaBackupOptions _options;
    readonly ScyllaBackupPathResolver _paths;
    readonly IScyllaAdministrationClient _administration;
    readonly IScyllaSnapshotArtifactTransport _snapshotArtifacts;
    int _validated;

    public ScyllaBackupCapability(ScyllaBackupOptions options)
        : this(options, new ScyllaManagerCliAdministrationClient(options), null) { }

    public ScyllaBackupCapability(
        ScyllaBackupOptions options,
        IScyllaSnapshotArtifactTransport snapshotArtifacts)
        : this(options, new ScyllaManagerCliAdministrationClient(options), snapshotArtifacts) { }

    internal ScyllaBackupCapability(
        ScyllaBackupOptions options,
        IScyllaAdministrationClient administration,
        IScyllaSnapshotArtifactTransport? snapshotArtifacts = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _paths = new ScyllaBackupPathResolver(options);
        _administration = administration ?? throw new ArgumentNullException(nameof(administration));
        _snapshotArtifacts = snapshotArtifacts ?? new ReferenceOnlyScyllaSnapshotArtifactTransport();
    }

    public async ValueTask ValidateAsync(CancellationToken cancellationToken)
    {
        _options.Validate();
        Directory.CreateDirectory(_paths.BackupRoot);
        Directory.CreateDirectory(_paths.RestoreRoot);
        await _administration.ValidateAsync(cancellationToken).ConfigureAwait(false);
        Volatile.Write(ref _validated, 1);
    }

    public async ValueTask<ScyllaBackupBoundary> CreateBackupAsync(
        ScyllaBackupRequest request,
        IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken)
    {
        var protectionSet = ValidateRequest(request);
        var lineage = (request.BackupLineage ?? new DatabaseBackupLineage())
            .NormalizeLegacyFull(DatabaseEngine.ScyllaDb) with
        {
            NativeIdentity = $"{protectionSet.ManagerCluster}:{string.Join(',', protectionSet.Keyspaces.Order(StringComparer.Ordinal))}"
        };
        lineage.Validate(resolvedRequired: true);
        ArgumentNullException.ThrowIfNull(progress);
        await EnsureValidatedAsync(cancellationToken).ConfigureAwait(false);
        var final = _paths.BackupFinal(request.OperationId);
        if (File.Exists(ScyllaEvidenceSerializer.BackupEvidencePath(final)))
            return Boundary(await ScyllaEvidenceSerializer.ReadBackupAsync(final, cancellationToken).ConfigureAwait(false));

        var staging = _paths.BackupStaging(request.OperationId);
        if (File.Exists(ScyllaEvidenceSerializer.BackupEvidencePath(staging)))
            return Boundary(await ScyllaEvidenceSerializer.ReadBackupAsync(staging, cancellationToken).ConfigureAwait(false));
        if (Directory.Exists(staging) && Directory.EnumerateFileSystemEntries(staging).Any())
            throw new InvalidOperationException("An incomplete Scylla native capture requires explicit reconciliation.");
        Directory.CreateDirectory(staging);
        var nativeDirectory = Directory.CreateDirectory(Path.Combine(staging, "native")).FullName;
        var capture = await _administration.CaptureAsync(
            request.OperationId, protectionSet, nativeDirectory, progress, cancellationToken).ConfigureAwait(false);
        ValidateCapture(capture, protectionSet);
        var portableBytes = await _snapshotArtifacts.ExportAsync(
            protectionSet.BackupLocation,
            capture.SnapshotTag,
            capture.ArtifactReferences,
            nativeDirectory,
            cancellationToken).ConfigureAwait(false);
        var statistics = Statistics(
            DatabaseRecoveryPhase.Capturing, Math.Max(capture.SourceBytes, portableBytes), null,
            capture.ArtifactReferences.Length, capture.Elapsed);
        var evidence = new ScyllaBackupEvidence(
            request.OperationId,
            request.ProtectionSetId.Value,
            SafeBoundary(capture.NativeManifestSha256),
            capture.Topology,
            Snapshot(capture),
            capture.ArtifactReferences,
            statistics,
            DateTimeOffset.UtcNow,
            lineage,
            protectionSet.BackupLocation);
        await ScyllaEvidenceSerializer.WriteBackupAsync(staging, evidence, cancellationToken).ConfigureAwait(false);
        return Boundary(evidence);
    }

    public async ValueTask<ScyllaVerificationResult> VerifyAsync(
        ScyllaVerificationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OperationId.Value == Guid.Empty || string.IsNullOrWhiteSpace(request.SafeBoundaryReference))
            throw new ArgumentException("Scylla verification requires an operation and safe boundary reference.", nameof(request));
        await EnsureValidatedAsync(cancellationToken).ConfigureAwait(false);
        var final = _paths.BackupFinal(request.OperationId);
        if (File.Exists(ScyllaEvidenceSerializer.BackupEvidencePath(final)))
        {
            var published = await ScyllaEvidenceSerializer.ReadBackupAsync(final, cancellationToken).ConfigureAwait(false);
            EnsureBoundary(published.SafeBoundaryReference, request.SafeBoundaryReference);
            return Verification(published, published.Topology, published.Statistics);
        }
        var staging = _paths.BackupStaging(request.OperationId);
        var evidence = await ScyllaEvidenceSerializer.ReadBackupAsync(staging, cancellationToken).ConfigureAwait(false);
        EnsureBoundary(evidence.SafeBoundaryReference, request.SafeBoundaryReference);
        if (!_options.ProtectionSets.TryGetValue(evidence.ProtectionSetId, out var protectionSet))
            throw new InvalidOperationException("The Scylla backup evidence references a protection set not configured on this host.");
        var capture = Capture(evidence);
        var verification = await _administration.VerifyAsync(
            protectionSet, capture, Path.Combine(staging, "native"), cancellationToken).ConfigureAwait(false);
        if (!verification.Succeeded
            || !string.Equals(verification.NativeManifestSha256, evidence.Snapshot.NativeManifestSha256, StringComparison.Ordinal))
            return new ScyllaVerificationResult(DatabaseVerificationLevel.Native, false);
        var statistics = Statistics(
            DatabaseRecoveryPhase.Verifying, verification.SourceBytes, null,
            evidence.Snapshot.ArtifactCount, verification.Elapsed);
        Directory.Move(staging, final);
        return Verification(evidence, verification.Topology, statistics);
    }

    public async ValueTask<ScyllaRestoreResult> RestoreToFreshTargetAsync(
        ScyllaRestoreRequest request,
        IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken)
    {
        var target = ValidateRestoreRequest(request);
        ArgumentNullException.ThrowIfNull(progress);
        await EnsureValidatedAsync(cancellationToken).ConfigureAwait(false);
        var final = _paths.RestoreFinal(request);
        if (File.Exists(ScyllaEvidenceSerializer.RestoreEvidencePath(final)))
            return RestoreResult(await ScyllaEvidenceSerializer.ReadRestoreAsync(final, cancellationToken).ConfigureAwait(false));

        var sourceRoot = _paths.RestorePoint(request.RestorePointId);
        var sourceEvidence = await ScyllaEvidenceSerializer.ReadBackupAsync(sourceRoot, cancellationToken).ConfigureAwait(false);
        if (!_options.ProtectionSets.TryGetValue(sourceEvidence.ProtectionSetId, out var protectionSet))
            throw new InvalidOperationException("The Scylla restore point references a protection set not configured on this host.");
        var capture = Capture(sourceEvidence);
        ValidateCapture(capture, protectionSet);
        ValidateRecoveryExpectation(request.ExpectedRecovery, sourceEvidence);
        var restoreProtectionSet = RestoreProtectionSet(protectionSet);
        _ = await _snapshotArtifacts.EnsureAvailableAsync(
            sourceEvidence.BackupLocation ?? protectionSet.BackupLocation,
            restoreProtectionSet.BackupLocation,
            capture.SnapshotTag,
            Path.Combine(sourceRoot, "native"),
            cancellationToken).ConfigureAwait(false);
        var verification = await _administration.VerifyAsync(
            restoreProtectionSet, capture, Path.Combine(sourceRoot, "native"), cancellationToken).ConfigureAwait(false);
        if (!verification.Succeeded)
            throw new InvalidDataException("The Scylla restore point failed native verification.");

        var staging = _paths.RestoreStaging(request);
        if (Directory.Exists(staging))
            throw new InvalidOperationException("An incomplete Scylla fresh-target restore requires explicit reconciliation.");
        Directory.CreateDirectory(staging);
        var validation = await _administration.RestoreAsync(
            request.OperationId,
            restoreProtectionSet,
            target,
            capture,
            Path.Combine(sourceRoot, "native"),
            Path.Combine(staging, "workspace"),
            progress,
            cancellationToken).ConfigureAwait(false);
        if (!validation.Succeeded || !validation.Topology.SchemaAgreement
            || validation.Topology.LiveNodeCount < target.RequiredLiveNodes)
            throw new InvalidOperationException("The Scylla fresh target failed native or application validation.");
        var statistics = Statistics(
            DatabaseRecoveryPhase.Validating,
            sourceEvidence.Statistics.SourceBytes,
            validation.RestoredBytes,
            sourceEvidence.Snapshot.ArtifactCount,
            validation.Elapsed);
        var evidence = new ScyllaRestoreEvidence(
            request.OperationId,
            request.RestorePointId.Value,
            $"scylla-fresh-{request.OperationId.Format()[..12]}",
            sourceEvidence.Topology.ClusterName,
            validation.RestoredClusterName,
            validation.ValidationRevision,
            validation.Topology,
            statistics,
            DateTimeOffset.UtcNow);
        await ScyllaEvidenceSerializer.WriteRestoreAsync(staging, evidence, cancellationToken).ConfigureAwait(false);
        Directory.Move(staging, final);
        return RestoreResult(evidence);
    }

    async ValueTask EnsureValidatedAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _validated) == 0) await ValidateAsync(cancellationToken).ConfigureAwait(false);
    }

    ScyllaProtectionSetOptions ValidateRequest(ScyllaBackupRequest request)
    {
        if (request.OperationId.Value == Guid.Empty)
            throw new ArgumentException("A Scylla backup operation ID is required.", nameof(request));
        return _options.ProtectionSets.TryGetValue(request.ProtectionSetId.Value, out var protectionSet)
            ? protectionSet
            : throw new InvalidOperationException("The Scylla protection set is not allowlisted by this host.");
    }

    ScyllaFreshTargetProfileOptions ValidateRestoreRequest(ScyllaRestoreRequest request)
    {
        if (request.OperationId.Value == Guid.Empty || string.IsNullOrWhiteSpace(request.RestorePointId.Value))
            throw new ArgumentException("A Scylla restore operation and restore point are required.", nameof(request));
        return _options.FreshTargetProfiles.TryGetValue(request.FreshTarget.Profile, out var target)
            && target.AllowedLogicalTargets.Contains(request.FreshTarget.LogicalTarget, StringComparer.Ordinal)
                ? target
                : throw new InvalidOperationException("The Scylla fresh target is not allowlisted by this host.");
    }

    static void ValidateCapture(ScyllaNativeCapture capture, ScyllaProtectionSetOptions protectionSet)
    {
        if (!capture.Topology.SchemaAgreement || capture.Topology.LiveNodeCount < protectionSet.RequiredLiveNodes)
            throw new InvalidDataException("The Scylla capture does not prove required topology coverage and schema agreement.");
        if (string.IsNullOrWhiteSpace(capture.SnapshotTag) || string.IsNullOrWhiteSpace(capture.SchemaSha256)
            || capture.SchemaSha256.Length != 64 || string.IsNullOrWhiteSpace(capture.NativeManifestSha256)
            || capture.NativeManifestSha256.Length != 64 || capture.ArtifactReferences.Length == 0
            || capture.KeyspaceCount == 0 || capture.TableCount == 0)
            throw new InvalidDataException("The Scylla capture evidence is incomplete.");
    }

    static ScyllaSnapshotEvidence Snapshot(ScyllaNativeCapture capture)
        => new(
            capture.SnapshotTag,
            capture.ManagerTaskReference,
            capture.SchemaSha256,
            capture.NativeManifestSha256,
            capture.KeyspaceCount,
            capture.TableCount,
            capture.Topology.LiveNodeCount,
            capture.ArtifactReferences.Length,
            capture.ScyllaVersion,
            capture.ManagerVersion);

    static ScyllaNativeCapture Capture(ScyllaBackupEvidence evidence)
        => new(
            evidence.Snapshot.ManagerTaskReference,
            evidence.Snapshot.SnapshotTag,
            evidence.Topology,
            evidence.Snapshot.SchemaSha256,
            evidence.Snapshot.NativeManifestSha256,
            evidence.ArtifactReferences,
            evidence.Snapshot.KeyspaceCount,
            evidence.Snapshot.TableCount,
            evidence.Statistics.SourceBytes ?? 0,
            evidence.Snapshot.ScyllaVersion,
            evidence.Snapshot.ManagerVersion,
            evidence.Statistics.Elapsed ?? TimeSpan.Zero);

    static ScyllaBackupBoundary Boundary(ScyllaBackupEvidence evidence)
        => new(evidence.SafeBoundaryReference)
        {
            Topology = evidence.Topology,
            Snapshot = evidence.Snapshot,
            Statistics = evidence.Statistics,
            BackupLineage = evidence.BackupLineage?.NormalizeLegacyFull(DatabaseEngine.ScyllaDb)
        };

    static ScyllaRestoreResult RestoreResult(ScyllaRestoreEvidence evidence)
        => new(true, evidence.ValidationRevision)
        {
            SafeTargetReference = evidence.SafeTargetReference,
            SourceClusterName = evidence.SourceClusterName,
            RestoredClusterName = evidence.RestoredClusterName,
            Topology = evidence.Topology,
            Statistics = evidence.Statistics
        };

    static ScyllaVerificationResult Verification(
        ScyllaBackupEvidence evidence,
        ScyllaTopologyEvidence topology,
        DatabaseRecoveryRunStatistics statistics)
        => new(DatabaseVerificationLevel.Native, true)
        {
            SafeEvidenceReference = $"scylla-verify-{evidence.Snapshot.NativeManifestSha256[..16]}",
            Topology = topology,
            Statistics = statistics
        };

    static DatabaseRecoveryRunStatistics Statistics(
        DatabaseRecoveryPhase phase,
        long? sourceBytes,
        long? restoredBytes,
        int artifactCount,
        TimeSpan elapsed)
        => new()
        {
            Engine = DatabaseEngine.ScyllaDb,
            Phase = phase,
            StartedUtc = DateTimeOffset.UtcNow - elapsed,
            CompletedUtc = DateTimeOffset.UtcNow,
            Elapsed = elapsed,
            SourceBytes = sourceBytes,
            StoredBytes = phase == DatabaseRecoveryPhase.Capturing ? sourceBytes : null,
            TransferredBytes = phase == DatabaseRecoveryPhase.Capturing ? sourceBytes : null,
            RestoredBytes = restoredBytes,
            ArtifactCount = artifactCount,
            AverageThroughputBytesPerSecond = Rate(restoredBytes ?? sourceBytes, elapsed),
            RetryCount = 0,
            WarningCount = 0
        };

    static double? Rate(long? bytes, TimeSpan elapsed)
        => bytes is not null && elapsed > TimeSpan.Zero ? bytes.Value / elapsed.TotalSeconds : null;

    static string SafeBoundary(string manifestSha256) => $"scylla-snapshot-{manifestSha256[..16]}";

    static void EnsureBoundary(string actual, string expected)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidOperationException("The Scylla verification boundary does not match captured evidence.");
    }

    static void ValidateRecoveryExpectation(
        ScyllaRecoveryExpectation? expected,
        ScyllaBackupEvidence actual)
    {
        if (expected is null) return;
        if (expected.Topology != actual.Topology || expected.Snapshot != actual.Snapshot)
            throw new InvalidDataException(
                "The staged Scylla protection set differs from the signed AWS topology or snapshot evidence.");
    }

    static ScyllaProtectionSetOptions RestoreProtectionSet(ScyllaProtectionSetOptions source)
        => string.IsNullOrWhiteSpace(source.RestoreLocation)
            ? source
            : new ScyllaProtectionSetOptions
            {
                ManagerCluster = source.ManagerCluster,
                BackupLocation = source.RestoreLocation,
                RestoreLocation = source.RestoreLocation,
                Keyspaces = source.Keyspaces,
                RequiredLiveNodes = source.RequiredLiveNodes,
                ManagerRetentionCount = source.ManagerRetentionCount
            };
}
