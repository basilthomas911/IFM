using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Startup;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Observability;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;

public sealed record AwsRecoverySourceQualificationResult(
    DatabaseRestorePointId RestorePointId,
    DatabaseEngine Engine,
    int ArtifactCount,
    long VerifiedBytes,
    DateTimeOffset QualifiedUtc);

public sealed class AwsRecoverySourceQualificationService
{
    static readonly DatabaseArtifactReplicaId PrimaryReplica = new("aws-primary");
    static readonly DatabaseArtifactReplicaId RecoveryReplica = new("aws-recovery");
    readonly S3DatabaseBackupCatalog _primary;
    readonly S3DatabaseBackupCatalog _recovery;
    readonly AwsCloudDatabaseBackupOptions _options;
    readonly TimeProvider _timeProvider;
    readonly AwsDatabaseBackupTelemetry? _telemetry;

    public AwsRecoverySourceQualificationService(
        S3DatabaseBackupCatalog primary,
        AwsRecoveryVaultClient recoveryVault,
        IAwsDocumentSignatureService signatures,
        AwsCloudDatabaseBackupOptions options,
        TimeProvider timeProvider,
        AwsDatabaseBackupTelemetry? telemetry = null)
    {
        _primary = primary;
        _options = options;
        _timeProvider = timeProvider;
        _telemetry = telemetry;
        var vault = new AwsVaultLocation(
            options.RecoveryBucketName,
            options.RecoveryRegion,
            options.RecoveryEncryptionKeyArn,
            RecoveryReplica);
        _recovery = new S3DatabaseBackupCatalog(
            recoveryVault.Client,
            new S3ImmutableObjectStore(recoveryVault.Client, options, timeProvider),
            signatures,
            options,
            vault);
    }

    public async ValueTask<AwsRecoverySourceQualificationResult> QualifyAsync(
        DatabaseRestorePointId restorePointId,
        DatabaseEngine engine,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(restorePointId.Value))
            throw new ArgumentException("A recovery-source qualification requires a restore point.", nameof(restorePointId));
        DatabaseBackupEnumValidation.RequireDefined(engine, nameof(engine));
        var primary = await _primary.ResolveAwsAsync(
            restorePointId, PrimaryReplica, cancellationToken).ConfigureAwait(false);
        var recovery = await _recovery.ResolveAwsAsync(
            restorePointId, RecoveryReplica, cancellationToken).ConfigureAwait(false);
        if (primary.RestorePoint.Manifest.Engine != engine || recovery.RestorePoint.Manifest.Engine != engine)
            throw new InvalidDataException("The recovery-source qualification selected the wrong database engine.");
        AwsRecoverySourceQualificationPolicy.Validate(primary, recovery, _options);
        var lag = recovery.Entry.PublishedUtc - primary.Entry.PublishedUtc;
        _telemetry?.RecordReplicationLag(engine, lag < TimeSpan.Zero ? TimeSpan.Zero : lag);
        return new AwsRecoverySourceQualificationResult(
            restorePointId,
            engine,
            recovery.Publication.Artifacts.Length,
            recovery.Publication.Artifacts.Sum(static artifact => artifact.Object.Length),
            _timeProvider.GetUtcNow());
    }
}

public static class AwsRecoverySourceQualificationPolicy
{
    public static void Validate(
        AwsResolvedCatalogRestorePoint primary,
        AwsResolvedCatalogRestorePoint recovery,
        AwsCloudDatabaseBackupOptions options)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(options);
        if (primary.RestorePoint.Entry.RestorePointId != recovery.RestorePoint.Entry.RestorePointId
            || primary.RestorePoint.Manifest.ManifestId != recovery.RestorePoint.Manifest.ManifestId
            || primary.RestorePoint.Manifest.Revision != recovery.RestorePoint.Manifest.Revision
            || primary.RestorePoint.Manifest.Engine != recovery.RestorePoint.Manifest.Engine
            || primary.RestorePoint.Manifest.ProtectionSetId != recovery.RestorePoint.Manifest.ProtectionSetId
            || !primary.RestorePoint.Manifest.Dependencies.SequenceEqual(recovery.RestorePoint.Manifest.Dependencies)
            || primary.RestorePoint.Manifest.BackupLineage != recovery.RestorePoint.Manifest.BackupLineage)
            throw new InvalidDataException("The recovery replica is not logically equivalent to the primary publication.");

        if (recovery.Entry.ReplicaId != new DatabaseArtifactReplicaId("aws-recovery")
            || recovery.Publication.ReplicaId != new DatabaseArtifactReplicaId("aws-recovery"))
            throw new InvalidDataException("The recovery catalog did not preserve explicit recovery replica identity.");
        ValidateObject(recovery.Publication.EngineManifest, options);
        var primaryArtifacts = primary.Publication.Artifacts
            .OrderBy(static artifact => artifact.LogicalRelativePath, StringComparer.Ordinal).ToArray();
        var recoveryArtifacts = recovery.Publication.Artifacts
            .OrderBy(static artifact => artifact.LogicalRelativePath, StringComparer.Ordinal).ToArray();
        if (primaryArtifacts.Length == 0 || primaryArtifacts.Length != recoveryArtifacts.Length)
            throw new InvalidDataException("The recovery replica artifact set is incomplete.");
        for (var index = 0; index < primaryArtifacts.Length; index++)
        {
            var source = primaryArtifacts[index];
            var replica = recoveryArtifacts[index];
            if (!StringComparer.Ordinal.Equals(source.LogicalRelativePath, replica.LogicalRelativePath)
                || !StringComparer.Ordinal.Equals(source.Object.ObjectKey, replica.Object.ObjectKey)
                || source.Object.Length != replica.Object.Length
                || !StringComparer.OrdinalIgnoreCase.Equals(source.Object.Sha256, replica.Object.Sha256)
                || !StringComparer.Ordinal.Equals(source.Object.S3ChecksumSha256, replica.Object.S3ChecksumSha256)
                || replica.Object.RetainUntilUtc < source.Object.RetainUntilUtc)
                throw new InvalidDataException("A recovery artifact differs from its exact primary publication evidence.");
            ValidateObject(replica.Object, options);
        }
    }

    static void ValidateObject(AwsImmutableObjectVersion value, AwsCloudDatabaseBackupOptions options)
    {
        if (!StringComparer.Ordinal.Equals(value.BucketName, options.RecoveryBucketName)
            || !StringComparer.Ordinal.Equals(value.Region, options.RecoveryRegion)
            || !StringComparer.Ordinal.Equals(value.EncryptionKeyArn, options.RecoveryEncryptionKeyArn)
            || string.IsNullOrWhiteSpace(value.VersionId)
            || value.Length < 0
            || value.Sha256?.Length != 64
            || string.IsNullOrWhiteSpace(value.S3ChecksumSha256)
            || value.RetainUntilUtc <= value.PublishedUtc)
            throw new InvalidDataException("The recovery object lacks exact-version, ownership, KMS, checksum, or retention evidence.");
    }
}
