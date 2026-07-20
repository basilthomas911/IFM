using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.State;

/// <summary>
/// Represents the event-sourced state of futures closing price commands within the actor system.
/// </summary>
/// <remarks>This class manages the state transitions for futures closing price operations by applying domain events
/// such as <see cref="FuturesClosingPriceInsertedEvent"/>.</remarks>
public class FuturesClosingPriceCommandState(IMarketDataDbContext? db = null)
    : BaseEventSourceActorState<FuturesClosingPriceCommandState>, IEventSourceActorState<FuturesClosingPriceCommandState>
{
    public override ActorThreadId Id { get; set; } = default!;

    readonly IMarketDataDbContext? _db = db;
    FuturesClosingPriceInsertedEvent? _closingPrice = default;

    /// <summary>
    /// Determines whether a closing price exists for the specified futures data identifier using in-memory state.
    /// </summary>
    /// <param name="id">The identifier for the futures data to check for a closing price.</param>
    /// <returns>true if a closing price exists for the specified identifier; otherwise, false.</returns>
    internal bool FuturesClosingPriceExists(FuturesDataId id)
        => _closingPrice is not null && _closingPrice.EntityId == id;

    /// <summary>
    /// Asynchronously determines whether a closing price exists for the specified futures data identifier,
    /// checking in-memory state first and falling back to the database when available.
    /// </summary>
    /// <param name="id">The identifier for the futures data to check for a closing price.</param>
    /// <returns>true if a closing price exists for the specified identifier; otherwise, false.</returns>
    internal async ValueTask<bool> FuturesClosingPriceExistsAsync(FuturesDataId id)
        => FuturesClosingPriceExists(id)
           || (_db is not null && await _db.GetFuturesClosingPriceAsync(id) is not null);

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
                FuturesClosingPriceInsertedEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;
    }

    /// <summary>
    /// futures closing price inserted
    /// </summary>
    /// <param name="e"></param>
    bool On(FuturesClosingPriceInsertedEvent e)
    {
        _closingPrice = e;
        return true;
    }
}