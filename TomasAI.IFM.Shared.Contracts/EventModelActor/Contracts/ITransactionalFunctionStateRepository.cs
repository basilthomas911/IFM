using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>Commits business changes and the canonical completed event in one durable transaction.</summary>
/// <remarks>The implementation owns reconciliation and must not mutate actor state before commit.
/// The returned event is the stored original, including on an idempotent concurrent replay.</remarks>
public interface ITransactionalFunctionStateRepository<TState, in TRequest, TCompletedEvent>
    : IEventSourceFunctionStateRepository<TState, TRequest>
    where TState : IActorState
    where TRequest : ICommand
    where TCompletedEvent : class, IEvent
{
    /// <summary>Returns only after confirmed commit; ambiguous outcomes must throw a classified exception.</summary>
    ValueTask<TCompletedEvent> CommitAsync(IFunctionActorContext context, TRequest request,
        TCompletedEvent candidate, CancellationToken cancellationToken = default);
}
