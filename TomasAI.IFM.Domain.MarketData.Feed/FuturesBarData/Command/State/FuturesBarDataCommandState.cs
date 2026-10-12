using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.State;

/// <summary>
/// Represents the event-sourced state of futures bar data commands within the actor system.
/// </summary>
/// <remarks>This class manages the state transitions for futures bar data operations by applying domain events
/// such as <see cref="FuturesBarDataInsertedEvent"/>, <see cref="FuturesBarDataDeletedEvent"/>,
/// <see cref="FuturesBarDataStreamingStartedEvent"/>, and <see cref="FuturesBarDataStreamingStoppedEvent"/>.</remarks>
public class FuturesBarDataCommandState
    : BaseEventSourceActorState<FuturesBarDataCommandState>, IEventSourceActorState<FuturesBarDataCommandState>
{
    public override ActorThreadId Id { get; set; } = default!;

    /// <summary>
    /// apply state change event
    /// </summary>
    /// <param name="domainEvent"></param>
    /// <returns></returns>
    protected override bool Apply(IEvent domainEvent) => domainEvent switch
    {
        FuturesBarDataInsertedEvent => true,
        FuturesBarDataDeletedEvent => true,
        FuturesBarDataStreamingStartedEvent => true,
        FuturesBarDataStreamingStoppedEvent => true,
        _ => false
    };
}
