using System.Collections.Immutable;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;

/// <summary>Accepted direction of the option strategy. Catalog Balanced maps explicitly to Neutral.</summary>
public enum OptionStrategyMarketBias { Neutral, Bullish, Bearish }

/// <summary>Global versioned option-universe and construction settings; no fund overrides are implied.</summary>
[MessagePackObject]
public sealed record StrategyOptionChainParameterSet
{
    /// <summary>Stable identity of the global set.</summary>
    [Key(0)] public Guid ParameterSetId { get; init; }
    /// <summary>Immutable published policy version.</summary>
    [Key(1)] public int Version { get; init; } = 1;
    /// <summary>Business label.</summary>
    [Key(2)] public string Name { get; init; } = string.Empty;
    /// <summary>Exact strategy catalog structure identity.</summary>
    [Key(3)] public Guid StrategyDefinitionId { get; init; }
    /// <summary>Exact version of the referenced structure.</summary>
    [Key(4)] public int StrategyDefinitionVersion { get; init; }
    /// <summary>Canonical futures root, initially ES.</summary>
    [Key(5)] public string InstrumentRoot { get; init; } = "ES";
    /// <summary>Explicit environment; development parameters are not production defaults.</summary>
    [Key(6)] public string Environment { get; init; } = "Development";
    /// <summary>Whether background maintenance should prepare this strategy.</summary>
    [Key(7)] public bool Enabled { get; init; }
    /// <summary>All three explicitly configured directions.</summary>
    [Key(8)] public ImmutableArray<OptionStrategyBiasParameters> BiasRows { get; init; } = [];
    /// <summary>Volatility envelope multiplier: F * IV * sqrt(T) * multiplier.</summary>
    [Key(9)] public double CacheRangeMultiplier { get; init; } = 2;
    /// <summary>Hard subscription bound; incomplete coverage cannot be called ready.</summary>
    [Key(10)] public int MaximumContracts { get; init; } = 2048;
    /// <summary>Hard number of expirations maintained for this strategy.</summary>
    [Key(11)] public int MaximumExpirations { get; init; } = 3;
    /// <summary>Wire schema of the configuration.</summary>
    [Key(12)] public int SchemaVersion { get; init; } = 1;

    /// <summary>Validates complete global bias rows and finite bounded coverage before publication.</summary>
    public void Validate()
    {
        if (SchemaVersion != 1 || ParameterSetId == Guid.Empty
            || Version < 1 || Enabled && (StrategyDefinitionId == Guid.Empty || StrategyDefinitionVersion < 1)
            || !Enabled && (StrategyDefinitionId == Guid.Empty ? StrategyDefinitionVersion != 0 : StrategyDefinitionVersion < 1) || string.IsNullOrWhiteSpace(Name)
            || InstrumentRoot != "ES" || Environment is not ("Development" or "Paper" or "Production")
            || !double.IsFinite(CacheRangeMultiplier) || CacheRangeMultiplier is < 1 or > 5
            || MaximumContracts is < 4 or > 2048 || MaximumExpirations is < 1 or > 16
            || BiasRows.IsDefault || BiasRows.Length != 3
            || BiasRows.Any(x => x is null)
            || BiasRows.Select(x => x.MarketBias).Distinct().Count() != 3)
            throw new ArgumentException("Invalid global strategy option-chain parameter set.");
        foreach (var row in BiasRows) row.Validate();
    }

    /// <summary>Serializes the complete validated global policy.</summary>
    public string Serialize() { Validate(); return JsonSerializer.Serialize(this); }
    /// <summary>Fingerprints exact policy inputs with SHA256.</summary>
    public string Hash() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize()))).ToLowerInvariant();
    /// <summary>Reads and validates persisted policy JSON; unknown fields are rejected.</summary>
    public static StrategyOptionChainParameterSet Read(string json)
    {
        var result = JsonSerializer.Deserialize<StrategyOptionChainParameterSet>(json,
            new JsonSerializerOptions { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow })
            ?? throw new ArgumentException("Missing global chain policy.");
        result.Validate(); return result;
    }
}

