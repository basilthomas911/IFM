using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;
/// <summary>Maps configured contract-root prefixes to roll-aware continuation identities.</summary>
public sealed class PrefixFuturesTradeSessionBarSeriesResolver(IReadOnlyDictionary<string, MarketSeriesIdentity> mappings) : IFuturesTradeSessionBarSeriesResolver
{
    readonly KeyValuePair<string, MarketSeriesIdentity>[] orderedMappings = [..mappings.OrderByDescending(x => x.Key.Length)];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "S3267", Justification = "This lookup runs on the realtime tick path; use a bounded early-return scan without a LINQ iterator or capturing predicate allocation.")]
    /// <inheritdoc/>
    public MarketSeriesIdentity Resolve(string contractId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        foreach (var mapping in orderedMappings)
            if (contractId.StartsWith(mapping.Key, StringComparison.OrdinalIgnoreCase))
                return mapping.Value;
        return MarketSeriesIdentity.ForContract(contractId);
    }
}
