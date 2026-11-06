using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Persistence;

/// <summary>Restores Trade aggregates whose accepted facts replace the entire authoritative definition.</summary>
public abstract class TradeReplacementStateRepository(IEventSourceActorStateFactory factory,
    IEventSourceActorDbContext source, IActorService actors, ILogger logger)
    : BaseEventSourceActorRepository(factory, source, actors, logger)
{
    protected async ValueTask<TState> LoadVerifiedReplacementAsync<TState, TFact, TId>(ICommand command,
        Func<TFact, bool> isComplete)
        where TState : BaseEventSourceActorState<TState>
        where TFact : IEvent<TId>
        where TId : IActorEntityId
    {
        var accepted = false;
        var state = await LoadStateFromLatestCheckpointAsync<TState>(command, [typeof(TFact)],
            (target, fact, _) =>
            {
                // A later verified replacement can supersede an incomplete legacy prefix.
                accepted = fact is TFact snapshot && isComplete(snapshot) &&
                    string.Equals(snapshot.EntityId.Format(), command.Subject.EntityId, StringComparison.Ordinal) &&
                    target.Apply(snapshot, addEvent: false);
                return true;
            }, CancellationToken.None).ConfigureAwait(false);
        if (state.CommittedStreamVersion > 0 && !accepted)
            throw new InvalidDataException($"Trade stream '{command.StreamId}' has no usable replacement at its committed head.");
        // All accepted facts are already committed and were applied without adding pending changes.
        state.AcceptChanges();
        return state;
    }
}
