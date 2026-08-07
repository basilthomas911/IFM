using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Option.Query;

internal static class GetIronCondorTradePrice
{
    /// <summary>
    /// Gets the price of an Iron Condor trade based on the provided query.
    /// </summary>
    /// <param name="q"> </param>
    /// <param name="context"> </param>
    /// <param name="dbFactory"> </param>
    /// <returns></returns>
    internal static async ValueTask<TradePriceReadModel?> GetIronCondorTradePriceAsync(
        this GetIronCondorTradePriceQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => await (cancellationToken.CanBeCanceled
            ? dbFactory.TradeDb.GetIronCondorTradePriceAsync(
                q.TradeId, q.ValueDate, cancellationToken)
            : dbFactory.TradeDb.GetIronCondorTradePriceAsync(q.TradeId, q.ValueDate));
}
