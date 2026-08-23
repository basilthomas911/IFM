using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Scylla;

namespace TomasAI.IFM.Api.DatabaseBackup.Host.Services;

public sealed class S3ScyllaSnapshotArtifactTransport : IScyllaSnapshotArtifactTransport, IDisposable
{
    const string ManifestRelativePath = "portable-snapshot/manifest-v1.json";
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    readonly ScyllaPortableSnapshotOptions _options;
    readonly IAmazonS3 _s3;

    public S3ScyllaSnapshotArtifactTransport(ScyllaBackupOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.PortableSnapshot;
        _options.Validate();
        if (!_options.Enabled)
            throw new InvalidOperationException("The Scylla portable-snapshot transport is disabled.");
        var accessKey = Environment.GetEnvironmentVariable(_options.AccessKeyIdEnvironmentVariable);
        var secretKey = Environment.GetEnvironmentVariable(_options.SecretAccessKeyEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("The Scylla portable-snapshot object-store credentials are unavailable.");
        _s3 = new AmazonS3Client(
            new BasicAWSCredentials(accessKey, secretKey),
            new AmazonS3Config
            {
                ServiceURL = _options.ServiceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1"
            });
    }

    public async ValueTask<long> ExportAsync(
        string backupLocation,
        string snapshotTag,
        IReadOnlyList<string> artifactReferences,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        var bucket = ParseBackupLocation(backupLocation);
        ValidateSnapshotTag(snapshotTag);
        ArgumentNullException.ThrowIfNull(artifactReferences);
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var reference in artifactReferences)
            keys.Add(ParseReference(reference, bucket));
        await AddSnapshotMetadataKeysAsync(bucket, snapshotTag, keys, cancellationToken).ConfigureAwait(false);
        if (keys.Count == 0 || keys.Count > _options.MaximumObjectCount)
            throw new InvalidDataException("The Scylla portable snapshot has an unsafe object count.");

        var portableRoot = Path.Combine(destinationDirectory, "portable-snapshot");
        var objectRoot = Path.Combine(portableRoot, "objects");
        Directory.CreateDirectory(objectRoot);
        if (Directory.EnumerateFileSystemEntries(objectRoot).Any())
            throw new InvalidOperationException("The Scylla portable-snapshot staging directory is not empty.");
        var objects = new List<PortableSnapshotObject>(keys.Count);
        long totalBytes = 0;
        var index = 0;
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = $"portable-snapshot/objects/{index++:D7}.bin";
            var path = Path.Combine(destinationDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            using var response = await _s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = bucket,
                Key = key
            }, cancellationToken).ConfigureAwait(false);
            await using var destination = new FileStream(
                path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var (length, sha256) = await CopyAndHashAsync(
                response.ResponseStream, destination, _options.MaximumTotalBytes - totalBytes, cancellationToken)
                .ConfigureAwait(false);
            totalBytes = checked(totalBytes + length);
            objects.Add(new PortableSnapshotObject(bucket, key, relativePath, length, sha256));
        }
        var manifest = new PortableSnapshotManifest(1, backupLocation, snapshotTag, totalBytes, objects);
        var manifestPath = Path.Combine(destinationDirectory, ManifestRelativePath.Replace('/', Path.DirectorySeparatorChar));
        await File.WriteAllBytesAsync(
            manifestPath, JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions), cancellationToken).ConfigureAwait(false);
        return totalBytes;
    }

    public async ValueTask<long> EnsureAvailableAsync(
        string sourceBackupLocation,
        string destinationBackupLocation,
        string snapshotTag,
        string sourceDirectory,
        CancellationToken cancellationToken)
    {
        var sourceBucket = ParseBackupLocation(sourceBackupLocation);
        var destinationBucket = ParseBackupLocation(destinationBackupLocation);
        ValidateSnapshotTag(snapshotTag);
        var manifestPath = Path.Combine(sourceDirectory, ManifestRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(manifestPath))
            throw new InvalidDataException("The verified Scylla restore point has no portable snapshot manifest.");
        if (new FileInfo(manifestPath).Length > 64 * 1024 * 1024)
            throw new InvalidDataException("The Scylla portable snapshot manifest exceeds its bounded size.");
        var manifest = JsonSerializer.Deserialize<PortableSnapshotManifest>(
            await File.ReadAllBytesAsync(manifestPath, cancellationToken).ConfigureAwait(false), JsonOptions)
            ?? throw new InvalidDataException("The Scylla portable snapshot manifest is empty.");
        ValidateManifest(manifest, sourceBackupLocation, snapshotTag, sourceBucket);

        long totalBytes = 0;
        foreach (var item in manifest.Objects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = ResolveArtifactPath(sourceDirectory, item.RelativePath);
            var (length, sha256) = await HashFileAsync(path, cancellationToken).ConfigureAwait(false);
            if (length != item.Length || !string.Equals(sha256, item.Sha256, StringComparison.Ordinal))
                throw new InvalidDataException("A portable Scylla snapshot object failed local checksum verification.");
            totalBytes = checked(totalBytes + length);
            if (await RemoteMatchesAsync(destinationBucket, item, cancellationToken).ConfigureAwait(false)) continue;
            await using var source = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await _s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = destinationBucket,
                Key = item.Key,
                InputStream = source,
                AutoCloseStream = false
            }, cancellationToken).ConfigureAwait(false);
        }
        if (totalBytes != manifest.TotalBytes)
            throw new InvalidDataException("The Scylla portable snapshot byte total does not match its manifest.");
        return totalBytes;
    }

    async ValueTask AddSnapshotMetadataKeysAsync(
        string bucket,
        string snapshotTag,
        ISet<string> keys,
        CancellationToken cancellationToken)
    {
        string? continuation = null;
        do
        {
            var response = await _s3.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucket,
                Prefix = "backup/",
                ContinuationToken = continuation
            }, cancellationToken).ConfigureAwait(false);
            foreach (var item in response.S3Objects)
            {
                if (item.Key.Contains(snapshotTag, StringComparison.Ordinal)) keys.Add(item.Key);
            }
            continuation = response.IsTruncated == true ? response.NextContinuationToken : null;
        } while (!string.IsNullOrEmpty(continuation));
    }

    async ValueTask<bool> RemoteMatchesAsync(
        string destinationBucket,
        PortableSnapshotObject expected,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = destinationBucket,
                Key = expected.Key
            }, cancellationToken).ConfigureAwait(false);
            var (length, sha256) = await HashAsync(response.ResponseStream, expected.Length, cancellationToken)
                .ConfigureAwait(false);
            if (length == expected.Length && string.Equals(sha256, expected.Sha256, StringComparison.Ordinal)) return true;
            throw new InvalidDataException("A conflicting Scylla snapshot object already exists in Manager storage.");
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    void ValidateManifest(
        PortableSnapshotManifest manifest,
        string backupLocation,
        string snapshotTag,
        string bucket)
    {
        if (manifest.SchemaVersion != 1
            || !string.Equals(manifest.BackupLocation, backupLocation, StringComparison.Ordinal)
            || !string.Equals(manifest.SnapshotTag, snapshotTag, StringComparison.Ordinal)
            || manifest.Objects.Count == 0 || manifest.Objects.Count > _options.MaximumObjectCount
            || manifest.TotalBytes <= 0 || manifest.TotalBytes > _options.MaximumTotalBytes
            || manifest.Objects.Select(static item => item.Key).Distinct(StringComparer.Ordinal).Count() != manifest.Objects.Count)
            throw new InvalidDataException("The Scylla portable snapshot manifest is invalid or outside its bounds.");
        foreach (var item in manifest.Objects)
        {
            if (!string.Equals(item.Bucket, bucket, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(item.Key) || item.Key.Any(char.IsControl)
                || item.Length < 0 || item.Sha256.Length != 64)
                throw new InvalidDataException("A Scylla portable snapshot object descriptor is invalid.");
        }
    }

    static string ResolveArtifactPath(string root, string relativePath)
    {
        if (!relativePath.StartsWith("portable-snapshot/objects/", StringComparison.Ordinal)
            || relativePath.Contains('\\') || relativePath.Split('/').Any(static segment => segment is "" or "." or ".."))
            throw new InvalidDataException("A Scylla portable snapshot path is unsafe.");
        var fullRoot = Path.GetFullPath(root);
        var path = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A Scylla portable snapshot path escaped its restore root.");
        return path;
    }

    static string ParseBackupLocation(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("s3:", StringComparison.Ordinal))
            throw new InvalidOperationException("Portable Scylla snapshots currently require an S3-compatible Manager location.");
        var bucket = value[3..];
        if (string.IsNullOrWhiteSpace(bucket) || bucket.Contains(':') || bucket.Contains('/') || bucket.Any(char.IsControl))
            throw new InvalidOperationException("The Scylla Manager backup bucket is invalid.");
        return bucket;
    }

    static string ParseReference(string value, string expectedBucket)
    {
        var separator = value.IndexOf('|');
        var uriText = (separator >= 0 ? value[..separator] : value).Trim();
        if (!uriText.StartsWith("s3://", StringComparison.Ordinal))
            throw new InvalidDataException("A Scylla Manager artifact reference is not an S3 object URI.");
        var remainder = uriText[5..];
        var slash = remainder.IndexOf('/');
        if (slash <= 0 || !string.Equals(remainder[..slash], expectedBucket, StringComparison.Ordinal)
            || slash == remainder.Length - 1)
            throw new InvalidDataException("A Scylla Manager artifact reference targets an unexpected bucket or key.");
        return remainder[(slash + 1)..];
    }

    static void ValidateSnapshotTag(string value)
    {
        if (value.Length != 20 || !value.StartsWith("sm_", StringComparison.Ordinal)
            || !value.EndsWith("UTC", StringComparison.Ordinal)
            || !value.AsSpan(3, 14).ToString().All(char.IsAsciiDigit))
            throw new InvalidDataException("The Scylla Manager snapshot tag is invalid.");
    }

    static async ValueTask<(long Length, string Sha256)> CopyAndHashAsync(
        Stream source,
        Stream destination,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        if (maximumBytes < 0) throw new InvalidDataException("The Scylla portable snapshot exceeds its byte bound.");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
        long length = 0;
        try
        {
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                length = checked(length + read);
                if (length > maximumBytes)
                    throw new InvalidDataException("The Scylla portable snapshot exceeds its byte bound.");
                hash.AppendData(buffer, 0, read);
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
            return (length, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    static async ValueTask<(long Length, string Sha256)> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await HashAsync(stream, long.MaxValue, cancellationToken).ConfigureAwait(false);
    }

    static async ValueTask<(long Length, string Sha256)> HashAsync(
        Stream source,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        await using var sink = Stream.Null;
        return await CopyAndHashAsync(source, sink, maximumBytes, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => _s3.Dispose();

    sealed record PortableSnapshotManifest(
        int SchemaVersion,
        string BackupLocation,
        string SnapshotTag,
        long TotalBytes,
        IReadOnlyList<PortableSnapshotObject> Objects);

    sealed record PortableSnapshotObject(
        string Bucket,
        string Key,
        string RelativePath,
        long Length,
        string Sha256);
}
