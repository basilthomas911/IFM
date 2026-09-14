using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetIronCondorTradePrice
{
    /// <summary>Reads the legacy Iron Condor price projection.</summary>
    public static async ValueTask ExecuteAsync(
        this GetIronCondorTradePriceQuery query,
        IFuturesOptionTradeQueryContext context,
        CancellationToken cancellationToken)
    {
        var result = await context.DbFactory.TradeDb
            .GetIronCondorTradePriceAsync(query.TradeId, query.ValueDate, cancellationToken)
            .ConfigureAwait(false);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<TradePriceReadModel?>(result)).ConfigureAwait(false);
    }
}
