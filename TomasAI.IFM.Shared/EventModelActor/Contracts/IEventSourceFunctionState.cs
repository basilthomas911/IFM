using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>Defines completed-only event-sourced Function state.</summary>
public interface IEventSourceFunctionState<TState, in TRequest, TCompletedEvent>
    : IEventSourceActorState<TState>
    where TState : IActorState
    where TRequest : ICommand
    where TCompletedEvent : IEvent
{
    bool IsCompleted { get; }
    TCompletedEvent? CompletedEvent { get; }
    bool Matches(TRequest request);
    bool TryComplete(TCompletedEvent completedEvent, TRequest request);
}
