using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

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
    protected override bool Apply(IEvent domainEvent)
    {
        try
        {
            return domainEvent switch
            {
                FuturesTickDataInsertedEvent e => On(e),
                FuturesTickDataStreamingStartedEvent e => On(e),
                FuturesTickDataStreamingStoppedEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;
    }

    /// <summary>
    /// futures tick data inserted
    /// </summary>
    /// <param name="e"></param>
    bool On(FuturesTickDataInsertedEvent e) => true;

    /// <summary>
    /// futures tick data streaming started
    /// </summary>
    /// <param name="e"></param>
    bool On(FuturesTickDataStreamingStartedEvent e) => true;

    /// <summary>
    /// futures tick data streaming stopped
    /// </summary>
    /// <param name="e"></param>
    bool On(FuturesTickDataStreamingStoppedEvent e) => true;
}
