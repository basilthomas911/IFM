using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.Trade.Order.Broker.Realtime.Actor;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;

/// <summary>Feeds the execution book the same original option quotes used for position marking.</summary>
internal static class OptionQuoteForwarding
{
    internal static async ValueTask ForwardOptionQuoteAsync(this FuturesTickTradeDataChangedEvent changed, IBrokerOrderRealtimeContext context)
    {
        var tick=changed.OptionMarketPriceObservation?.OptionTickData;
        if(tick is null || changed.OptionMarketPriceObservation!.PriceBasis != OptionMarketPriceBasis.QuoteMidpoint
            || changed.OptionMarketPriceObservation.SourceSequence <= 0
            || tick.IsTradeObservation || tick.IsPricingUpdate || tick.SourceGeneration==Guid.Empty
            || tick.ObservedAtUtc==default
            || string.IsNullOrWhiteSpace(tick.ContractId) || !double.IsFinite(tick.BidPrice) || !double.IsFinite(tick.AskPrice)
            || tick.BidPrice<=0 || tick.AskPrice<tick.BidPrice || tick.BidSize<0 || tick.AskSize<0)return;
        var epoch=BitConverter.ToInt64(tick.SourceGeneration.ToByteArray(),0)&long.MaxValue;
        await context.TradeBroker.PublishMarketQuoteAsync(new BrokerMarketQuote(tick.ContractId,(decimal)tick.BidPrice,
            (decimal)tick.AskPrice,tick.BidSize,tick.AskSize,tick.ObservedAtUtc.UtcDateTime,epoch,
            changed.OptionMarketPriceObservation!.SourceSequence)).ConfigureAwait(false);
    }
}
