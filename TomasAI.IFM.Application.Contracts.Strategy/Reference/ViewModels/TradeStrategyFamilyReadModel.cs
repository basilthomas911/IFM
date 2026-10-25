// LEGACY: retained for migration/replay and UI comparison only. Active authoring uses ConfigurationDb.
// Removal criteria: Domain.Reference/Docs/Strategy-Catalog-Legacy-Retirement.md.
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

/// <summary>Typed strategy definition: twelve original keys plus product identity and Exchange.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record TradeStrategyFamilyReadModel
{
    [Key(0)] public int TradeStrategyFamilyId { get; init; }
    [Key(1)] public long DefinitionVersion { get; init; }
    [Key(2)] public string SystemKey { get; init; } = string.Empty;
    [Key(3)] public TradeStrategyFamilyType Family { get; init; }
    [Key(4)] public TradeStrategyType Strategy { get; init; }
    [Key(5)] public TimeFrameType TimeFrame { get; init; }
    [Key(6)] public string Symbol { get; init; } = string.Empty;
    [Key(7)] public string Currency { get; init; } = string.Empty;
    [Key(8)] public string Description { get; init; } = string.Empty;
    [Key(9)] public TradeStrategyFamilyState State { get; init; }
    [Key(10)] public DateTime CreatedOnUtc { get; init; }
    [Key(11)] public string CreatedBy { get; init; } = string.Empty;
    // Zero/empty only on pre-product-catalog legacy definitions. New creation requires both.
    [Key(12)] public int TradeStrategySymbolId { get; init; }
    [Key(13)] public string Exchange { get; init; } = string.Empty;

    public static string ComposeSystemKey(TradeStrategyFamilyType family, TradeStrategyType strategy)
    {
        if (!Enum.IsDefined(family) || family == TradeStrategyFamilyType.Unknown)
            throw new ArgumentOutOfRangeException(nameof(family));
        if (!Enum.IsDefined(strategy) || strategy == TradeStrategyType.Unknown)
            throw new ArgumentOutOfRangeException(nameof(strategy));
        return $"{family}-{strategy}";
    }

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (TradeStrategyFamilyId <= 0 || DefinitionVersion <= 0) errors.Add("Positive family identity and definition version are required.");
        var validFamily = Enum.IsDefined(Family) && Family != TradeStrategyFamilyType.Unknown;
        var validStrategy = Enum.IsDefined(Strategy) && Strategy != TradeStrategyType.Unknown;
        if (!validFamily) errors.Add("A defined non-Unknown Family is required.");
        if (!validStrategy) errors.Add("A defined non-Unknown Strategy is required.");
        if (validFamily && validStrategy && !string.Equals(SystemKey, ComposeSystemKey(Family, Strategy), StringComparison.Ordinal))
            errors.Add("SystemKey must equal Family-Strategy using the exact enum names.");
        if (!TradeStrategyTimeFrames.IsAllowed(TimeFrame)) errors.Add("TimeFrame must be Daily, Weekly or Monthly.");
        if (string.IsNullOrWhiteSpace(Symbol) || !string.Equals(Symbol,Symbol.Trim(),StringComparison.Ordinal)) errors.Add("A non-empty trimmed Symbol is required.");
        if (Currency is null || Currency.Length != 3 || Currency.Any(c => c is < 'A' or > 'Z')) errors.Add("Currency must be a three-letter uppercase code.");
        if (string.IsNullOrWhiteSpace(Description)) errors.Add("Description is required.");
        if (TradeStrategySymbolId < 0 || (TradeStrategySymbolId > 0 && string.IsNullOrWhiteSpace(Exchange))) errors.Add("A linked product requires a positive ID and Exchange.");
        if (!Enum.IsDefined(State) || State == TradeStrategyFamilyState.Unknown) errors.Add("A defined non-Unknown State is required.");
        if (CreatedOnUtc == default || CreatedOnUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(CreatedBy)) errors.Add("UTC audit provenance is required.");
        return errors;
    }
}
