using Amazon.S3;
using Amazon.S3.Model;
using System.Security.Cryptography;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.PostgreSql;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Startup;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;

public sealed class S3DatabaseRestoreSourceCapability(
    IAmazonS3 s3,
    S3DatabaseBackupCatalog catalog,
    IDatabaseNativeRestoreArtifactSink sink,
    AwsPostgreSqlWalArchive walArchive,
    AwsCloudDatabaseBackupOptions options,
    AwsRecoveryVaultClient recoveryVault,
    IAwsDocumentSignatureService signatures,
    TimeProvider timeProvider) : IDatabaseRestoreSourceCapability
{
    static readonly DatabaseArtifactReplicaId PrimaryReplica = new("aws-primary");
    static readonly DatabaseArtifactReplicaId RecoveryReplica = new("aws-recovery");

    public async ValueTask<DatabasePreparedRestoreSource> PrepareAsync(
        DatabaseRestoreSourceRequest request, CancellationToken cancellationToken)
    {
        if (request.OperationId.Value == Guid.Empty || string.IsNullOrWhiteSpace(request.RestorePointId.Value))
            throw new ArgumentException("AWS restore preparation requires an operation and restore point.", nameof(request));
        DatabaseBackupEnumValidation.RequireDefined(request.Engine, nameof(request.Engine));
        var replica = request.PreferredReplicaId ?? PrimaryReplica;
        if (replica != PrimaryReplica && replica != RecoveryReplica)
            throw new InvalidOperationException("The requested AWS restore replica is not configured on this processor.");
        var context = replica == PrimaryReplica
            ? new RestoreVaultContext(s3, catalog, walArchive)
            : CreateRecoveryContext();

        var ordered = new List<AwsResolvedCatalogRestorePoint>();
        var visiting = new HashSet<DatabaseRestorePointId>();
        var visited = new HashSet<DatabaseRestorePointId>();
        await VisitAsync(context, request.RestorePointId, replica, request.Engine, visiting, visited, ordered, cancellationToken)
            .ConfigureAwait(false);
        if (ordered.Count > 32) throw new InvalidDataException("The AWS restore dependency chain exceeds its safety bound.");
        foreach (var point in ordered)
            await StageAsync(context.S3, point, cancellationToken).ConfigureAwait(false);
        var selected = ordered[^1];
        var dependencies = ordered.Take(ordered.Count - 1).Select(static point => point.RestorePoint.Entry.RestorePointId).ToArray();
        var recovery = request.PostgreSqlRecoveryTargetUtc is { } target
            ? await StageWalAsync(context, request, selected, target, cancellationToken).ConfigureAwait(false)
            : null;
        return new DatabasePreparedRestoreSource(
            selected.RestorePoint.Entry.RestorePointId, replica,
            selected.RestorePoint.Manifest.ManifestId, selected.RestorePoint.Manifest.Revision,
            ordered.Sum(static point => point.RestorePoint.VerifiedBytes),
            ordered.Sum(static point => point.RestorePoint.VerifiedArtifactCount), dependencies, recovery);
    }

    async ValueTask VisitAsync(
        RestoreVaultContext context,
        DatabaseRestorePointId restorePointId, DatabaseArtifactReplicaId replicaId, DatabaseEngine engine,
        HashSet<DatabaseRestorePointId> visiting, HashSet<DatabaseRestorePointId> visited,
        List<AwsResolvedCatalogRestorePoint> ordered, CancellationToken cancellationToken)
    {
        if (visited.Contains(restorePointId)) return;
        if (!visiting.Add(restorePointId)) throw new InvalidDataException("The AWS restore dependency graph contains a cycle.");
        var point = await context.Catalog.ResolveAwsAsync(restorePointId, replicaId, cancellationToken).ConfigureAwait(false);
        if (point.RestorePoint.Manifest.Engine != engine)
            throw new InvalidDataException("An AWS restore dependency uses the wrong database engine.");
        foreach (var dependency in point.RestorePoint.Manifest.Dependencies)
            await VisitAsync(context, dependency, replicaId, engine, visiting, visited, ordered, cancellationToken).ConfigureAwait(false);
        visiting.Remove(restorePointId);
        visited.Add(restorePointId);
        ordered.Add(point);
    }

    async ValueTask StageAsync(IAmazonS3 vaultS3, AwsResolvedCatalogRestorePoint point, CancellationToken cancellationToken)
    {
        var restorePointId = point.RestorePoint.Entry.RestorePointId;
        var engine = point.RestorePoint.Manifest.Engine;
        await sink.PrepareFreshAsync(engine, restorePointId, cancellationToken).ConfigureAwait(false);
        foreach (var artifact in point.Publication.Artifacts.OrderBy(static value => value.LogicalRelativePath, StringComparer.Ordinal))
        {
            using var response = await vaultS3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = artifact.Object.BucketName, Key = artifact.Object.ObjectKey,
                VersionId = artifact.Object.VersionId, ChecksumMode = ChecksumMode.ENABLED
            }, cancellationToken).ConfigureAwait(false);
            await sink.WriteAsync(engine, restorePointId, artifact.LogicalRelativePath,
                response.ResponseStream, artifact.Object.Length, artifact.Object.Sha256, cancellationToken).ConfigureAwait(false);
        }
    }

    async ValueTask<PostgreSqlPreparedRecovery> StageWalAsync(
        RestoreVaultContext context,
        DatabaseRestoreSourceRequest request,
        AwsResolvedCatalogRestorePoint selected,
        DateTimeOffset targetUtc,
        CancellationToken cancellationToken)
    {
        if (request.Engine != DatabaseEngine.PostgreSql || targetUtc.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("A PITR request requires PostgreSQL and a UTC recovery target.");
        var timeline = selected.Publication.PostgreSqlTimeline;
        if (string.IsNullOrWhiteSpace(timeline))
            throw new InvalidDataException("The selected PostgreSQL publication has no WAL timeline evidence.");
        if (targetUtc < selected.RestorePoint.Manifest.CreatedUtc)
            throw new InvalidOperationException("The PostgreSQL PITR target precedes the selected backup.");
        var continuity = await context.WalArchive.InspectContinuityAsync(
            selected.RestorePoint.Entry.ProtectionSetId, timeline, cancellationToken).ConfigureAwait(false);
        if (!continuity.Contiguous)
            throw new InvalidDataException("The PostgreSQL WAL archive has a gap and is ineligible for PITR.");
        var records = (await context.WalArchive.EnumerateRecordsAsync(
            selected.RestorePoint.Entry.ProtectionSetId, timeline, cancellationToken).ConfigureAwait(false))
            .Where(static value => value.SegmentName.Length == 24)
            .OrderBy(static value => value.SegmentName, StringComparer.Ordinal).ToArray();
        var lastRequired = Array.FindIndex(records, value => value.SourceCompletedUtc >= targetUtc);
        if (lastRequired < 0)
            throw new InvalidDataException("The PostgreSQL WAL archive does not reach the requested PITR target.");
        var required = records.Take(lastRequired + 1).ToArray();
        var root = Path.GetFullPath(Path.Combine(options.WalSpoolPath, "restore", request.OperationId.Format()));
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            throw new InvalidOperationException("The PostgreSQL WAL restore staging target is not fresh.");
        Directory.CreateDirectory(root);
        foreach (var record in required)
        {
            var target = Path.Combine(root, record.SegmentName);
            using var response = await context.S3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = record.Object.BucketName, Key = record.Object.ObjectKey,
                VersionId = record.Object.VersionId, ChecksumMode = ChecksumMode.ENABLED
            }, cancellationToken).ConfigureAwait(false);
            await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                128 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[128 * 1024];
            int read;
            while ((read = await response.ResponseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                hash.AppendData(buffer, 0, read);
            }
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (output.Length != record.Object.Length || !CryptographicOperations.FixedTimeEquals(
                    hash.GetHashAndReset(), Convert.FromHexString(record.Object.Sha256)))
                throw new InvalidDataException("A staged PostgreSQL WAL segment failed length or digest verification.");
        }
        return new PostgreSqlPreparedRecovery(targetUtc, timeline, root,
            required.Select(static value => value.SegmentName).ToArray());
    }

    RestoreVaultContext CreateRecoveryContext()
    {
        var vault = new AwsVaultLocation(
            options.RecoveryBucketName, options.RecoveryRegion, options.RecoveryEncryptionKeyArn, RecoveryReplica);
        var recoveryObjects = new S3ImmutableObjectStore(recoveryVault.Client, options, timeProvider);
        return new RestoreVaultContext(
            recoveryVault.Client,
            new S3DatabaseBackupCatalog(recoveryVault.Client, recoveryObjects, signatures, options, vault),
            new AwsPostgreSqlWalArchive(recoveryVault.Client, recoveryObjects, signatures, options, timeProvider, vault));
    }

    sealed record RestoreVaultContext(
        IAmazonS3 S3,
        S3DatabaseBackupCatalog Catalog,
        AwsPostgreSqlWalArchive WalArchive);
}
