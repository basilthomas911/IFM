using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.State;

/// <summary>
/// Represents the event-sourced state of futures option tick data commands within the actor system.
/// </summary>
/// <remarks>This class manages the state transitions for futures option tick data operations by applying domain events
/// such as <see cref="FuturesOptionTickDataInsertedEvent"/>, <see cref="FuturesOptionTickDataStreamingStartedEvent"/>,
/// and <see cref="FuturesOptionTickDataStreamingStoppedEvent"/>.</remarks>
public class FuturesOptionTickDataCommandState
    : BaseEventSourceActorState<FuturesOptionTickDataCommandState>, IEventSourceActorState<FuturesOptionTickDataCommandState>
{
    public override ActorThreadId Id { get; set; } = default!;

    /// <summary>
    /// apply state change event
    /// </summary>
    /// <param name="domainEvent"></param>
    /// <returns></returns>
    protected override bool Apply(IEvent domainEvent) => domainEvent switch
    {
        FuturesOptionTickDataInsertedEvent => true,
        FuturesOptionTickDataStreamingStartedEvent => true,
        FuturesOptionTickDataStreamingStoppedEvent => true,
        _ => false
    };
}
