using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventSourcing.Persistence.Command;

/// <summary>Prepares command audit evidence and chooses audit-only or atomic event persistence.</summary>
public static class AtomicCommandEventCommit
{
    static readonly CommandAuditMessagePackCodec Codec = new();

    public static async Task<DomainEventCollection> CommitAsync(IEventSourceActorDbContext database, ICommand command,
        DomainEventCollection events, long expectedStreamVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedStreamVersion);
        var audit = CommandAuditPreparation.Create(command, Codec, database.BusinessClock);
        if (events.Count != 0)
            return await database.PersistCommandEventBatchAsync(audit, events, expectedStreamVersion, cancellationToken)
                .ConfigureAwait(false);
        if (!await database.TryInsertCommandAuditAsync(audit, cancellationToken).ConfigureAwait(false))
            throw new CommandAuditDuplicateException(command.CommandId);
        return [];
    }
}
