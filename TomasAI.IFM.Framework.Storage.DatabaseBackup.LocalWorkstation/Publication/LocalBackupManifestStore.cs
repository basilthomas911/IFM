using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using TomasAI.IFM.Application.DatabaseBackup.Policies;

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
        => DatabaseBackupManifestPolicy.Validate(manifest, BackupSource.LocalWorkstation);
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
