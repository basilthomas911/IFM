using System.Numerics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;
using TomasAI.IFM.Application.DatabaseBackup.Policies;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.PostgreSql;

public sealed record PostgreSqlWalArchiveRequest(
    DatabaseProtectionSetId ProtectionSetId,
    string Timeline,
    string SegmentName,
    long Length,
    string Sha256,
    DateTimeOffset SourceCompletedUtc);

public sealed record PostgreSqlWalArchiveRecord
{
    public int SchemaVersion { get; init; } = 1;
    public required DatabaseProtectionSetId ProtectionSetId { get; init; }
    public required string Timeline { get; init; }
    public required string SegmentName { get; init; }
    public required AwsImmutableObjectVersion Object { get; init; }
    public required DateTimeOffset SourceCompletedUtc { get; init; }
    public required DateTimeOffset ArchivedUtc { get; init; }
}

public sealed record PostgreSqlWalContinuityStatus(
    string Timeline,
    string FirstSegment,
    string LastSegment,
    int SegmentCount,
    bool Contiguous,
    string[] MissingSegments,
    TimeSpan ArchiveLag);

public sealed class AwsPostgreSqlWalArchive(
    IAmazonS3 s3,
    S3ImmutableObjectStore objects,
    IAwsDocumentSignatureService signatures,
    AwsCloudDatabaseBackupOptions options,
    TimeProvider timeProvider,
    AwsVaultLocation? vault = null)
{
    static readonly Regex SegmentPattern = new("^[0-9A-F]{24}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    static readonly Regex HistoryPattern = new("^[0-9A-F]{8}\\.history$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    readonly AwsBackupObjectKeyFactory _keys = new(options.Environment.ToString().ToLowerInvariant());
    readonly string _bucketName = vault?.BucketName ?? options.PrimaryBucketName;
    readonly string _region = vault?.Region ?? options.PrimaryRegion;
    readonly string _encryptionKeyArn = vault?.EncryptionKeyArn ?? options.PrimaryEncryptionKeyArn;

    public async ValueTask<PostgreSqlWalArchiveRecord> PublishAsync(
        PostgreSqlWalArchiveRequest request,
        Func<CancellationToken, ValueTask<Stream>> openSource,
        CancellationToken cancellationToken)
    {
        Validate(request);
        var existing = await TryReadRecordAsync(request, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (existing.Object.Length != request.Length
                || !StringComparer.OrdinalIgnoreCase.Equals(existing.Object.Sha256, request.Sha256))
                throw new InvalidDataException("A PostgreSQL WAL identity was replayed with different content.");
            await objects.VerifyAsync(existing.Object, cancellationToken).ConfigureAwait(false);
            return existing;
        }

        var context = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["application"] = "IFM", ["component"] = "DatabaseBackup",
            ["protectionSetId"] = request.ProtectionSetId.Value,
            ["timeline"] = request.Timeline, ["walSegment"] = request.SegmentName
        };
        var retention = timeProvider.GetUtcNow().AddDays(options.DefaultRetentionDays);
        var version = await objects.UploadAsync(
            _keys.Wal(request.ProtectionSetId, request.Timeline, request.SegmentName),
            openSource, request.Length, retention, context, cancellationToken).ConfigureAwait(false);
        if (!StringComparer.OrdinalIgnoreCase.Equals(version.Sha256, request.Sha256))
            throw new InvalidDataException("The archived PostgreSQL WAL digest differs from the declared source digest.");
        var record = new PostgreSqlWalArchiveRecord
        {
            ProtectionSetId = request.ProtectionSetId, Timeline = request.Timeline,
            SegmentName = request.SegmentName, Object = version,
            SourceCompletedUtc = request.SourceCompletedUtc.ToUniversalTime(), ArchivedUtc = timeProvider.GetUtcNow()
        };
        var bytes = DatabaseBackupCanonicalJson.Serialize(record);
        var signature = await signatures.SignAsync(bytes, cancellationToken).ConfigureAwait(false);
        await UploadDocumentAsync(_keys.WalRecord(request.ProtectionSetId, request.Timeline, request.SegmentName),
            bytes, retention, context, cancellationToken).ConfigureAwait(false);
        await UploadDocumentAsync(_keys.WalRecordSignature(request.ProtectionSetId, request.Timeline, request.SegmentName),
            DatabaseBackupCanonicalJson.Serialize(signature), retention, context, cancellationToken).ConfigureAwait(false);
        return record;
    }

    public async ValueTask<PostgreSqlWalContinuityStatus> InspectContinuityAsync(
        DatabaseProtectionSetId protectionSetId, string timeline, CancellationToken cancellationToken)
    {
        ValidateTimeline(timeline);
        var records = await EnumerateRecordsAsync(protectionSetId, timeline, cancellationToken).ConfigureAwait(false);
        var segments = records.Where(static record => SegmentPattern.IsMatch(record.SegmentName))
            .OrderBy(static record => SegmentOrdinal(record.SegmentName)).ToArray();
        if (segments.Length == 0)
            return new PostgreSqlWalContinuityStatus(timeline, string.Empty, string.Empty, 0, false, [], TimeSpan.MaxValue);
        var missing = new List<string>();
        for (var index = 1; index < segments.Length; index++)
        {
            var previous = SegmentOrdinal(segments[index - 1].SegmentName);
            var current = SegmentOrdinal(segments[index].SegmentName);
            for (var value = previous + 1; value < current && missing.Count < 1024; value++)
                missing.Add(FormatSegment(timeline, value));
        }
        return new PostgreSqlWalContinuityStatus(
            timeline, segments[0].SegmentName, segments[^1].SegmentName, segments.Length,
            missing.Count == 0, [.. missing], timeProvider.GetUtcNow() - segments.Max(static record => record.SourceCompletedUtc));
    }

    public async ValueTask<IReadOnlyList<PostgreSqlWalArchiveRecord>> EnumerateRecordsAsync(
        DatabaseProtectionSetId protectionSetId, string timeline, CancellationToken cancellationToken)
    {
        ValidateTimeline(timeline);
        var prefix = $"v1/environment/{options.Environment.ToString().ToLowerInvariant()}/protection-set/{protectionSetId.Value}/postgresql/timeline/{timeline}/wal-index/";
        var result = new List<PostgreSqlWalArchiveRecord>();
        string? marker = null;
        do
        {
            var response = await s3.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucketName, Prefix = prefix, ContinuationToken = marker
            }, cancellationToken).ConfigureAwait(false);
            foreach (var item in response.S3Objects ?? [])
            {
                if (item.Key?.EndsWith("/record-v1.json", StringComparison.Ordinal) != true) continue;
                var versions = await s3.ListVersionsAsync(new ListVersionsRequest
                {
                    BucketName = _bucketName, Prefix = item.Key, MaxKeys = 2
                }, cancellationToken).ConfigureAwait(false);
                var exact = (versions.Versions ?? []).Where(version => version.IsDeleteMarker != true
                    && StringComparer.Ordinal.Equals(version.Key, item.Key)).ToArray();
                if (exact.Length != 1) throw new InvalidDataException("A PostgreSQL WAL record is version-ambiguous.");
                result.Add(await ReadRecordAsync(item.Key, exact[0].VersionId!, cancellationToken).ConfigureAwait(false));
            }
            marker = response.NextContinuationToken;
        } while (marker is not null);
        return result;
    }

    async ValueTask<PostgreSqlWalArchiveRecord?> TryReadRecordAsync(
        PostgreSqlWalArchiveRequest request, CancellationToken cancellationToken)
    {
        var key = _keys.WalRecord(request.ProtectionSetId, request.Timeline, request.SegmentName).Value;
        var response = await s3.ListVersionsAsync(new ListVersionsRequest
        {
            BucketName = _bucketName, Prefix = key, MaxKeys = 2
        }, cancellationToken).ConfigureAwait(false);
        var exact = (response.Versions ?? []).Where(version => version.IsDeleteMarker != true
            && StringComparer.Ordinal.Equals(version.Key, key)).ToArray();
        if (exact.Length == 0) return null;
        if (exact.Length != 1) throw new InvalidDataException("A PostgreSQL WAL archive record is version-ambiguous.");
        return await ReadRecordAsync(key, exact[0].VersionId!, cancellationToken).ConfigureAwait(false);
    }

    async ValueTask<PostgreSqlWalArchiveRecord> ReadRecordAsync(
        string key, string versionId, CancellationToken cancellationToken)
    {
        using var response = await s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _bucketName, Key = key, VersionId = versionId
        }, cancellationToken).ConfigureAwait(false);
        if (response.ContentLength > options.MaximumSignedDocumentBytes)
            throw new InvalidDataException("A PostgreSQL WAL record exceeds its document bound.");
        using var content = new MemoryStream();
        await response.ResponseStream.CopyToAsync(content, cancellationToken).ConfigureAwait(false);
        var signatureKey = key.Replace("record-v1.json", "record-v1.signature.json", StringComparison.Ordinal);
        var signaturesResponse = await s3.ListVersionsAsync(new ListVersionsRequest
        {
            BucketName = _bucketName, Prefix = signatureKey, MaxKeys = 2
        }, cancellationToken).ConfigureAwait(false);
        var exact = (signaturesResponse.Versions ?? []).Where(version => version.IsDeleteMarker != true
            && StringComparer.Ordinal.Equals(version.Key, signatureKey)).ToArray();
        if (exact.Length != 1) throw new InvalidDataException("A PostgreSQL WAL signature is missing or version-ambiguous.");
        using var signatureResponse = await s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _bucketName, Key = signatureKey, VersionId = exact[0].VersionId
        }, cancellationToken).ConfigureAwait(false);
        using var signatureContent = new MemoryStream();
        await signatureResponse.ResponseStream.CopyToAsync(signatureContent, cancellationToken).ConfigureAwait(false);
        await signatures.VerifyAsync(content.ToArray(),
            DatabaseBackupCanonicalJson.Deserialize<AwsSignatureEnvelope>(signatureContent.ToArray()), cancellationToken).ConfigureAwait(false);
        var record = DatabaseBackupCanonicalJson.Deserialize<PostgreSqlWalArchiveRecord>(content.ToArray());
        return record with
        {
            Object = record.Object with
            {
                BucketName = _bucketName, Region = _region, EncryptionKeyArn = _encryptionKeyArn
            }
        };
    }

    async ValueTask UploadDocumentAsync(
        AwsGeneratedObjectKey key, byte[] content, DateTimeOffset retention,
        IReadOnlyDictionary<string, string> context, CancellationToken cancellationToken)
    {
        _ = await objects.UploadAsync(key,
            _ => ValueTask.FromResult<Stream>(new MemoryStream(content, writable: false)),
            content.LongLength, retention, context, cancellationToken).ConfigureAwait(false);
    }

    static void Validate(PostgreSqlWalArchiveRequest request)
    {
        ValidateTimeline(request.Timeline);
        if ((!SegmentPattern.IsMatch(request.SegmentName) && !HistoryPattern.IsMatch(request.SegmentName))
            || request.Length <= 0 || request.Sha256.Length != 64 || !request.Sha256.All(Uri.IsHexDigit)
            || request.SourceCompletedUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("The PostgreSQL WAL archive request is invalid.", nameof(request));
        if (SegmentPattern.IsMatch(request.SegmentName)
            && !request.SegmentName.StartsWith(request.Timeline, StringComparison.Ordinal))
            throw new ArgumentException("The PostgreSQL WAL segment does not belong to its declared timeline.", nameof(request));
    }

    static void ValidateTimeline(string timeline)
    {
        if (timeline.Length != 8 || !timeline.All(Uri.IsHexDigit) || timeline != timeline.ToUpperInvariant())
            throw new ArgumentException("The PostgreSQL timeline must be eight uppercase hexadecimal characters.", nameof(timeline));
    }

    static BigInteger SegmentOrdinal(string segmentName)
        => BigInteger.Parse("0" + segmentName[8..], System.Globalization.NumberStyles.HexNumber);

    static string FormatSegment(string timeline, BigInteger ordinal)
        => timeline + ordinal.ToString("X16");
}

