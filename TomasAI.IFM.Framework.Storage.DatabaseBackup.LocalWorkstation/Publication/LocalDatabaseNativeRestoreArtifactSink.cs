using System.Buffers;
using System.Security.Cryptography;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Publication;

public sealed class LocalDatabaseNativeRestoreArtifactSink(
    PostgreSqlBackupOptions postgreSql,
    ScyllaBackupOptions scylla) : IDatabaseNativeRestoreArtifactSink
{
    public ValueTask PrepareFreshAsync(
        DatabaseEngine engine, DatabaseRestorePointId restorePointId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = Root(engine, restorePointId);
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            throw new InvalidOperationException("The AWS native restore staging target is not fresh.");
        Directory.CreateDirectory(root);
        return ValueTask.CompletedTask;
    }

    public async ValueTask WriteAsync(
        DatabaseEngine engine, DatabaseRestorePointId restorePointId, string relativePath,
        Stream source, long expectedLength, string expectedSha256, CancellationToken cancellationToken)
    {
        ValidateRelative(relativePath);
        var root = Root(engine, restorePointId);
        var target = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The AWS restore artifact escaped its approved native root.");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (File.Exists(target)) throw new IOException("The AWS restore artifact target already exists.");
        await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            128 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
        long written = 0;
        try
        {
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                hash.AppendData(buffer, 0, read);
                written += read;
                if (written > expectedLength) throw new InvalidDataException("The AWS restore artifact exceeds its declared length.");
            }
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (written != expectedLength || !CryptographicOperations.FixedTimeEquals(
                    hash.GetHashAndReset(), Convert.FromHexString(expectedSha256)))
                throw new InvalidDataException("The AWS restore artifact failed length or SHA-256 verification.");
        }
        catch
        {
            output.Dispose();
            File.Delete(target);
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    string Root(DatabaseEngine engine, DatabaseRestorePointId restorePointId)
    {
        var configured = engine switch
        {
            DatabaseEngine.PostgreSql => postgreSql.ResolveBackupRoot(),
            DatabaseEngine.ScyllaDb => scylla.ResolveBackupRoot(),
            _ => throw new ArgumentOutOfRangeException(nameof(engine))
        };
        var root = Path.GetFullPath(Path.Combine(configured, restorePointId.Value));
        var approved = Path.GetFullPath(configured);
        if (!root.StartsWith(approved + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The AWS native restore point escaped its approved root.");
        return root;
    }

    static void ValidateRelative(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value) || value.Contains('\\')
            || value.Split('/').Any(static segment => segment is "" or "." or "..") || value.Any(char.IsControl))
            throw new ArgumentException("The AWS restore artifact relative path is unsafe.", nameof(value));
    }
}
