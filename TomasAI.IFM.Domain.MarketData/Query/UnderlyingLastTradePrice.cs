using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
namespace TomasAI.IFM.Domain.MarketData.Query;

/// <summary>Display last-trade identity, distinct from a freshness-qualified pricing mark or midpoint.</summary>
internal static class UnderlyingLastTradePrice
{
    internal static (decimal? Price, DateTimeOffset? At) Read(IMarketDataApi? api, string contractId)
    {
        if(api is null || !api.TryGetLastTickPrice(contractId, out var snapshot)
            || !string.Equals(snapshot.ContractId, contractId, StringComparison.Ordinal)
            || snapshot.Trade is not {} trade || trade.LastPrice <= 0
            || trade.NormalizedTradeAction is NormalizedTradeAction.Cancel or NormalizedTradeAction.Clear
            || (trade.NormalizedTradeConditionFlags & NormalizedTradeConditionFlags.UndefinedPrice) != 0)
            return (null,null);
        return (trade.LastPrice,trade.EventTimestamp);
    }
}