public sealed class AwsPostgreSqlWalSpool(AwsCloudDatabaseBackupOptions options)
{
    public string Enqueue(string sourcePath, string segmentName)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("The PostgreSQL WAL source file does not exist.", sourcePath);
        if (!Regex.IsMatch(segmentName, "^(?:[0-9A-F]{24}|[0-9A-F]{8}\\.history)$", RegexOptions.CultureInvariant))
            throw new ArgumentException("The PostgreSQL WAL spool identity is invalid.", nameof(segmentName));
        var root = Path.GetFullPath(options.WalSpoolPath);
        Directory.CreateDirectory(root);
        var used = Directory.EnumerateFiles(root).Sum(static path => new FileInfo(path).Length);
        var length = new FileInfo(sourcePath).Length;
        if (used + length > options.MaximumWalSpoolBytes)
            throw new IOException("The bounded PostgreSQL WAL spool is full; required WAL was not discarded.");
        var target = Path.Combine(root, segmentName);
        if (File.Exists(target))
        {
            if (new FileInfo(target).Length != length || !FilesMatch(sourcePath, target))
                throw new InvalidDataException("A PostgreSQL WAL spool identity was replayed with different content.");
            return target;
        }
        File.Copy(sourcePath, target, overwrite: false);
        return target;
    }

    static bool FilesMatch(string firstPath, string secondPath)
    {
        using var first = File.OpenRead(firstPath);
        using var second = File.OpenRead(secondPath);
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(first), SHA256.HashData(second));
    }
}
