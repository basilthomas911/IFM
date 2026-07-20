using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

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
    protected override bool Apply(IEvent domainEvent)
    {
        try
        {
            return domainEvent switch
            {
                FuturesBarDataInsertedEvent e => On(e),
                FuturesBarDataDeletedEvent e => On(e),
                FuturesBarDataStreamingStartedEvent e => On(e),
                FuturesBarDataStreamingStoppedEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;
    }

    /// <summary>
    /// futures bar data inserted
    /// </summary>
    /// <param name="e"></param>
    bool On(FuturesBarDataInsertedEvent e) => true;

    /// <summary>
    /// futures bar data deleted
    /// </summary>
    /// <param name="e"></param>
    bool On(FuturesBarDataDeletedEvent e) => true;

    /// <summary>
    /// futures bar data streaming started
    /// </summary>
    /// <param name="e"></param>
    bool On(FuturesBarDataStreamingStartedEvent e) => true;

    /// <summary>
    /// futures bar data streaming stopped
    /// </summary>
    /// <param name="e"></param>
    bool On(FuturesBarDataStreamingStoppedEvent e) => true;
}
