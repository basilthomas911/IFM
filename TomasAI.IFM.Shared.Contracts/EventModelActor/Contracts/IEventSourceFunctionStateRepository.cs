using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>Loads completed Function state and persists only a successful completion.</summary>
public interface IEventSourceFunctionStateRepository<TState, in TRequest>
    where TState : IActorState
    where TRequest : ICommand
{
    ValueTask<TState> LoadStateAsync(TRequest request, CancellationToken cancellationToken = default);

    ValueTask SaveCompletedStateAsync(
        IFunctionActorContext context,
        TState state,
        TRequest request,
        CancellationToken cancellationToken = default);
}
