using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;

/// <summary>Reviewed source-series mapping. Evidence is required independently of numeric agreement.</summary>
[MessagePackObject]
public sealed record TreasuryRateConversionPolicy(
    [property: Key(0)] string Source,
    [property: Key(1)] string SourceSeriesId,
    [property: Key(2)] TreasuryRateConvention Convention,
    [property: Key(3)] string Version,
    [property: Key(4)] string EvidenceId);