/// <summary>Direction-specific expiry, per-contract delta and wing requirements in explicit units.</summary>
[MessagePackObject]
public sealed record OptionStrategyBiasParameters
{
    /// <summary>Neutral, Bullish or Bearish.</summary>
    [Key(0)] public OptionStrategyMarketBias MarketBias { get; init; }
    /// <summary>Explicit eligibility of this bias for the catalog strategy.</summary>
    [Key(1)] public bool Enabled { get; init; } = true;
    /// <summary>Inclusive minimum calendar days to expiry from operational value date.</summary>
    [Key(2)] public int MinimumDte { get; init; }
    /// <summary>Inclusive maximum calendar days to expiry.</summary>
    [Key(3)] public int MaximumDte { get; init; }
    /// <summary>Preferred expiry distance within the eligible range.</summary>
    [Key(4)] public int PreferredDte { get; init; }
    /// <summary>Absolute put delta selection range and target; calculated put delta remains signed.</summary>
    [Key(5)] public OptionDeltaRange PutDelta { get; init; } = new(0, 0, 0);
    /// <summary>Absolute call delta selection range and target.</summary>
    [Key(6)] public OptionDeltaRange CallDelta { get; init; } = new(0, 0, 0);
    /// <summary>Allowed put-wing strike differences in underlying price points.</summary>
    [Key(7)] public ImmutableArray<decimal> PutWingWidths { get; init; } = [];
    /// <summary>Allowed call-wing strike differences in underlying price points.</summary>
    [Key(8)] public ImmutableArray<decimal> CallWingWidths { get; init; } = [];
    /// <summary>Signed aggregate delta per strategy unit using catalog sides and ratios.</summary>
    [Key(9)] public decimal TargetNetDelta { get; init; }
    /// <summary>Maximum absolute distance from target aggregate delta.</summary>
    [Key(10)] public decimal NetDeltaTolerance { get; init; }
    /// <summary>Maximum quote age in milliseconds for candidate execution evidence.</summary>
    [Key(11)] public int MaximumQuoteAgeMilliseconds { get; init; } = 1000;
    /// <summary>Maximum event timestamp skew between required legs and underlying.</summary>
    [Key(12)] public int MaximumQuoteSkewMilliseconds { get; init; } = 250;
    /// <summary>Minimum conservative credit in premium points per strategy unit.</summary>
    [Key(13)] public decimal MinimumNetCredit { get; init; }
    /// <summary>Maximum debit in premium points per strategy unit; null means inapplicable.</summary>
    [Key(14)] public decimal? MaximumNetDebit { get; init; }
    /// <summary>Minimum displayed executable-side size in contracts.</summary>
    [Key(15)] public int MinimumQuoteSize { get; init; } = 1;
    /// <summary>Maximum allowed leg bid/ask spread in premium points.</summary>
    [Key(16)] public decimal MaximumLegSpreadPoints { get; init; }
    /// <summary>Bounded shortlist size per option side before attaching wings.</summary>
    [Key(17)] public int MaximumCandidatesPerSide { get; init; } = 8;

    /// <summary>Validates expiry ordering, delta units, distinct wings and explicit quality bounds.</summary>
    public void Validate()
    {
        if (!Enum.IsDefined(MarketBias) || MinimumDte < 1 || MaximumDte < MinimumDte || MaximumDte > 730
            || PreferredDte < MinimumDte || PreferredDte > MaximumDte
            || PutWingWidths.IsDefault || CallWingWidths.IsDefault
            || PutWingWidths.Length > 16 || CallWingWidths.Length > 16
            || PutWingWidths.Concat(CallWingWidths).Any(x => x <= 0 || x > 10000)
            || PutWingWidths.Distinct().Count() != PutWingWidths.Length || CallWingWidths.Distinct().Count() != CallWingWidths.Length
            || NetDeltaTolerance is < 0 or > 4 || TargetNetDelta is < -4 or > 4
            || MaximumQuoteAgeMilliseconds is < 1 or > 5000 || MaximumQuoteSkewMilliseconds is < 0 or > 2000
            || MinimumNetCredit < 0 || MaximumNetDebit is <= 0 || MinimumQuoteSize < 1
            || MaximumLegSpreadPoints <= 0 || MaximumCandidatesPerSide is < 1 or > 32)
            throw new ArgumentException("Invalid strategy bias parameters.");
        if (PutDelta is null || CallDelta is null) throw new ArgumentException("Delta ranges are required.");
        PutDelta.Validate(); CallDelta.Validate();
    }
}

/// <summary>Absolute option delta magnitudes, each between zero and one, with a target inside the range.</summary>
[MessagePackObject]
public sealed record OptionDeltaRange([property: Key(0)] decimal Minimum,
    [property: Key(1)] decimal Target, [property: Key(2)] decimal Maximum)
{
    /// <summary>Validates ordered delta magnitudes.</summary>
    public void Validate()
    {
        if (Minimum < 0 || Maximum > 1 || Minimum > Target || Target > Maximum)
            throw new ArgumentException("Invalid absolute option delta range.");
    }
}
