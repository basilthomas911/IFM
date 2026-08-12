using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Publication;

public sealed class LocalBackupPathPolicy(
    DatabaseBackupPublicationOptions options,
    PostgreSqlBackupOptions postgreSql,
    ScyllaBackupOptions scylla) : IBackupPathPolicy
{
    public DatabaseApprovedStorageRoot GetReplicaRoot(DatabaseArtifactReplicaId replicaId)
    {
        if (replicaId == new DatabaseArtifactReplicaId(options.OnlineVault.ReplicaId))
            return new DatabaseApprovedStorageRoot(replicaId.Value, options.OnlineVault.ResolveRoot());
        if (options.OfflineMedia.Enabled
            && replicaId == new DatabaseArtifactReplicaId(options.OfflineMedia.ReplicaId))
            return new DatabaseApprovedStorageRoot(replicaId.Value, options.OfflineMedia.ResolveRoot());
        throw new InvalidOperationException("The requested local backup replica is not configured.");
    }

    public DatabaseApprovedStorageRoot GetRestoreWorkspaceRoot()
        => new("restore-workspace", options.RestoreWorkspace.ResolveRoot());

    public DatabaseApprovedStorageRoot GetNativeBackupRoot(DatabaseEngine engine)
        => engine switch
        {
            DatabaseEngine.PostgreSql => new("native-postgresql", postgreSql.ResolveBackupRoot()),
            DatabaseEngine.ScyllaDb => new("native-scylla", scylla.ResolveBackupRoot()),
            _ => throw new ArgumentOutOfRangeException(nameof(engine))
        };

    public string Resolve(DatabaseApprovedStorageRoot approvedRoot, string normalizedRelativePath)
    {
        ArgumentNullException.ThrowIfNull(approvedRoot);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(approvedRoot.FullPath));
        if (string.IsNullOrWhiteSpace(normalizedRelativePath) || Path.IsPathFullyQualified(normalizedRelativePath))
            throw new InvalidOperationException("A non-empty relative backup path is required.");
        var segments = normalizedRelativePath.Replace('\\', '/').Split('/');
        if (segments.Any(IsUnsafeSegment))
            throw new InvalidOperationException("The backup path contains traversal, an alternate stream, or an invalid segment.");
        var resolved = Path.GetFullPath(Path.Combine([root, .. segments]));
        if (!DatabaseBackupPublicationOptions.IsWithin(resolved, root))
            throw new InvalidOperationException("The backup path escapes its approved root.");
        RejectLinksAlongExistingPath(root, resolved);
        return resolved;
    }

    public void ValidateTree(DatabaseApprovedStorageRoot approvedRoot)
    {
        ArgumentNullException.ThrowIfNull(approvedRoot);
        var root = Path.GetFullPath(approvedRoot.FullPath);
        if (!Directory.Exists(root)) return;
        RejectLink(new DirectoryInfo(root));
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    var child = new DirectoryInfo(entry);
                    RejectLink(child);
                    pending.Push(entry);
                }
                else
                {
                    RejectLink(new FileInfo(entry));
                }
            }
        }
    }

    static bool IsUnsafeSegment(string segment)
        => string.IsNullOrWhiteSpace(segment)
            || segment is "." or ".."
            || segment.Contains(':', StringComparison.Ordinal)
            || segment.Any(char.IsControl)
            || segment.EndsWith(' ') || segment.EndsWith('.');

    static void RejectLinksAlongExistingPath(string root, string resolved)
    {
        if (Directory.Exists(root)) RejectLink(new DirectoryInfo(root));
        var relative = Path.GetRelativePath(root, resolved);
        var current = root;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current)) RejectLink(new DirectoryInfo(current));
            else if (File.Exists(current)) RejectLink(new FileInfo(current));
            else break;
        }
    }

    internal static void RejectLink(FileSystemInfo entry)
    {
        entry.Refresh();
        if (entry.LinkTarget is not null || entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidOperationException("Backup paths cannot contain symbolic links, junctions, or reparse points.");
    }
}

public sealed class Sha256ArtifactChecksumService(IBackupPathPolicy pathPolicy) : IArtifactChecksumService
{
    public async ValueTask<DatabaseArtifactDigest> CalculateAsync(
        DatabaseApprovedStorageRoot approvedRoot,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var path = pathPolicy.Resolve(approvedRoot, relativePath);
        var file = new FileInfo(path);
        if (!file.Exists) throw new FileNotFoundException("A manifest artifact is missing.", path);
        LocalBackupPathPolicy.RejectLink(file);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var digest = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return new DatabaseArtifactDigest(
            relativePath.Replace('\\', '/'), file.Length, Convert.ToHexStringLower(digest));
    }
}

public sealed class EcdsaManifestSignatureService : IManifestSignatureService, IDisposable
{
    public const string AlgorithmName = "ECDSA-P256-SHA256";
    readonly ECDsa? _privateKey;
    readonly ECDsa _publicKey;

    public EcdsaManifestSignatureService(DatabaseManifestOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate(requirePrivateKey: false);
        KeyId = options.KeyId;
        LocalBackupPathPolicy.RejectLink(new FileInfo(options.PublicKeyPemFile));
        _publicKey = ECDsa.Create();
        _publicKey.ImportFromPem(File.ReadAllText(options.PublicKeyPemFile));
        if (!string.IsNullOrWhiteSpace(options.PrivateKeyPemFile))
        {
            LocalBackupPathPolicy.RejectLink(new FileInfo(options.PrivateKeyPemFile));
            _privateKey = ECDsa.Create();
            _privateKey.ImportFromPem(File.ReadAllText(options.PrivateKeyPemFile));
        }
    }

