using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

public enum TradeStrategyFamilyState { Unknown = 0, Active = 1, Retired = 2 }
public enum TradeStrategyType { Unknown = 0, Futures = 1, IronCondor = 2, VerticalSpread = 3 }

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
        if (string.IsNullOrWhiteSpace(Symbol) || Symbol != Symbol.Trim()) errors.Add("A non-empty trimmed Symbol is required.");
        if (Currency is null || Currency.Length != 3 || Currency.Any(c => c is < 'A' or > 'Z')) errors.Add("Currency must be a three-letter uppercase code.");
        if (string.IsNullOrWhiteSpace(Description)) errors.Add("Description is required.");
        if (TradeStrategySymbolId < 0 || (TradeStrategySymbolId > 0 && string.IsNullOrWhiteSpace(Exchange))) errors.Add("A linked product requires a positive ID and Exchange.");
        if (!Enum.IsDefined(State) || State == TradeStrategyFamilyState.Unknown) errors.Add("A defined non-Unknown State is required.");
        if (CreatedOnUtc == default || CreatedOnUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(CreatedBy)) errors.Add("UTC audit provenance is required.");
        return errors;
    }
}

/// <summary>The strategy UI subset, not the full market-data timeframe enumeration.</summary>
public static class TradeStrategyTimeFrames
{
    public static IReadOnlyList<TimeFrameType> Allowed { get; } = Array.AsReadOnly(new[] { TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly });
    public static bool IsAllowed(TimeFrameType value) => value is TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly;
    public static bool TryParseName(string? name, out TimeFrameType value)
    {
        foreach (var candidate in Allowed)
            if (string.Equals(name, candidate.ToString(), StringComparison.Ordinal)) { value = candidate; return true; }
        value = TimeFrameType.None;
        return false;
    }
}

public sealed record TradeStrategyFamilySeedDefinition(
    string LegacySystemKey, TradeStrategyFamilyType Family, TradeStrategyType Strategy,
    TimeFrameType TimeFrame, string Symbol, string Currency, string Description)
{
    public string SystemKey => TradeStrategyFamilyReadModel.ComposeSystemKey(Family, Strategy);
    public TradeStrategyFamilyReadModel Create(int id, DateTime createdOnUtc, string createdBy, long version = 1) => new()
    {
        TradeStrategyFamilyId = id, DefinitionVersion = version, SystemKey = SystemKey,
        Family = Family, Strategy = Strategy, TimeFrame = TimeFrame, Symbol = Symbol, Currency = Currency,
        Description = Description, State = TradeStrategyFamilyState.Active, CreatedOnUtc = createdOnUtc, CreatedBy = createdBy,
    };
}

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
