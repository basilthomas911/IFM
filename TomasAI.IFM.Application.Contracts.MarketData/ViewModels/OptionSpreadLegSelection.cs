namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>One-sided limits for automatic option-spread selection.</summary>
public static class OptionSpreadLegSelection
{
    /// <summary>Ranks usable OTM legs by absolute delta descending, with |delta| ? target. Delta uses fractions, not percent.</summary>
    public static IOrderedEnumerable<EvaluatedOptionContractReadModel> ShortCandidates(
        IEnumerable<EvaluatedOptionContractReadModel> contracts, bool isCall, decimal targetDelta,
        decimal? underlyingPrice) => ShortCandidates(contracts, isCall, targetDelta, underlyingPrice, false);

    /// <summary>Ranks legs for live selection or an explicitly authorized frozen emulator preview.</summary>
    /// <param name="allowFrozenEmulatorPreview">Opt in only for a confirmed frozen emulator snapshot; live callers retain stale-quote rejection.</param>
    public static IOrderedEnumerable<EvaluatedOptionContractReadModel> ShortCandidates(
        IEnumerable<EvaluatedOptionContractReadModel> contracts, bool isCall, decimal targetDelta,
        decimal? underlyingPrice, bool allowFrozenEmulatorPreview)
    {
        var maximum = (double)targetDelta;
        return contracts.Where(x => x.IsCall == isCall && (!x.IsStale || allowFrozenEmulatorPreview) && (x.GreeksValid || x.SelectionValid)
                && x.Delta is { } delta && double.IsFinite(delta)
                && (isCall ? delta > 0 : delta < 0) && Math.Abs(delta) < .5 && Math.Abs(delta) <= maximum)
            .OrderByDescending(x => Math.Abs(x.Delta!.Value))
            .ThenBy(x => underlyingPrice is { } price ? Math.Abs(x.Strike - price) : 0m)
            .ThenBy(x => x.ContractId, StringComparer.Ordinal);
    }

    /// <summary>Selects the nearest strike in the requested direction with width ? minimumWidth. Missing wings return null.</summary>
    public static string? Wing(IEnumerable<(string ContractId, decimal Strike)> contracts,
        decimal shortStrike, decimal minimumWidth, bool upward)
    {
        if (minimumWidth <= 0) return null;
        return contracts.Where(x => upward ? x.Strike - shortStrike >= minimumWidth : shortStrike - x.Strike >= minimumWidth)
            .OrderBy(x => Math.Abs(x.Strike - shortStrike))
            .ThenBy(x => x.ContractId, StringComparer.Ordinal)
            .Select(x => x.ContractId).FirstOrDefault();
    }
}
