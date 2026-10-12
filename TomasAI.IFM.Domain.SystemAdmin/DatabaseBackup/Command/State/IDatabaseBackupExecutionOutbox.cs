using System.Collections.Concurrent;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;

public interface IDatabaseBackupExecutionOutbox
{
    ValueTask EnqueueAsync(DatabaseBackupEventContract executionEvent, CancellationToken cancellationToken = default);
    ValueTask MarkPublishedAsync(Guid eventId, CancellationToken cancellationToken = default);
    IReadOnlyCollection<DatabaseBackupEventContract> Pending { get; }
}
