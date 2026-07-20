using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.State;

/// <summary>
/// Represents the event-sourced state of futures EOD data commands within the actor system.
/// </summary>
/// <remarks>This class manages the state transitions for futures EOD data operations by applying domain events
/// such as <see cref="FuturesEodDataInsertedEvent"/> and <see cref="VixFuturesEodDataInsertedEvent"/>.</remarks>
public class FuturesEodDataCommandState
    : BaseEventSourceActorState<FuturesEodDataCommandState>, IEventSourceActorState<FuturesEodDataCommandState>
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
                FuturesEodDataInsertedEvent e => On(e),
                VixFuturesEodDataInsertedEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;
    }

    /// <summary>
    /// futures eod data inserted
    /// </summary>
    /// <param name="e"></param>
    bool On(FuturesEodDataInsertedEvent e) => true;

    /// <summary>
    /// vix futures eod data inserted
    /// </summary>
    /// <param name="e"></param>
    bool On(VixFuturesEodDataInsertedEvent e) => true;
}
