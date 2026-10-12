using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.State;

/// <summary>
/// Represents the event-sourced state of futures tick data commands within the actor system.
/// </summary>
/// <remarks>This class manages the state transitions for futures tick data operations by applying domain events
/// such as <see cref="FuturesTickDataInsertedEvent"/>, <see cref="FuturesTickDataStreamingStartedEvent"/>,
/// and <see cref="FuturesTickDataStreamingStoppedEvent"/>.</remarks>
public class FuturesTickDataCommandState
    : BaseEventSourceActorState<FuturesTickDataCommandState>, IEventSourceActorState<FuturesTickDataCommandState>
{
    public override ActorThreadId Id { get; set; } = default!;

    /// <summary>
    /// apply state change event
    /// </summary>
    /// <param name="domainEvent"></param>
    /// <returns></returns>
    protected override bool Apply(IEvent domainEvent) => domainEvent switch
    {
        FuturesTickDataInsertedEvent => true,
        FuturesTickDataStreamingStartedEvent => true,
        FuturesTickDataStreamingStoppedEvent => true,
        _ => false
    };
}
