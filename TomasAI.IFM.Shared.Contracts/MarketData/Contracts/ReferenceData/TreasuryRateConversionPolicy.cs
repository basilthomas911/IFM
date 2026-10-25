using MessagePack;

namespace TomasAI.IFM.Shared.MarketData.Contracts;

/// <summary>Reviewed source-series mapping. Evidence is required independently of numeric agreement.</summary>
/// <param name="Source">The source.</param>
/// <param name="SourceSeriesId">The source series id.</param>
/// <param name="Convention">The convention.</param>
/// <param name="Version">The version.</param>
/// <param name="EvidenceId">The evidence id.</param>
[MessagePackObject]
public sealed record TreasuryRateConversionPolicy(
    [property: Key(0)] string Source,
    [property: Key(1)] string SourceSeriesId,
    [property: Key(2)] TreasuryRateConvention Convention,
    [property: Key(3)] string Version,
    [property: Key(4)] string EvidenceId);
