using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Databento;

public static class OptionExpiryCalendarPolicy
{
    public static readonly (string Family, string[] Roots)[] EsRoots =
    [
        ("Daily-Monday", ["E1A", "E2A", "E3A", "E4A", "E5A"]),
        ("Daily-Tuesday", ["E1B", "E2B", "E3B", "E4B", "E5B"]),
        ("Daily-Wednesday", ["E1C", "E2C", "E3C", "E4C", "E5C"]),
        ("Daily-Thursday", ["E1D", "E2D", "E3D", "E4D", "E5D"]),
        ("Weekly-Friday", ["EW1", "EW2", "EW3", "EW4"]),
        ("End-Of-Month", ["EW"]),
        ("Quarterly-Serial", ["ES"])
    ];

    public static IReadOnlyList<(string Family, string[] Roots)> GetRoots(string symbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var normalized = symbol.Trim().ToUpperInvariant();
        return normalized == "ES" ? EsRoots : [("Quarterly-Serial", [normalized])];
    }

    public static DateOnly CalculateCoverageThrough(
        DateOnly valueDate,
        IReadOnlyCollection<FuturesContractV3ReadModel> contracts)
    {
        ArgumentNullException.ThrowIfNull(contracts);
        var futures = contracts.Where(contract => contract.LastTradeDate >= valueDate)
            .OrderBy(contract => contract.LastTradeDate).ToArray();
        if (futures.Length == 0) throw new InvalidOperationException("No current or future futures contracts exist.");
        var onTheRunIndex = Array.FindIndex(futures, contract => contract.OnTheRun);
        if (onTheRunIndex < 0) onTheRunIndex = 0;
        var followingIndex = Math.Min(onTheRunIndex + 1, futures.Length - 1);
        return futures[followingIndex].LastTradeDate;
    }
}
