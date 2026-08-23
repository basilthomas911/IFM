using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.DatabaseBackup.Policies;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;

public sealed class S3DatabaseBackupCatalog(
    IAmazonS3 s3,
    S3ImmutableObjectStore objects,
    IAwsDocumentSignatureService signatures,
    AwsCloudDatabaseBackupOptions options,
    AwsVaultLocation? vault = null) : IDatabaseBackupCatalog
{
    readonly string _bucketName = vault?.BucketName ?? options.PrimaryBucketName;
    readonly string _region = vault?.Region ?? options.PrimaryRegion;
    readonly string _encryptionKeyArn = vault?.EncryptionKeyArn ?? options.PrimaryEncryptionKeyArn;
    readonly DatabaseArtifactReplicaId _replicaId = vault?.ReplicaId ?? new DatabaseArtifactReplicaId("aws-primary");
    readonly string _catalogPrefix = $"v1/environment/{options.Environment.ToString().ToLowerInvariant()}/catalog/restore-point/";
    readonly string _environmentPrefix = $"v1/environment/{options.Environment.ToString().ToLowerInvariant()}/protection-set/";

    public async ValueTask<DatabaseCatalogRestorePoint> ResolveAsync(
        DatabaseRestorePointId restorePointId, DatabaseArtifactReplicaId replicaId,
        CancellationToken cancellationToken)
    {
        var all = await EnumerateAsync(replicaId, cancellationToken).ConfigureAwait(false);
        return all.SingleOrDefault(value => value.Entry.RestorePointId == restorePointId)
            ?? throw new FileNotFoundException("The AWS restore point is not catalog-visible on the requested replica.");
    }

    public async ValueTask<IReadOnlyList<DatabaseCatalogRestorePoint>> EnumerateAsync(
        DatabaseArtifactReplicaId replicaId, CancellationToken cancellationToken)
        => (await EnumerateAwsAsync(replicaId, cancellationToken).ConfigureAwait(false))
            .Select(static value => value.RestorePoint).ToArray();

    public async ValueTask<IReadOnlyList<AwsResolvedCatalogRestorePoint>> EnumerateAwsAsync(
        DatabaseArtifactReplicaId replicaId, CancellationToken cancellationToken)
    {
        var result = new List<AwsResolvedCatalogRestorePoint>();
        foreach (var version in await ListImmutableVersionsAsync(_catalogPrefix, "/catalog-entry-v1.json", cancellationToken).ConfigureAwait(false))
        {
            var bytes = await ReadVersionAsync(version.Key!, version.VersionId!, options.MaximumSignedDocumentBytes, cancellationToken).ConfigureAwait(false);
            var entry = MapEntry(DatabaseBackupCanonicalJson.Deserialize<AwsCatalogEntry>(bytes));
            if (entry.ReplicaId != replicaId) continue;
            var catalogObject = await DescribeVersionAsync(version.Key!, version.VersionId!, bytes, cancellationToken).ConfigureAwait(false);
            result.Add(await ResolveEntryAsync(entry, cancellationToken, MapObject(catalogObject)).ConfigureAwait(false));
        }
        return result.OrderBy(static value => value.Entry.PublishedUtc)
            .ThenBy(static value => value.Entry.RestorePointId.Value, StringComparer.Ordinal).ToArray();
    }

    /// <summary>Rebuilds logical catalog content solely from signed immutable publication records.</summary>
    public async ValueTask<IReadOnlyList<DatabaseCatalogRestorePoint>> RebuildAsync(CancellationToken cancellationToken)
    {
        var result = new List<DatabaseCatalogRestorePoint>();
        foreach (var version in await ListImmutableVersionsAsync(_environmentPrefix, "/publication-v1.json", cancellationToken).ConfigureAwait(false))
        {
            var recordBytes = await ReadVersionAsync(version.Key!, version.VersionId!, options.MaximumSignedDocumentBytes, cancellationToken).ConfigureAwait(false);
            var record = DatabaseBackupCanonicalJson.Deserialize<AwsPublicationRecord>(recordBytes);
            var signatureKey = version.Key![..^"publication-v1.json".Length] + "publication-v1.signature.json";
            var signatureBytes = await ReadOnlyVersionForKeyAsync(signatureKey, cancellationToken).ConfigureAwait(false);
            var signature = DatabaseBackupCanonicalJson.Deserialize<AwsSignatureEnvelope>(signatureBytes);
            await signatures.VerifyAsync(recordBytes, signature, cancellationToken).ConfigureAwait(false);
            var descriptor = await DescribeVersionAsync(version.Key, version.VersionId!, recordBytes, cancellationToken).ConfigureAwait(false);
            var entry = new AwsCatalogEntry
            {
                RestorePointId = record.RestorePointId, ReplicaId = record.ReplicaId,
                ProtectionSetId = record.ProtectionSetId, Engine = record.Engine,
                PublicationRecord = descriptor,
                PublicationRecordSha256 = Convert.ToHexString(SHA256.HashData(recordBytes)),
                PublishedUtc = record.PublishedUtc
            };
            result.Add((await ResolveEntryAsync(entry, cancellationToken).ConfigureAwait(false)).RestorePoint);
        }
        return result.OrderBy(static value => value.Entry.PublishedUtc).ThenBy(static value => value.Entry.RestorePointId.Value, StringComparer.Ordinal).ToArray();
    }

    public async ValueTask<AwsResolvedCatalogRestorePoint> ResolveAwsAsync(
        DatabaseRestorePointId restorePointId, DatabaseArtifactReplicaId replicaId,
        CancellationToken cancellationToken)
    {
        foreach (var version in await ListImmutableVersionsAsync(_catalogPrefix, "/catalog-entry-v1.json", cancellationToken).ConfigureAwait(false))
        {
            var bytes = await ReadVersionAsync(version.Key!, version.VersionId!, options.MaximumSignedDocumentBytes, cancellationToken).ConfigureAwait(false);
            var entry = MapEntry(DatabaseBackupCanonicalJson.Deserialize<AwsCatalogEntry>(bytes));
            if (entry.RestorePointId == restorePointId && entry.ReplicaId == replicaId)
            {
                var catalogObject = await DescribeVersionAsync(version.Key!, version.VersionId!, bytes, cancellationToken).ConfigureAwait(false);
                return await ResolveEntryAsync(entry, cancellationToken, MapObject(catalogObject)).ConfigureAwait(false);
            }
        }
        throw new FileNotFoundException("The AWS restore point is not catalog-visible on the requested replica.");
    }

    async ValueTask<AwsResolvedCatalogRestorePoint> ResolveEntryAsync(
        AwsCatalogEntry entry,
        CancellationToken cancellationToken,
        AwsImmutableObjectVersion? catalogObject = null)
    {
        entry = MapEntry(entry);
        var recordBytes = await objects.DownloadBoundedAsync(
            entry.PublicationRecord, options.MaximumSignedDocumentBytes, cancellationToken).ConfigureAwait(false);
        if (!CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(recordBytes), Convert.FromHexString(entry.PublicationRecordSha256)))
            throw new InvalidDataException("The AWS catalog publication-record digest is invalid.");
        var record = DatabaseBackupCanonicalJson.Deserialize<AwsPublicationRecord>(recordBytes);
        if (record.RestorePointId != entry.RestorePointId
            || record.Engine != entry.Engine || record.ProtectionSetId != entry.ProtectionSetId)
            throw new InvalidDataException("The AWS catalog entry does not identify its signed publication record.");
        var signatureKey = record.EngineManifest.ObjectKey.Replace(
            "manifests/engine-manifest-v2.json", $"publications/{record.ReplicaId.Value}/publication-v1.signature.json",
            StringComparison.Ordinal);
        var publicationSignature = await ReadOnlyObjectForKeyAsync(signatureKey, cancellationToken).ConfigureAwait(false);
        var signatureBytes = publicationSignature.Content;
        await signatures.VerifyAsync(recordBytes,
            DatabaseBackupCanonicalJson.Deserialize<AwsSignatureEnvelope>(signatureBytes), cancellationToken).ConfigureAwait(false);
        record = MapRecord(record);

        var manifestBytes = await objects.DownloadBoundedAsync(
            record.EngineManifest, options.MaximumSignedDocumentBytes, cancellationToken).ConfigureAwait(false);
        if (!CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(manifestBytes), Convert.FromHexString(record.EngineManifestSha256)))
            throw new InvalidDataException("The AWS engine-manifest digest is invalid.");
        await signatures.VerifyAsync(manifestBytes, record.EngineManifestSignature, cancellationToken).ConfigureAwait(false);
        var manifestSignatureKey = record.EngineManifest.ObjectKey.Replace(
            "engine-manifest-v2.json", "engine-manifest-v2.signature.json", StringComparison.Ordinal);
        var manifestSignature = await ReadOnlyObjectForKeyAsync(manifestSignatureKey, cancellationToken).ConfigureAwait(false);
        var manifest = DatabaseBackupCanonicalJson.Deserialize<DatabaseBackupManifest>(manifestBytes);
        DatabaseBackupManifestPolicy.Validate(manifest, BackupSource.AwsCloud);
        if (manifest.RestorePointId != entry.RestorePointId || manifest.Engine != entry.Engine)
            throw new InvalidDataException("The AWS engine manifest does not match its catalog identity.");

        foreach (var artifact in record.Artifacts)
            await objects.VerifyAsync(artifact.Object, cancellationToken).ConfigureAwait(false);
        await objects.VerifyAsync(publicationSignature.Object, cancellationToken).ConfigureAwait(false);
        await objects.VerifyAsync(manifestSignature.Object, cancellationToken).ConfigureAwait(false);
        if (catalogObject is not null) await objects.VerifyAsync(catalogObject, cancellationToken).ConfigureAwait(false);
        var applicationEntry = new DatabaseCatalogEntry(
            entry.SchemaVersion, entry.RestorePointId,
            manifest.ManifestId, manifest.Revision, entry.Engine, entry.ProtectionSetId, entry.ReplicaId,
            record.EngineManifest.ObjectKey, entry.PublicationRecord.ObjectKey, entry.PublishedUtc);
        var restorePoint = new DatabaseCatalogRestorePoint(applicationEntry, manifest,
            record.Artifacts.Sum(static value => value.Object.Length), record.Artifacts.Length);
        var immutableObjects = record.Artifacts.Select(static artifact => artifact.Object)
            .Append(record.EngineManifest)
            .Append(entry.PublicationRecord)
            .Append(publicationSignature.Object)
            .Append(manifestSignature.Object)
            .Concat(catalogObject is null ? [] : [catalogObject])
            .OrderBy(static value => value.ObjectKey, StringComparer.Ordinal)
            .ToArray();
        return new AwsResolvedCatalogRestorePoint(entry, record, restorePoint, immutableObjects);
    }

    async Task<List<S3ObjectVersion>> ListImmutableVersionsAsync(
        string prefix, string suffix, CancellationToken cancellationToken)
    {
        var result = new List<S3ObjectVersion>();
        string? keyMarker = null;
        string? versionMarker = null;
        do
        {
            var response = await s3.ListVersionsAsync(new ListVersionsRequest
            {
                BucketName = _bucketName, Prefix = prefix,
                KeyMarker = keyMarker, VersionIdMarker = versionMarker
            }, cancellationToken).ConfigureAwait(false);
            result.AddRange((response.Versions ?? []).Where(value => value.IsDeleteMarker != true
                && value.Key?.EndsWith(suffix, StringComparison.Ordinal) == true));
            if (response.IsTruncated != true) break;
            keyMarker = response.NextKeyMarker;
            versionMarker = response.NextVersionIdMarker;
        } while (true);
        var duplicates = result.GroupBy(static value => value.Key, StringComparer.Ordinal).Where(static group => group.Count() != 1).ToArray();
        if (duplicates.Length > 0) throw new InvalidDataException("An immutable AWS publication key has multiple object versions.");
        return result;
    }

    async Task<byte[]> ReadOnlyVersionForKeyAsync(string key, CancellationToken cancellationToken)
        => (await ReadOnlyObjectForKeyAsync(key, cancellationToken).ConfigureAwait(false)).Content;

    async Task<ImmutableDocument> ReadOnlyObjectForKeyAsync(string key, CancellationToken cancellationToken)
    {
        var versions = await ListImmutableVersionsAsync(key, key, cancellationToken).ConfigureAwait(false);
        var exact = versions.Where(value => StringComparer.Ordinal.Equals(value.Key, key)).ToArray();
        if (exact.Length != 1) throw new InvalidDataException("A signed AWS publication sidecar is missing or version-ambiguous.");
        var content = await ReadVersionAsync(key, exact[0].VersionId!, options.MaximumSignedDocumentBytes, cancellationToken).ConfigureAwait(false);
        var descriptor = MapObject(await DescribeVersionAsync(
            key, exact[0].VersionId!, content, cancellationToken).ConfigureAwait(false));
        return new ImmutableDocument(content, descriptor);
    }

    async Task<byte[]> ReadVersionAsync(string key, string versionId, int maximumBytes, CancellationToken cancellationToken)
    {
        using var response = await s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _bucketName, Key = key, VersionId = versionId
        }, cancellationToken).ConfigureAwait(false);
        if (response.ContentLength > maximumBytes) throw new InvalidDataException("An AWS signed document exceeds its configured bound.");
        using var target = new MemoryStream();
        await response.ResponseStream.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        return target.ToArray();
    }

    async Task<AwsImmutableObjectVersion> DescribeVersionAsync(
        string key, string versionId, byte[] content, CancellationToken cancellationToken)
    {
        var metadata = await s3.GetObjectMetadataAsync(new GetObjectMetadataRequest
        {
            BucketName = _bucketName, Key = key, VersionId = versionId, ChecksumMode = ChecksumMode.ENABLED
        }, cancellationToken).ConfigureAwait(false);
        return new AwsImmutableObjectVersion
        {
            BucketName = _bucketName, Region = _region, ObjectKey = key,
            VersionId = versionId, Length = content.LongLength, Sha256 = Convert.ToHexString(SHA256.HashData(content)),
            S3ChecksumSha256 = metadata.ChecksumSHA256 ?? throw new InvalidDataException("The AWS publication lacks an S3 checksum."),
            EncryptionKeyArn = _encryptionKeyArn,
            EncryptionContextBase64 = string.Empty,
            ObjectLockMode = metadata.ObjectLockMode?.Value ?? string.Empty,
            RetainUntilUtc = metadata.ObjectLockRetainUntilDate ?? throw new InvalidDataException("The AWS publication lacks retention."),
            PublishedUtc = metadata.LastModified ?? DateTimeOffset.UtcNow
        };
    }

    AwsCatalogEntry MapEntry(AwsCatalogEntry entry) => entry with
    {
        ReplicaId = _replicaId,
        PublicationRecord = MapObject(entry.PublicationRecord)
    };

    AwsPublicationRecord MapRecord(AwsPublicationRecord record) => record with
    {
        ReplicaId = _replicaId,
        EngineManifest = MapObject(record.EngineManifest),
        Artifacts = record.Artifacts.Select(value => value with { Object = MapObject(value.Object) }).ToArray()
    };

    AwsImmutableObjectVersion MapObject(AwsImmutableObjectVersion value) => value with
    {
        BucketName = _bucketName,
        Region = _region,
        EncryptionKeyArn = _encryptionKeyArn
    };

    sealed record ImmutableDocument(byte[] Content, AwsImmutableObjectVersion Object);
}
