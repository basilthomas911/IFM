using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

[MessagePackObject]
public sealed record VolatilitySeriesGovernance(
    [property: Key(0)] DateTimeOffset EffectiveFromUtc,
    [property: Key(1)] DateTimeOffset? EffectiveUntilUtc,
    [property: Key(2)] string ApprovedConfigurationVersion,
    [property: Key(3)] string Owner,
    [property: Key(4)] string ApprovalEvidenceId,
    [property: Key(5)] ImmutableArray<string> CompatibleHistoricalMethodologyVersions);
