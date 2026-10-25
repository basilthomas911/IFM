using System.Collections.Immutable;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;

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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "The existing model/command Validate method validates object properties, not method parameters. Preserve ArgumentException and property-specific diagnostic labels consumed by validation/error adapters.")]
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
