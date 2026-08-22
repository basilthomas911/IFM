using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Publication;

public sealed class LocalDatabaseNativeArtifactSource(
    PostgreSqlBackupOptions postgreSql,
    ScyllaBackupOptions scylla) : IDatabaseNativeArtifactSource
{
    public ValueTask<IReadOnlyList<DatabaseNativeArtifactDescriptor>> DescribeAsync(
        DatabaseEngine engine, DatabaseRecoveryOperationId operationId, CancellationToken cancellationToken)
    {
        var root = ResolveRoot(engine, operationId);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("The verified native backup artifact set is unavailable.");
        var result = new List<DatabaseNativeArtifactDescriptor>();
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RejectLink(path);
            var relative = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
            ValidateRelative(relative);
            result.Add(new DatabaseNativeArtifactDescriptor(relative, new FileInfo(path).Length));
        }
        if (result.Count == 0) throw new InvalidDataException("The native backup artifact set is empty.");
        return ValueTask.FromResult<IReadOnlyList<DatabaseNativeArtifactDescriptor>>(result);
    }

    public ValueTask<Stream> OpenReadAsync(
        DatabaseEngine engine, DatabaseRecoveryOperationId operationId, string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRelative(relativePath);
        var root = ResolveRoot(engine, operationId);
        var path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The native artifact path escaped its approved root.");
        RejectLink(path);
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return ValueTask.FromResult(stream);
    }

    string ResolveRoot(DatabaseEngine engine, DatabaseRecoveryOperationId operationId)
    {
        var configured = engine switch
        {
            DatabaseEngine.PostgreSql => postgreSql.ResolveBackupRoot(),
            DatabaseEngine.ScyllaDb => scylla.ResolveBackupRoot(),
            _ => throw new ArgumentOutOfRangeException(nameof(engine))
        };
        return Path.GetFullPath(Path.Combine(configured, operationId.Format()));
    }

    static void ValidateRelative(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value) || value.Contains('\\')
            || value.Split('/').Any(static segment => segment is "" or "." or "..") || value.Any(char.IsControl))
            throw new ArgumentException("The native artifact relative path is unsafe.", nameof(value));
    }

    static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Native backup artifacts cannot contain filesystem links.");
    }
}