    public string KeyId { get; }

    public DatabaseManifestSignature Sign(ReadOnlySpan<byte> content)
    {
        if (_privateKey is null)
            throw new InvalidOperationException("The manifest signing private key is unavailable in this process.");
        return new DatabaseManifestSignature(
            KeyId,
            AlgorithmName,
            Convert.ToBase64String(_privateKey.SignData(content, HashAlgorithmName.SHA256)));
    }

    public void Verify(ReadOnlySpan<byte> content, DatabaseManifestSignature signature)
    {
        ArgumentNullException.ThrowIfNull(signature);
        if (!string.Equals(signature.KeyId, KeyId, StringComparison.Ordinal)
            || !string.Equals(signature.Algorithm, AlgorithmName, StringComparison.Ordinal))
            throw new CryptographicException("The manifest signature key or algorithm is not trusted.");
        byte[] value;
        try { value = Convert.FromBase64String(signature.Value); }
        catch (FormatException exception) { throw new CryptographicException("The manifest signature is malformed.", exception); }
        if (!_publicKey.VerifyData(content, value, HashAlgorithmName.SHA256))
            throw new CryptographicException("The manifest signature is invalid.");
    }

    public void Dispose()
    {
        _privateKey?.Dispose();
        _publicKey.Dispose();
    }
}

public sealed class LocalBackupCapacityReader : ILocalBackupCapacityReader
{
    public long GetAvailableBytes(DatabaseApprovedStorageRoot approvedRoot)
    {
        ArgumentNullException.ThrowIfNull(approvedRoot);
        var root = Path.GetFullPath(approvedRoot.FullPath);
        var existing = root;
        while (!Directory.Exists(existing))
            existing = Path.GetDirectoryName(existing)
                ?? throw new InvalidOperationException("The approved storage root has no available filesystem.");
        return new DriveInfo(Path.GetPathRoot(existing)!).AvailableFreeSpace;
    }

    public void EnsureCapacity(DatabaseApprovedStorageRoot approvedRoot, long requiredBytes, long reserveBytes)
    {
        if (requiredBytes < 0 || reserveBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(requiredBytes), "Capacity requirements cannot be negative.");
        var available = GetAvailableBytes(approvedRoot);
        if (requiredBytes > available || reserveBytes > available - requiredBytes)
            throw new IOException($"The approved storage root lacks capacity for {requiredBytes} bytes plus its reserve.");
    }
}

internal static class LocalBackupJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
        options.Converters.Add(new StrongIdJsonConverter<DatabaseRecoveryOperationId>(
            static value => new DatabaseRecoveryOperationId(Guid.ParseExact(value, "N")),
            static value => value.Format()));
        options.Converters.Add(new StrongIdJsonConverter<DatabaseRetentionPlanId>(
            static value => new DatabaseRetentionPlanId(Guid.ParseExact(value, "N")),
            static value => value.Value.ToString("N")));
        options.Converters.Add(new StrongIdJsonConverter<DatabaseProtectionSetId>(
            static value => new DatabaseProtectionSetId(value), static value => value.Value));
        options.Converters.Add(new StrongIdJsonConverter<DatabaseRestorePointId>(
            static value => new DatabaseRestorePointId(value), static value => value.Value));
        options.Converters.Add(new StrongIdJsonConverter<DatabaseArtifactId>(
            static value => new DatabaseArtifactId(value), static value => value.Value));
        options.Converters.Add(new StrongIdJsonConverter<DatabaseArtifactReplicaId>(
            static value => new DatabaseArtifactReplicaId(value), static value => value.Value));
        return options;
    }

    public static byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);

    public static T Deserialize<T>(ReadOnlySpan<byte> value)
        => JsonSerializer.Deserialize<T>(value, Options)
            ?? throw new InvalidDataException($"The signed {typeof(T).Name} document is empty.");

    public static async ValueTask WriteCreateNewAsync(
        string path,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            64 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    public static async ValueTask WriteSignedCreateNewAsync<T>(
        string documentPath,
        T document,
        IManifestSignatureService signatures,
        CancellationToken cancellationToken)
    {
        var content = Serialize(document);
        var signature = Serialize(signatures.Sign(content));
        await WriteCreateNewAsync(documentPath, content, cancellationToken).ConfigureAwait(false);
        await WriteCreateNewAsync(documentPath + ".sig", signature, cancellationToken).ConfigureAwait(false);
        var reopened = await File.ReadAllBytesAsync(documentPath, cancellationToken).ConfigureAwait(false);
        var reopenedSignature = Deserialize<DatabaseManifestSignature>(
            await File.ReadAllBytesAsync(documentPath + ".sig", cancellationToken).ConfigureAwait(false));
        signatures.Verify(reopened, reopenedSignature);
    }

    public static async ValueTask<T> ReadSignedAsync<T>(
        string documentPath,
        IManifestSignatureService signatures,
        CancellationToken cancellationToken)
    {
        var content = await File.ReadAllBytesAsync(documentPath, cancellationToken).ConfigureAwait(false);
        var signature = Deserialize<DatabaseManifestSignature>(
            await File.ReadAllBytesAsync(documentPath + ".sig", cancellationToken).ConfigureAwait(false));
        signatures.Verify(content, signature);
        return Deserialize<T>(content);
    }

    sealed class StrongIdJsonConverter<T>(Func<string, T> parse, Func<T, string> format)
        : JsonConverter<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => parse(reader.GetString() ?? throw new JsonException("A strong identity cannot be null."));

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => writer.WriteStringValue(format(value));
    }
}
