using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.Trade.Order.Broker.Logging;
using TomasAI.IFM.Domain.Trade.Order.Broker.Realtime.Actor;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Realtime;

/// <summary>Handles one quote batch without creating a tick queue or database work.</summary>
public static class FuturesTickQuoteDataChanged
{
    /// <summary>Forwards only the newest valid quote and logs only a generated fill or a real failure.</summary>
    public static async ValueTask ExecuteAsync(this FuturesTickQuoteDataChangedEvent changed,
        IBrokerOrderRealtimeContext context)
    {
        if (changed.QuoteData.Count == 0) return;
        var latest = changed.QuoteData.Buffer[changed.QuoteData.Count - 1];
        if (latest.BidPrice is not { } bid || latest.AskPrice is not { } ask) return;
        var marketTimeUtc = DateTime.UnixEpoch.AddTicks(latest.EventTimestampNanoseconds / 100);
        var matched = await context.TradeBroker.PublishMarketQuoteAsync(new BrokerMarketQuote(
            changed.TickDataId.ContractId, bid, ask, checked((int)latest.BidSize),
            checked((int)latest.AskSize), marketTimeUtc, changed.DefinitionDate.DayNumber,
            latest.SourceSequence)).ConfigureAwait(false);
        if (matched > 0)
            BrokerOrderQuoteLogging.Matched(context.Logger, changed.TickDataId.ContractId, matched);
    }
}
