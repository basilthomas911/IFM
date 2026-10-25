// LEGACY: retained for migration/replay and UI comparison only. Active authoring uses ConfigurationDb.
// Removal criteria: Domain.Reference/Docs/Strategy-Catalog-Legacy-Retirement.md.
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

public static class TradeStrategyFamilySeed
{
    public static IReadOnlyList<TradeStrategyFamilySeedDefinition> Definitions { get; } = Array.AsReadOnly(new[]
    {
        new TradeStrategyFamilySeedDefinition("FUTURES", TradeStrategyFamilyType.Futures, TradeStrategyType.Futures, TimeFrameType.Daily, "ES", "USD", "Daily ES futures"),
        new TradeStrategyFamilySeedDefinition("VERTICAL_SPREAD", TradeStrategyFamilyType.FuturesOption, TradeStrategyType.VerticalSpread, TimeFrameType.Weekly, "ES", "USD", "Weekly ES futures option vertical spread"),
        new TradeStrategyFamilySeedDefinition("IRON_CONDOR", TradeStrategyFamilyType.FuturesOption, TradeStrategyType.IronCondor, TimeFrameType.Monthly, "ES", "USD", "Monthly ES futures option iron condor"),
    });
    public static void Validate(IReadOnlyCollection<TradeStrategyFamilyReadModel> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count != Definitions.Count || rows.Select(x => x.TradeStrategyFamilyId).Distinct().Count() != rows.Count)
            throw new InvalidOperationException("The catalog must contain exactly three distinct definition identities.");
        foreach (var definition in Definitions)
        {
            var matches = rows.Where(x => string.Equals(x.SystemKey, definition.SystemKey, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1 || matches[0].Validate().Count != 0 ||
                matches[0] != definition.Create(matches[0].TradeStrategyFamilyId, matches[0].CreatedOnUtc, matches[0].CreatedBy))
                throw new InvalidOperationException($"The TradeStrategyFamily definition {definition.SystemKey} is not canonical.");
        }
    }
}
