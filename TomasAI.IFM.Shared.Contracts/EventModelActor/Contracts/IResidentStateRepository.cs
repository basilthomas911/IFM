using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>Validates the stream head when a resident Command acknowledges without appending an event.</summary>
public interface IResidentCommandStateRepository<TState> : IEventSourceActorStateRepository<TState>
    where TState : IEventSourceActorState<TState>
{
    ValueTask ValidateNoChangeAsync(TState state, ICommand command, CancellationToken cancellationToken);
}

/// <summary>Validates a resident completion replay that bypasses an optimistic append.</summary>
public interface IResidentFunctionStateRepository<TState, in TRequest> : IEventSourceFunctionStateRepository<TState, TRequest>
    where TState : IActorState where TRequest : ICommand
{
    ValueTask<bool> IsCurrentAsync(TState state, TRequest request, CancellationToken cancellationToken);
}
