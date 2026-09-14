using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetOptionTradeSpreadData
{
    /// <summary>Reads the latest legacy spread projection for an option trade.</summary>
    public static async ValueTask ExecuteAsync(
        this GetOptionTradeSpreadDataQuery query,
        IFuturesOptionTradeQueryContext context,
        CancellationToken cancellationToken)
    {
        var result = await context.DbFactory.TradeDb.GetOptionTradeSpreadDataAsync(
            query.OrderId, query.TradeId, query.ValueDate, query.TradeType, cancellationToken)
            .ConfigureAwait(false);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<OptionTradeSpreadsDataModel?>(result)).ConfigureAwait(false);
    }
}
