using TomasAI.IFM.Domain.Trade.Futures.Trade.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Trade;
using TomasAI.IFM.Domain.Trade.Shared.Model;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Trade.Query.Extensions;

public static class FuturesTradeQueryHandlers
{
    public static async ValueTask ExecuteAsync(
        this GetFuturesTradeQuery query,
        IFuturesTradeQueryContext context,
        CancellationToken cancellationToken)
    {
        var value = await context.DbFactory.TradeDb
            .GetEstablishedTradeAsync(query.TradeId, cancellationToken)
            .ConfigureAwait(false);
        if (value?.AssetFamily != TradeAssetFamily.Futures) value = null;
        await context.ReplyAsync(
            query.Subject.ThreadId,
            query.Subject.Verb,
            new ServiceResult<EstablishedTradeDefinition?>(value)).ConfigureAwait(false);
    }
}
