// LEGACY: retained for migration/replay and UI comparison only. Active authoring uses ConfigurationDb.
// Removal criteria: Domain.Reference/Docs/Strategy-Catalog-Legacy-Retirement.md.
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

/// <summary>The strategy UI subset, not the full market-data timeframe enumeration.</summary>
public static class TradeStrategyTimeFrames
{
    public static IReadOnlyList<TimeFrameType> Allowed { get; } = Array.AsReadOnly(new[] { TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly });
    public static bool IsAllowed(TimeFrameType value) => value is TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly;
    public static bool TryParseName(string? name, out TimeFrameType value)
    {
        value = Allowed.FirstOrDefault(candidate => string.Equals(name, candidate.ToString(), StringComparison.Ordinal));
        return value != TimeFrameType.None;
    }
}
