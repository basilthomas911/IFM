using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;

/// <summary>Reads a volatility instrument from the resident futures cache; no subscription or database read.</summary>
public static class VolatilityMarketPriceReader
{
    public static decimal? Read(IMarketDataApi market, string symbol, DateOnly valueDate, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(market);
        if (nowUtc.Kind != DateTimeKind.Utc) return null;
        if (!market.TryGetOnTheRunFuturesContract(symbol, out var contract)
            || !market.TryGetLastTickPrice(contract.ContractId, out var value)
            || value.ContractId != contract.ContractId || value.ValueDate != valueDate) return null;
        return Resolve(value, new DateTimeOffset(nowUtc));
    }

    public static decimal? Resolve(FuturesMarketPriceSnapshot value, DateTimeOffset now)
    {
        var trade = value.Trade;
        var hasTrade = trade is { LastPrice: > 0 } t && t.EventTimestamp != default
            && t.EventTimestamp.Offset == TimeSpan.Zero && t.EventTimestamp <= now;
        var quote = value.Quote;
        var hasQuote = quote is { BidPrice: > 0, AskPrice: > 0 } q && q.AskPrice >= q.BidPrice
            && q.EventTimestamp != default && q.EventTimestamp.Offset == TimeSpan.Zero && q.EventTimestamp <= now;
        if (hasQuote && (!hasTrade || quote!.Value.EventTimestamp > trade!.Value.EventTimestamp))
            return quote!.Value.BidPrice + (quote.Value.AskPrice - quote.Value.BidPrice) / 2;
        return hasTrade ? trade!.Value.LastPrice : null;
    }
}
