using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Query;

/// <summary>Separates a displayed window from buffered physical subscription coverage.</summary>
public static class OptionChainSubscriptionCoverage
{
    /// <summary>Adds 25 percent of the displayed range on each side, or the wing width if larger.</summary>
    public static FuturesOptionContractReadModel[] Buffer(FuturesOptionContractReadModel[] definitions,
        OptionChainStrikeWindowResult window, decimal wingWidth)
    {
        if (window.Contracts.Length == 0) return [];
        var lower = window.Contracts.Min(x => (decimal)x.StrikePrice);
        var upper = window.Contracts.Max(x => (decimal)x.StrikePrice);
        var margin = Math.Max(Math.Max(0, wingWidth), Math.Max(5m, (upper - lower) * 0.25m));
        var ids = window.Contracts.Select(x => x.ContractId).ToHashSet(StringComparer.Ordinal);
        var buffered = definitions.Where(x => double.IsFinite(x.StrikePrice) && x.StrikePrice > 0
                && ((decimal)x.StrikePrice >= lower - margin && (decimal)x.StrikePrice <= upper + margin
                    || ids.Contains(x.ContractId)))
            .DistinctBy(x => x.ContractId).OrderBy(x => x.StrikePrice).ThenBy(x => x.ContractId, StringComparer.Ordinal).ToArray();
        // The complete requested scope must never be clipped to satisfy the worker's bound.
        return buffered.Length <= 2048 ? buffered : window.Contracts;
    }

    /// <summary>Requires coverage of every requested ID with its exact immutable mapping and digest.</summary>
    public static bool Covers(IReadOnlyDictionary<string, (string MappingVersion, string DefinitionDigest)> subscribed,
        IReadOnlyList<FuturesOptionContractReadModel> requested)
    {
        foreach (var contract in requested)
            if (!subscribed.TryGetValue(contract.ContractId, out var identity)
                || identity.MappingVersion != contract.MappingVersion || identity.DefinitionDigest != contract.DefinitionDigest)
                return false;
        return true;
    }
}
