using System.Security.Cryptography;
using System.Text.Json;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Publication;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.PostgreSql;

internal sealed record PostgreSqlBackupEvidence(
    DatabaseRecoveryOperationId OperationId,
    string SafeBoundaryReference,
    string ManifestSha256,
    string SystemIdentifier,
    PostgreSqlWalContinuityEvidence WalContinuity,
    DatabaseRecoveryRunStatistics Statistics,
    int NativeMajorVersion,
    DateTimeOffset VerifiedUtc,
    DatabaseBackupLineage? BackupLineage = null);

internal sealed record PostgreSqlRestoreEvidence(
    DatabaseRecoveryOperationId OperationId,
    string RestorePointId,
    string SafeTargetReference,
    string SourceSystemIdentifier,
    string RestoredSystemIdentifier,
    long ValidationRevision,
    DatabaseRecoveryRunStatistics Statistics,
    DateTimeOffset ValidatedUtc);

internal sealed record PostgreSqlManifestEvidence(
    string ManifestSha256,
    string SystemIdentifier,
    PostgreSqlWalContinuityEvidence WalContinuity,
    int FileCount,
    long SourceBytes);

internal sealed class PostgreSqlBackupPathResolver(PostgreSqlBackupOptions options)
{
    public string BackupRoot { get; } = options.ResolveBackupRoot();
    public string RestoreRoot { get; } = options.ResolveRestoreRoot();

    public string BackupStaging(DatabaseRecoveryOperationId operationId)
        => Child(BackupRoot, operationId.Format() + ".inprogress");

    public string BackupFinal(DatabaseRecoveryOperationId operationId)
        => Child(BackupRoot, operationId.Format());

    public string RestorePoint(DatabaseRestorePointId restorePointId)
        => Child(BackupRoot, restorePointId.Value);

    public string RestoreStaging(PostgreSqlRestoreRequest request)
        => Child(RestoreRoot, request.FreshTarget.Profile, request.FreshTarget.LogicalTarget,
            request.OperationId.Format() + ".inprogress");

    public string RestoreFinal(PostgreSqlRestoreRequest request)
        => Child(RestoreRoot, request.FreshTarget.Profile, request.FreshTarget.LogicalTarget,
            request.OperationId.Format());

    static string Child(string root, params string[] segments)
    {
        var path = Path.GetFullPath(segments.Aggregate(root, Path.Combine));
        if (!PostgreSqlBackupOptions.IsWithin(path, root))
            throw new InvalidOperationException("A PostgreSQL operation path escaped its configured root.");
        return path;
    }
}

internal static class PostgreSqlBackupEvidenceSerializer
{
    const string BackupEvidenceName = "ifm-postgresql-backup-evidence.json";
    const string RestoreEvidenceName = "ifm-postgresql-restore-evidence.json";
    static readonly JsonSerializerOptions Options = new(LocalBackupJson.Options) { WriteIndented = true };

    public static string BackupEvidencePath(string operationRoot) => Path.Combine(operationRoot, BackupEvidenceName);
    public static string RestoreEvidencePath(string operationRoot) => Path.Combine(operationRoot, RestoreEvidenceName);

    public static async ValueTask WriteBackupAsync(
        string operationRoot,
        PostgreSqlBackupEvidence evidence,
        CancellationToken cancellationToken)
        => await WriteAtomicAsync(BackupEvidencePath(operationRoot), evidence, cancellationToken).ConfigureAwait(false);

    public static async ValueTask WriteRestoreAsync(
        string operationRoot,
        PostgreSqlRestoreEvidence evidence,
        CancellationToken cancellationToken)
        => await WriteAtomicAsync(RestoreEvidencePath(operationRoot), evidence, cancellationToken).ConfigureAwait(false);

    public static ValueTask<PostgreSqlBackupEvidence> ReadBackupAsync(string operationRoot, CancellationToken cancellationToken)
        => ReadAsync<PostgreSqlBackupEvidence>(BackupEvidencePath(operationRoot), cancellationToken);

    public static ValueTask<PostgreSqlRestoreEvidence> ReadRestoreAsync(string operationRoot, CancellationToken cancellationToken)
        => ReadAsync<PostgreSqlRestoreEvidence>(RestoreEvidencePath(operationRoot), cancellationToken);

    static async ValueTask WriteAtomicAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        var temporary = path + ".tmp";
        await using (var stream = new FileStream(
            temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, value, Options, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        File.Move(temporary, path, overwrite: false);
    }

    static async ValueTask<T> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        return await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("PostgreSQL recovery evidence is empty.");
    }
}

internal static class PostgreSqlBackupManifestReader
{
    public static async ValueTask<PostgreSqlManifestEvidence> ReadAsync(
        string dataDirectory,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(dataDirectory, "backup_manifest");
        var bytes = await File.ReadAllBytesAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        var systemIdentifier = OptionalIdentifier(root, "System-Identifier");
        var files = root.GetProperty("Files");
        var sourceBytes = 0L;
        foreach (var file in files.EnumerateArray()) sourceBytes = checked(sourceBytes + file.GetProperty("Size").GetInt64());
        var ranges = root.GetProperty("WAL-Ranges");
        if (ranges.GetArrayLength() == 0) throw new InvalidDataException("The PostgreSQL manifest contains no WAL range.");
        var first = ranges[0];
        var last = ranges[ranges.GetArrayLength() - 1];
        var timeline = first.GetProperty("Timeline").GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var startLsn = RequiredString(first, "Start-LSN");
        var endLsn = RequiredString(last, "End-LSN");
        var walDirectory = Path.Combine(dataDirectory, "pg_wal");
        var segmentCount = Directory.Exists(walDirectory)
            ? Directory.EnumerateFiles(walDirectory)
                .Count(static file => IsWalSegment(Path.GetFileName(file)))
            : 0;
        return new PostgreSqlManifestEvidence(
            digest,
            systemIdentifier,
            new PostgreSqlWalContinuityEvidence(timeline, startLsn, endLsn, segmentCount, segmentCount > 0),
            files.GetArrayLength(),
            sourceBytes);
    }

    static string RequiredString(JsonElement source, string name)
    {
        var value = source.GetProperty(name).GetString();
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"The PostgreSQL manifest field '{name}' is missing.")
            : value;
    }

    static string OptionalIdentifier(JsonElement source, string name)
    {
        if (!source.TryGetProperty(name, out var element)) return string.Empty;
        var value = element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            _ => null
        };
        return value ?? string.Empty;
    }

    static bool IsWalSegment(string fileName)
        => fileName.Length == 24 && fileName.All(static character => char.IsAsciiHexDigit(character));
}
