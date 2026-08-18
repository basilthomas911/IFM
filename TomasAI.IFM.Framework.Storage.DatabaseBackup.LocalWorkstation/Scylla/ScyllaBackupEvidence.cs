using System.Security.Cryptography;
using System.Text.Json;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Publication;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Scylla;

internal sealed record ScyllaBackupEvidence(
    DatabaseRecoveryOperationId OperationId,
    string ProtectionSetId,
    string SafeBoundaryReference,
    ScyllaTopologyEvidence Topology,
    ScyllaSnapshotEvidence Snapshot,
    string[] ArtifactReferences,
    DatabaseRecoveryRunStatistics Statistics,
    DateTimeOffset CapturedUtc,
    DatabaseBackupLineage? BackupLineage = null);

internal sealed record ScyllaRestoreEvidence(
    DatabaseRecoveryOperationId OperationId,
    string RestorePointId,
    string SafeTargetReference,
    string SourceClusterName,
    string RestoredClusterName,
    long ValidationRevision,
    ScyllaTopologyEvidence Topology,
    DatabaseRecoveryRunStatistics Statistics,
    DateTimeOffset ValidatedUtc);

internal sealed class ScyllaBackupPathResolver(ScyllaBackupOptions options)
{
    public string BackupRoot { get; } = options.ResolveBackupRoot();
    public string RestoreRoot { get; } = options.ResolveRestoreRoot();

    public string BackupStaging(DatabaseRecoveryOperationId operationId)
        => Child(BackupRoot, operationId.Format() + ".inprogress");

    public string BackupFinal(DatabaseRecoveryOperationId operationId)
        => Child(BackupRoot, operationId.Format());

    public string RestorePoint(DatabaseRestorePointId restorePointId)
        => Child(BackupRoot, restorePointId.Value);

    public string RestoreStaging(ScyllaRestoreRequest request)
        => Child(RestoreRoot, request.FreshTarget.Profile, request.FreshTarget.LogicalTarget,
            request.OperationId.Format() + ".inprogress");

    public string RestoreFinal(ScyllaRestoreRequest request)
        => Child(RestoreRoot, request.FreshTarget.Profile, request.FreshTarget.LogicalTarget,
            request.OperationId.Format());

    static string Child(string root, params string[] segments)
    {
        var path = Path.GetFullPath(segments.Aggregate(root, Path.Combine));
        if (!PostgreSqlBackupOptions.IsWithin(path, root))
            throw new InvalidOperationException("A Scylla operation path escaped its configured root.");
        return path;
    }
}

internal static class ScyllaEvidenceSerializer
{
    const string BackupEvidenceName = "ifm-scylla-backup-evidence.json";
    const string RestoreEvidenceName = "ifm-scylla-restore-evidence.json";
    static readonly JsonSerializerOptions Options = new(LocalBackupJson.Options) { WriteIndented = true };

    public static string BackupEvidencePath(string root) => Path.Combine(root, BackupEvidenceName);
    public static string RestoreEvidencePath(string root) => Path.Combine(root, RestoreEvidenceName);

    public static ValueTask WriteBackupAsync(string root, ScyllaBackupEvidence evidence, CancellationToken cancellationToken)
        => WriteAtomicAsync(BackupEvidencePath(root), evidence, cancellationToken);

    public static ValueTask WriteRestoreAsync(string root, ScyllaRestoreEvidence evidence, CancellationToken cancellationToken)
        => WriteAtomicAsync(RestoreEvidencePath(root), evidence, cancellationToken);

    public static ValueTask<ScyllaBackupEvidence> ReadBackupAsync(string root, CancellationToken cancellationToken)
        => ReadAsync<ScyllaBackupEvidence>(BackupEvidencePath(root), cancellationToken);

    public static ValueTask<ScyllaRestoreEvidence> ReadRestoreAsync(string root, CancellationToken cancellationToken)
        => ReadAsync<ScyllaRestoreEvidence>(RestoreEvidencePath(root), cancellationToken);

    public static async ValueTask<string> DirectoryManifestSha256Async(string root, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .OrderBy(static value => value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes(relative));
            await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                hash.AppendData(buffer.AsSpan(0, read));
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    static async ValueTask WriteAtomicAsync<T>(string path, T evidence, CancellationToken cancellationToken)
    {
        var temporary = path + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, evidence, Options, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        File.Move(temporary, path, overwrite: false);
    }

    static async ValueTask<T> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Scylla recovery evidence is empty.");
    }
}
