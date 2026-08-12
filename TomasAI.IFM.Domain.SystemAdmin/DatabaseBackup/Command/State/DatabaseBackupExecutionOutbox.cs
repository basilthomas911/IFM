using System.Collections.Concurrent;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;

public interface IDatabaseBackupExecutionOutbox
{
    ValueTask EnqueueAsync(DatabaseBackupEventContract executionEvent, CancellationToken cancellationToken = default);
    ValueTask MarkPublishedAsync(Guid eventId, CancellationToken cancellationToken = default);
    IReadOnlyCollection<DatabaseBackupEventContract> Pending { get; }
}

public sealed class DatabaseBackupExecutionOutbox : IDatabaseBackupExecutionOutbox
{
    readonly ConcurrentDictionary<Guid, DatabaseBackupEventContract> _pending = [];
    public IReadOnlyCollection<DatabaseBackupEventContract> Pending => [.. _pending.Values];

    public ValueTask EnqueueAsync(DatabaseBackupEventContract executionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executionEvent);
        cancellationToken.ThrowIfCancellationRequested();
        _pending.TryAdd(executionEvent.Id, executionEvent);
        return ValueTask.CompletedTask;
    }

    public ValueTask MarkPublishedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _pending.TryRemove(eventId, out _);
        return ValueTask.CompletedTask;
    }
}
