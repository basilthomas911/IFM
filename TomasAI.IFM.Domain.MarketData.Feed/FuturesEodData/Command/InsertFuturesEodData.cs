using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Model;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command;

public static class InsertFuturesEodData
{
    /// <summary>
    /// Handle an <see cref="InsertFuturesEodDataCommand"/> by computing the EOD analytics via
    /// <see cref="FuturesEodDataModel.CreateFuturesEodData"/> and building the corresponding
    /// <see cref="FuturesEodDataInsertedEvent"/> to update the actor state.
    /// </summary>
    public static bool Execute(this InsertFuturesEodDataCommand e, FuturesEodDataCommandState state)
    {
        var futuresEodData = e.CreateFuturesEodData(e.ValueDate, e.FuturesTickData, e.Contract, e.EodDataToday, e.EodDataRange, e.NormCurveData, e.WindowSize, e.VixEodData);
        return state.Update(e.CreateFuturesEodDataInsertedEvent(futuresEodData), e);
    }

    /// <summary>
    /// Creates a <see cref="FuturesEodDataInsertedEvent"/> from an <see cref="InsertFuturesEodDataCommand"/> and the
    /// computed <see cref="FuturesEodDataV2ReadModel"/>.
    /// </summary>
    /// <param name="e">The source insert command containing entity identifiers and origin metadata.</param>
    /// <param name="futuresEodData">The computed EOD data payload to embed in the event.</param>
    /// <returns>A fully-populated inserted event ready to be applied to actor state.</returns>
    internal static FuturesEodDataInsertedEvent CreateFuturesEodDataInsertedEvent(this InsertFuturesEodDataCommand e, FuturesEodDataV2ReadModel futuresEodData)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesEodDataInsertedEvent.Actor, FuturesEodDataInsertedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            FuturesEodData = futuresEodData,
            CreatedOn = e.OriginatedOn,
            CreatedBy = e.OriginatedBy
        };

}
