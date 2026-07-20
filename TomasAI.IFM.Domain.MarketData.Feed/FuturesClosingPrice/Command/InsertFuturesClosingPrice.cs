using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command;

public static class InsertFuturesClosingPrice
{
    /// <summary>
    /// Handle an <see cref="InsertFuturesClosingPriceCommand"/> by building the corresponding
    /// <see cref="FuturesClosingPriceInsertedEvent"/> and updating the actor state.
    /// </summary>
    public static bool Execute(this InsertFuturesClosingPriceCommand e, FuturesClosingPriceCommandState state)
        => e switch
        {
            _ when state.FuturesClosingPriceExists(e.FuturesClosingPriceId) => throw new InsertFuturesClosingPriceException(e.InsertFuturesClosingPriceErrorMsg()),
            _ => state.Update(e.CreateFuturesClosingPriceInsertedEvent(), e)
        };

    /// <summary>
    /// Builds the error message for a duplicate insert attempt.
    /// </summary>
    internal static string InsertFuturesClosingPriceErrorMsg(this InsertFuturesClosingPriceCommand e)
        => $"{e.CommandName}: futures {e.FuturesClosingPriceId.ContractId} closing price for {e.FuturesClosingPriceId.ValueDate:yyyy-MM-dd} already exists";

    /// <summary>
    /// Creates a <see cref="FuturesClosingPriceInsertedEvent"/> from an <see cref="InsertFuturesClosingPriceCommand"/>.
    /// </summary>
    internal static FuturesClosingPriceInsertedEvent CreateFuturesClosingPriceInsertedEvent(this InsertFuturesClosingPriceCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesClosingPriceInsertedEvent.Actor, FuturesClosingPriceInsertedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            FuturesClosingPriceId = e.FuturesClosingPriceId,
            ClosingPrice = e.ClosingPrice,
            CreatedOn = e.OriginatedOn,
            CreatedBy = e.OriginatedBy
        };
}
