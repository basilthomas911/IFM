using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

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
    protected override bool Apply(IEvent domainEvent)
    {
        try
        {
            return domainEvent switch
            {
                FuturesOptionTickDataInsertedEvent e => On(e),
                FuturesOptionTickDataStreamingStartedEvent e => On(e),
                FuturesOptionTickDataStreamingStoppedEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;
    }

    /// <summary>
    /// futures option tick data inserted
    /// </summary>
    /// <param name="e"></param>
    bool On(FuturesOptionTickDataInsertedEvent e) => true;

    /// <summary>
    /// futures option tick data streaming started
    /// </summary>
    /// <param name="e"></param>
    bool On(FuturesOptionTickDataStreamingStartedEvent e) => true;

    /// <summary>
    /// futures option tick data streaming stopped
    /// </summary>
    /// <param name="e"></param>
    bool On(FuturesOptionTickDataStreamingStoppedEvent e) => true;
}
