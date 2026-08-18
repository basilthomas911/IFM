using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Publication;

public sealed class LocalBackupManifestStore(
    IBackupPathPolicy pathPolicy,
    IManifestSignatureService signatures,
    DatabaseBackupPublicationOptions? options = null)
    : IDatabaseBackupManifestWriter, IDatabaseBackupManifestReader
{
    public ValueTask WriteSignedAsync(
        DatabaseApprovedStorageRoot approvedRoot,
        string relativePath,
        DatabaseBackupManifest manifest,
        CancellationToken cancellationToken)
    {
        Validate(manifest);
        if (LocalBackupJson.Serialize(manifest).LongLength > (options?.Limits.MaximumManifestBytes ?? 64L << 20))
            throw new InvalidDataException("The database backup manifest exceeds its configured size limit.");
        var path = pathPolicy.Resolve(approvedRoot, relativePath);
        return LocalBackupJson.WriteSignedCreateNewAsync(path, manifest, signatures, cancellationToken);
    }

    public async ValueTask<DatabaseBackupManifest> ReadAndVerifyAsync(
        DatabaseApprovedStorageRoot approvedRoot,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var path = pathPolicy.Resolve(approvedRoot, relativePath);
        if (new FileInfo(path).Length > (options?.Limits.MaximumManifestBytes ?? 64L << 20))
            throw new InvalidDataException("The database backup manifest exceeds its configured size limit.");
        var manifest = await LocalBackupJson.ReadSignedAsync<DatabaseBackupManifest>(
            path, signatures, cancellationToken).ConfigureAwait(false);
        Validate(manifest);
        return manifest;
    }

    internal static void Validate(DatabaseBackupManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.SchemaVersion is not (1 or 2) || manifest.Revision <= 0)
            throw new InvalidDataException("The database backup manifest schema or revision is unsupported.");
        _ = new DatabaseProtectionSetId(manifest.ManifestId);
        if (manifest.OperationId.Value == Guid.Empty || string.IsNullOrWhiteSpace(manifest.RestorePointId.Value))
            throw new InvalidDataException("The database backup manifest identity is invalid.");
        DatabaseBackupEnumValidation.RequireConcrete(manifest.Source);
        DatabaseBackupEnumValidation.RequireDefined(manifest.Engine, nameof(manifest.Engine));
        var lineage = manifest.BackupLineage.NormalizeLegacyFull(manifest.Engine);
        lineage.Validate(resolvedRequired: true);
        if (manifest.SchemaVersion == 2 && manifest.BackupLineage.ResolvedMode == DatabaseBackupMode.None)
            throw new InvalidDataException("A version 2 manifest requires resolved backup lineage.");
        if (manifest.SchemaVersion == 2 && lineage.ResolvedMode == DatabaseBackupMode.Full
            && lineage.BaseRestorePointId != manifest.RestorePointId)
            throw new InvalidDataException("A version 2 full manifest must identify itself as the chain base.");
        if (lineage.NativeKind is DatabaseNativeBackupKind.PostgreSqlBase or DatabaseNativeBackupKind.PostgreSqlIncremental
            && manifest.Engine != DatabaseEngine.PostgreSql)
            throw new InvalidDataException("The manifest native kind conflicts with its database engine.");
        if (lineage.NativeKind is DatabaseNativeBackupKind.ScyllaManagerSnapshot or DatabaseNativeBackupKind.ScyllaManagerDeduplicatedSnapshot
            && manifest.Engine != DatabaseEngine.ScyllaDb)
            throw new InvalidDataException("The manifest native kind conflicts with its database engine.");
        if (manifest.Source != BackupSource.LocalWorkstation)
            throw new InvalidDataException("A local vault cannot publish a non-local manifest.");
        if (manifest.CreatedUtc == default || manifest.CreatedUtc.Offset != TimeSpan.Zero)
            throw new InvalidDataException("Manifest creation time must be UTC.");
        if (string.IsNullOrWhiteSpace(manifest.SafeBoundaryReference)
            || manifest.SafeBoundaryReference.Any(char.IsControl))
            throw new InvalidDataException("The manifest boundary reference is invalid.");
        if (manifest.Artifacts.Length == 0 || manifest.Replicas.Length == 0)
            throw new InvalidDataException("The manifest must contain artifacts and replicas.");
        if (manifest.Artifacts.Select(static value => value.RelativePath)
            .Distinct(StringComparer.Ordinal).Count() != manifest.Artifacts.Length)
            throw new InvalidDataException("The manifest contains duplicate artifact paths.");
        if (manifest.Dependencies.Contains(manifest.RestorePointId)
            || manifest.Dependencies.Distinct().Count() != manifest.Dependencies.Length)
            throw new InvalidDataException("The manifest dependency graph contains a self-reference or duplicate.");
        if (lineage.NativeKind == DatabaseNativeBackupKind.PostgreSqlIncremental
            && (manifest.Dependencies.Length != 1
                || manifest.Dependencies[0] != lineage.ParentRestorePointId))
            throw new InvalidDataException("A PostgreSQL incremental manifest requires its direct parent dependency.");
        if (lineage.NativeKind != DatabaseNativeBackupKind.PostgreSqlIncremental
            && manifest.Dependencies.Length != 0)
            throw new InvalidDataException("Only PostgreSQL incremental manifests may declare restore-chain dependencies.");
        foreach (var artifact in manifest.Artifacts)
        {
            if (artifact.Length < 0 || artifact.Sha256.Length != 64
                || !artifact.Sha256.All(Uri.IsHexDigit))
                throw new InvalidDataException("A manifest artifact digest is invalid.");
        }
    }
}

public sealed class DatabaseRecoveryRunStatsCollector : IDatabaseRecoveryRunStatsCollector
{
    public DatabaseRecoveryRunStatistics Complete(
        DatabaseRecoveryRunStatistics? nativeStatistics,
        DatabaseEngine engine,
        DatabaseRecoveryPhase phase,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc,
        long bytes,
        int artifactCount)
    {
        if (completedUtc < startedUtc) throw new ArgumentOutOfRangeException(nameof(completedUtc));
        var elapsed = completedUtc - startedUtc;
        return (nativeStatistics ?? new DatabaseRecoveryRunStatistics()) with
        {
            Engine = engine,
            Phase = phase,
            StartedUtc = nativeStatistics?.StartedUtc ?? startedUtc,
            CompletedUtc = completedUtc,
            Elapsed = elapsed,
            SourceBytes = nativeStatistics?.SourceBytes ?? bytes,
            StoredBytes = bytes,
            ArtifactCount = artifactCount,
            AverageThroughputBytesPerSecond = elapsed.TotalSeconds <= 0 ? null : bytes / elapsed.TotalSeconds
        };
    }
}
