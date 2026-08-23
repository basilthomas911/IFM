using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;

public sealed class S3ImmutableObjectStore(
    IAmazonS3 s3,
    AwsCloudDatabaseBackupOptions options,
    TimeProvider timeProvider,
    IAwsMultipartCheckpointStore? checkpoints = null)
{
    public async ValueTask<AwsImmutableObjectVersion> UploadAsync(
        AwsGeneratedObjectKey key,
        Func<CancellationToken, ValueTask<Stream>> openSource,
        long length,
        DateTimeOffset retainUntilUtc,
        IReadOnlyDictionary<string, string> encryptionContext,
        CancellationToken cancellationToken)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        if (retainUntilUtc < timeProvider.GetUtcNow().AddDays(options.DefaultRetentionDays).AddMinutes(-5))
            throw new InvalidOperationException("The immutable object retention is shorter than configured policy.");
        var context = Convert.ToBase64String(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(
            encryptionContext.OrderBy(static value => value.Key, StringComparer.Ordinal).ToDictionary())));
        await RejectExistingKeyAsync(key, cancellationToken).ConfigureAwait(false);
        await using var source = await openSource(cancellationToken).ConfigureAwait(false);
        if (!source.CanRead) throw new InvalidOperationException("The immutable object source stream is unreadable.");
        var sha256 = await HashAsync(source, cancellationToken).ConfigureAwait(false);
        if (source.CanSeek) source.Position = 0;
        else throw new InvalidOperationException("The immutable upload source must be seekable for read-back-safe publication.");

        var result = length < options.MultipartThresholdBytes
            ? await UploadSingleAsync(key, source, length, sha256, retainUntilUtc, context, cancellationToken).ConfigureAwait(false)
            : await UploadMultipartAsync(key, source, length, sha256, retainUntilUtc, context, cancellationToken).ConfigureAwait(false);
        await VerifyAsync(result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async ValueTask VerifyAsync(AwsImmutableObjectVersion expected, CancellationToken cancellationToken)
    {
        var metadata = await s3.GetObjectMetadataAsync(new GetObjectMetadataRequest
        {
            BucketName = expected.BucketName, Key = expected.ObjectKey, VersionId = expected.VersionId,
            ChecksumMode = ChecksumMode.ENABLED
        }, cancellationToken).ConfigureAwait(false);
        var mismatches = new List<string>(7);
        if (metadata.ContentLength != expected.Length) mismatches.Add("length");
        if (metadata.VersionId != expected.VersionId) mismatches.Add("version");
        if (metadata.ServerSideEncryptionMethod != ServerSideEncryptionMethod.AWSKMS) mismatches.Add("encryption");
        if (metadata.ServerSideEncryptionKeyManagementServiceKeyId != expected.EncryptionKeyArn) mismatches.Add("kms-key");
        if (!StringComparer.Ordinal.Equals(metadata.ChecksumSHA256, expected.S3ChecksumSha256)) mismatches.Add("s3-checksum");
        if (!StringComparer.OrdinalIgnoreCase.Equals(metadata.ObjectLockMode?.Value, expected.ObjectLockMode))
            mismatches.Add("object-lock-mode");
        if (metadata.ObjectLockRetainUntilDate?.ToUniversalTime() < expected.RetainUntilUtc.AddSeconds(-1))
            mismatches.Add("retain-until");
        if (mismatches.Count > 0)
            throw new InvalidDataException(
                $"The S3 immutable object metadata does not match its publication evidence: {string.Join(',', mismatches)}.");

        using var response = await s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = expected.BucketName, Key = expected.ObjectKey, VersionId = expected.VersionId,
            ChecksumMode = ChecksumMode.ENABLED
        }, cancellationToken).ConfigureAwait(false);
        var digest = await HashAsync(response.ResponseStream, cancellationToken).ConfigureAwait(false);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected.Sha256), digest))
            throw new InvalidDataException("The exact S3 object version failed IFM SHA-256 read-back verification.");
    }

    public async ValueTask<byte[]> DownloadBoundedAsync(
        AwsImmutableObjectVersion expected, int maximumBytes, CancellationToken cancellationToken)
    {
        if (expected.Length > maximumBytes) throw new InvalidDataException("The immutable AWS document exceeds its bounded size.");
        await VerifyAsync(expected, cancellationToken).ConfigureAwait(false);
        using var response = await s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = expected.BucketName, Key = expected.ObjectKey, VersionId = expected.VersionId
        }, cancellationToken).ConfigureAwait(false);
        using var destination = new MemoryStream(checked((int)expected.Length));
        await response.ResponseStream.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        return destination.ToArray();
    }

    async Task<AwsImmutableObjectVersion> UploadSingleAsync(
        AwsGeneratedObjectKey key, Stream source, long length, byte[] sha256,
        DateTimeOffset retainUntilUtc, string context, CancellationToken cancellationToken)
    {
        var checksum = Convert.ToBase64String(sha256);
        try
        {
            var response = await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = options.PrimaryBucketName, Key = key.Value, InputStream = source,
                AutoCloseStream = false,
                ChecksumSHA256 = checksum,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AWSKMS,
                ServerSideEncryptionKeyManagementServiceKeyId = options.PrimaryEncryptionKeyArn,
                ServerSideEncryptionKeyManagementServiceEncryptionContext = context,
                ObjectLockMode = LockMode(), ObjectLockRetainUntilDate = retainUntilUtc.UtcDateTime
            }, cancellationToken).ConfigureAwait(false);
            return Descriptor(key, response.VersionId, length, sha256, response.ChecksumSHA256, retainUntilUtc, context);
        }
        catch (AmazonS3Exception exception) when (IsAmbiguous(exception))
        {
            var versionId = await ResolveAmbiguousCompletionAsync(key, cancellationToken).ConfigureAwait(false);
            if (versionId is null) throw;
            return Descriptor(key, versionId, length, sha256, checksum, retainUntilUtc, context);
        }
    }

    async Task<AwsImmutableObjectVersion> UploadMultipartAsync(
        AwsGeneratedObjectKey key, Stream source, long length, byte[] sha256,
        DateTimeOffset retainUntilUtc, string context, CancellationToken cancellationToken)
    {
        var checkpoint = checkpoints is null ? null : await checkpoints.ReadAsync(key, cancellationToken).ConfigureAwait(false);
        var uploadId = checkpoint?.UploadId;
        if (string.IsNullOrWhiteSpace(uploadId))
        {
            var initiated = await s3.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
            {
                BucketName = options.PrimaryBucketName, Key = key.Value,
                ChecksumAlgorithm = ChecksumAlgorithm.SHA256,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AWSKMS,
                ServerSideEncryptionKeyManagementServiceKeyId = options.PrimaryEncryptionKeyArn,
                ServerSideEncryptionKeyManagementServiceEncryptionContext = context,
                ObjectLockMode = LockMode(), ObjectLockRetainUntilDate = retainUntilUtc.UtcDateTime
            }, cancellationToken).ConfigureAwait(false);
            uploadId = initiated.UploadId ?? throw new InvalidOperationException("S3 returned no multipart upload identity.");
            checkpoint = new AwsMultipartCheckpoint(options.PrimaryBucketName, key.Value, uploadId, 0, 0, timeProvider.GetUtcNow());
            if (checkpoints is not null) await checkpoints.WriteAsync(checkpoint, cancellationToken).ConfigureAwait(false);
        }

        var existing = await ReadPartsAsync(key, uploadId, cancellationToken).ConfigureAwait(false);
        var completed = new List<PartETag>();
        var buffer = ArrayPool<byte>.Shared.Rent(options.MultipartPartSizeBytes);
        long uploaded = 0;
        var partNumber = 1;
        try
        {
            while (uploaded < length)
            {
                var required = checked((int)Math.Min(options.MultipartPartSizeBytes, length - uploaded));
                var read = await ReadExactlyOrEofAsync(source, buffer.AsMemory(0, required), cancellationToken).ConfigureAwait(false);
                if (read != required) throw new EndOfStreamException("The immutable multipart source was truncated.");
                if (existing.TryGetValue(partNumber, out var part) && part.Size == read)
                {
                    if (string.IsNullOrWhiteSpace(part.ChecksumSHA256))
                        throw new InvalidDataException("A resumed S3 multipart part has no SHA-256 checksum.");
                    completed.Add(new PartETag(partNumber, part.ETag) { ChecksumSHA256 = part.ChecksumSHA256 });
                }
                else
                {
                    var partDigest = SHA256.HashData(buffer.AsSpan(0, read));
                    using var body = new MemoryStream(buffer, 0, read, writable: false, publiclyVisible: true);
                    var response = await s3.UploadPartAsync(new UploadPartRequest
                    {
                        BucketName = options.PrimaryBucketName, Key = key.Value, UploadId = uploadId,
                        PartNumber = partNumber, PartSize = read, InputStream = body,
                        ChecksumSHA256 = Convert.ToBase64String(partDigest)
                    }, cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(response.ChecksumSHA256))
                        throw new InvalidDataException("S3 returned no SHA-256 checksum for an uploaded multipart part.");
                    completed.Add(new PartETag(partNumber, response.ETag) { ChecksumSHA256 = response.ChecksumSHA256 });
                }
                uploaded += read;
                if (checkpoints is not null)
                    await checkpoints.WriteAsync(checkpoint! with
                    {
                        CompletedPartCount = partNumber, UploadedBytes = uploaded, UpdatedUtc = timeProvider.GetUtcNow()
                    }, cancellationToken).ConfigureAwait(false);
                partNumber++;
            }
        }
        catch
        {
            // The upload remains resumable. Reconciliation owns age-bounded abort decisions.
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }

        CompleteMultipartUploadResponse? complete = null;
        string? resolvedVersionId = null;
        try
        {
            complete = await s3.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
            {
                BucketName = options.PrimaryBucketName, Key = key.Value, UploadId = uploadId, PartETags = completed
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonS3Exception exception) when (IsAmbiguous(exception))
        {
            resolvedVersionId = await ResolveAmbiguousCompletionAsync(key, cancellationToken).ConfigureAwait(false);
            if (resolvedVersionId is null) throw;
        }
        if (checkpoints is not null) await checkpoints.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        return Descriptor(key, complete?.VersionId ?? resolvedVersionId, length, sha256,
            complete?.ChecksumSHA256 ?? Convert.ToBase64String(sha256), retainUntilUtc, context);
    }

    public async ValueTask<int> AbortStaleMultipartUploadsAsync(CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow() - options.StaleMultipartUploadAge;
        string? keyMarker = null;
        string? uploadMarker = null;
        var count = 0;
        do
        {
            var response = await s3.ListMultipartUploadsAsync(new ListMultipartUploadsRequest
            {
                BucketName = options.PrimaryBucketName, Prefix = $"v1/environment/{options.Environment.ToString().ToLowerInvariant()}/",
                KeyMarker = keyMarker, UploadIdMarker = uploadMarker
            }, cancellationToken).ConfigureAwait(false);
            foreach (var upload in response.MultipartUploads ?? [])
            {
                if (upload.Initiated is null || upload.Initiated.Value.ToUniversalTime() > cutoff || string.IsNullOrWhiteSpace(upload.Key)
                    || string.IsNullOrWhiteSpace(upload.UploadId)) continue;
                _ = new AwsGeneratedObjectKey(upload.Key);
                await s3.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
                {
                    BucketName = options.PrimaryBucketName, Key = upload.Key, UploadId = upload.UploadId
                }, cancellationToken).ConfigureAwait(false);
                count++;
            }
            keyMarker = response.NextKeyMarker;
            uploadMarker = response.NextUploadIdMarker;
            if (response.IsTruncated != true) break;
        } while (true);
        return count;
    }

    async Task<Dictionary<int, PartDetail>> ReadPartsAsync(
        AwsGeneratedObjectKey key, string uploadId, CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, PartDetail>();
        string? marker = null;
        do
        {
            var response = await s3.ListPartsAsync(new ListPartsRequest
            {
                BucketName = options.PrimaryBucketName, Key = key.Value, UploadId = uploadId, PartNumberMarker = marker
            }, cancellationToken).ConfigureAwait(false);
            foreach (var part in response.Parts ?? [])
                result[part.PartNumber ?? throw new InvalidDataException("S3 returned a multipart part without a number.")] = part;
            if (response.IsTruncated != true) break;
            marker = response.NextPartNumberMarker?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        } while (true);
        return result;
    }

    async Task RejectExistingKeyAsync(AwsGeneratedObjectKey key, CancellationToken cancellationToken)
    {
        var response = await s3.ListVersionsAsync(new ListVersionsRequest
        {
            BucketName = options.PrimaryBucketName, Prefix = key.Value, MaxKeys = 2
        }, cancellationToken).ConfigureAwait(false);
        if ((response.Versions ?? []).Any(version => StringComparer.Ordinal.Equals(version.Key, key.Value)))
            throw new InvalidOperationException("Immutable AWS publication rejects reuse of an existing object key.");
    }

    async Task<string?> ResolveAmbiguousCompletionAsync(
        AwsGeneratedObjectKey key, CancellationToken cancellationToken)
    {
        var response = await s3.ListVersionsAsync(new ListVersionsRequest
        {
            BucketName = options.PrimaryBucketName, Prefix = key.Value, MaxKeys = 2
        }, cancellationToken).ConfigureAwait(false);
        var exact = (response.Versions ?? []).Where(version => version.IsDeleteMarker != true
            && StringComparer.Ordinal.Equals(version.Key, key.Value)).ToArray();
        if (exact.Length > 1)
            throw new InvalidDataException("An ambiguous S3 completion produced multiple immutable object versions.");
        return exact.SingleOrDefault()?.VersionId;
    }

    async Task<byte[]> HashAsync(Stream source, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
        try
        {
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                hash.AppendData(buffer, 0, read);
            return hash.GetHashAndReset();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    AwsImmutableObjectVersion Descriptor(
        AwsGeneratedObjectKey key, string? versionId, long length, byte[] sha256, string? s3Checksum,
        DateTimeOffset retainUntilUtc, string context)
    {
        if (string.IsNullOrWhiteSpace(versionId)) throw new InvalidOperationException("S3 immutable publication returned no exact version ID.");
        if (string.IsNullOrWhiteSpace(s3Checksum)) throw new InvalidOperationException("S3 immutable publication returned no SHA-256 checksum.");
        return new AwsImmutableObjectVersion
        {
            BucketName = options.PrimaryBucketName, Region = options.PrimaryRegion, ObjectKey = key.Value,
            VersionId = versionId, Length = length, Sha256 = Convert.ToHexString(sha256),
            S3ChecksumSha256 = s3Checksum, EncryptionKeyArn = options.PrimaryEncryptionKeyArn,
            EncryptionContextBase64 = context, ObjectLockMode = options.ObjectLockMode,
            RetainUntilUtc = retainUntilUtc.ToUniversalTime(), PublishedUtc = timeProvider.GetUtcNow()
        };
    }

    ObjectLockMode LockMode() => options.ObjectLockMode.Equals("Compliance", StringComparison.OrdinalIgnoreCase)
        ? ObjectLockMode.Compliance : ObjectLockMode.Governance;

    static bool IsAmbiguous(AmazonS3Exception exception)
        => (int)exception.StatusCode >= 500 || exception.InnerException is TimeoutException;

    static async Task<int> ReadExactlyOrEofAsync(Stream source, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await source.ReadAsync(buffer[total..], cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            total += read;
        }
        return total;
    }
}
