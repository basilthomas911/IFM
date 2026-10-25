using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Recovery;

/// <summary>
/// A deployment-wide file lock and flushed recovery record, stored outside active database
/// volumes. PostgreSQL restore cannot roll this journal back with the System Admin database.
/// </summary>
public sealed class FileDatabaseReplacementRecoveryJournal : IDatabaseReplacementRecoveryJournal
{
    static readonly JsonSerializerOptions Serialization = new()
    {
        Converters =
        {
            new IdConverter<DatabaseRecoveryOperationId>(id => id.Value.ToString("D"), text => new(Guid.Parse(text))),
            new IdConverter<DatabaseProtectionSetId>(id => id.Value, text => new(text)),
            new IdConverter<DatabaseRestorePointId>(id => id.Value, text => new(text))
        }
    };
    readonly string _root;

    public FileDatabaseReplacementRecoveryJournal(string persistentRoot)
    {
        if (!Path.IsPathFullyQualified(persistentRoot))
            throw new ArgumentException("Recovery journal requires an absolute persistent path.", nameof(persistentRoot));
        _root = Path.GetFullPath(persistentRoot);
    }

    public async ValueTask<IDatabaseReplacementRecoveryLease> AcquireAsync(
        DatabaseCutoverRequest request, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_root);
        FileStream? deploymentLock = null;
        while (deploymentLock is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                deploymentLock = new FileStream(Path.Combine(_root, "deployment-recovery.lock"),
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
        }
        var ownPath = Path.Combine(_root, request.OperationId.Value.ToString("N") + ".json");
        try
        {
            foreach (var path in Directory.EnumerateFiles(_root, "*.json"))
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(path, ownPath)) continue;
                await using var stream = File.OpenRead(path);
                var record = await JsonSerializer.DeserializeAsync<DatabaseReplacementRecoveryRecord>(stream,
                    Serialization, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidDataException("Recovery journal contains an invalid record.");
                if (record.Stage is not (DatabaseReplacementStage.Completed or DatabaseReplacementStage.RolledBack))
                    throw new InvalidOperationException("Another database recovery requires reconciliation before a new replacement.");
            }
            return new Lease(deploymentLock, ownPath);
        }
        catch
        {
            deploymentLock.Dispose();
            throw;
        }
    }

    sealed class IdConverter<TId>(Func<TId, string> format, Func<string, TId> parse) : JsonConverter<TId>
    {
        public override TId Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            => parse(reader.GetString() ?? throw new JsonException("Recovery identifier is null."));
        public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
            => writer.WriteStringValue(format(value));
    }

    sealed class Lease(FileStream deploymentLock, string path) : IDatabaseReplacementRecoveryLease
    {
        bool _disposed;

        public async ValueTask<DatabaseReplacementRecoveryRecord?> ReadAsync(CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!File.Exists(path)) return null;
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<DatabaseReplacementRecoveryRecord>(stream,
                Serialization, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("The durable recovery record is invalid.");
        }

        public async ValueTask WriteAsync(DatabaseReplacementRecoveryRecord record, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var temporary = path + ".pending";
            await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, record, Serialization, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }

        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                deploymentLock.Dispose();
            }
            return ValueTask.CompletedTask;
        }
    }
}
