using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetOptionTradeSpreadBarData
{
    /// <summary>Reads legacy option spread bars for the requested date range.</summary>
    public static async ValueTask ExecuteAsync(
        this GetOptionTradeSpreadBarDataQuery query,
        IFuturesOptionTradeQueryContext context,
        CancellationToken cancellationToken)
    {
        OptionTradeSpreadBarsDataModel[] result = [.. await context.DbFactory.TradeDb
            .GetOptionTradeSpreadBarDataAsync(query.OrderId, query.TradeId, query.ValueDate,
                query.TradeType, query.StartDate, query.EndDate, cancellationToken)
            .ConfigureAwait(false)];
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<OptionTradeSpreadBarsDataModel[]>(result)).ConfigureAwait(false);
    }
}
