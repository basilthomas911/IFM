using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event;

/// <summary>Resolves the observable price used by the 15-second ES and VX UI bars.</summary>
internal static class FuturesBarMarketPrice
{
    internal static bool TryResolve(
        string symbol,
        FuturesMarketPriceSnapshot snapshot,
        out decimal price)
    {
        if (StringComparer.Ordinal.Equals(symbol, "ES"))
            return TryGetTrade(snapshot.Trade, out price);

        if (!StringComparer.Ordinal.Equals(symbol, "VX"))
        {
            price = default;
            return false;
        }

        var trade = snapshot.Trade;
        var hasTrade = trade is { LastPrice: > 0m };
        var hasQuote = TryGetMidpoint(snapshot.Quote, out var midpoint, out var quoteTimestamp);
        if (hasQuote && (!hasTrade || quoteTimestamp > trade!.Value.EventTimestamp))
        {
            price = midpoint;
            return true;
        }

        if (hasTrade)
        {
            price = trade!.Value.LastPrice;
            return true;
        }

        price = default;
        return false;
    }

    static bool TryGetTrade(FuturesMarketTradeSnapshot? snapshot, out decimal price)
    {
        if (snapshot is { LastPrice: > 0m } trade)
        {
            price = trade.LastPrice;
            return true;
        }

        price = default;
        return false;
    }

    static bool TryGetMidpoint(
        FuturesMarketQuoteSnapshot? snapshot,
        out decimal midpoint,
        out DateTimeOffset timestamp)
    {
        if (snapshot is { BidPrice: > 0m, AskPrice: > 0m } quote
            && quote.BidPrice <= quote.AskPrice)
        {
            midpoint = quote.BidPrice.Value
                + ((quote.AskPrice.Value - quote.BidPrice.Value) / 2m);
            timestamp = quote.EventTimestamp;
            return true;
        }

        midpoint = default;
        timestamp = default;
        return false;
    }
}
