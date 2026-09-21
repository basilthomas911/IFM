using TomasAI.IFM.Domain.Portfolio.GeneralLedger;
using TomasAI.IFM.Domain.Trade.Futures.Option.Event.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Events;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Event;

public static class OptionTradeEndOfDayProcessed
{
    /// <summary>Continues completed option end-of-day processing at the Portfolio ledger boundary.</summary>
    public static async ValueTask ExecuteAsync(
        this OptionTradeEndOfDayProcessedEvent source,
        IFuturesOptionTradeEventContext context)
    {
        var request = new PortfolioTradeValuationRequest(
            source.FundId,
            source.OrderId,
            source.EntityId.TradeId,
            source.EodKey.ValueDate,
            source.TradePnl,
            source.Reference,
            source.Id,
            0,
            source.UpdatedOn.Kind == DateTimeKind.Utc
                ? source.UpdatedOn
                : DateTime.SpecifyKind(source.UpdatedOn, DateTimeKind.Utc));
        var result = await context.PortfolioValuation.PostAsync(request)
            .ConfigureAwait(false);
        if (result?.Success != true)
            throw new InvalidOperationException(
                result?.ErrorMessage ?? "Portfolio end-of-day valuation returned no result.");
    }
}
