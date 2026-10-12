using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

[MessagePackObject]
public sealed record VolatilityObservationProvenance(
    [property: Key(0)] ImmutableArray<VolatilityContributor> Contributors,
    [property: Key(1)] string PricerVersion,
    [property: Key(2)] string InputDigest,
    [property: Key(3)] string SourceMethodologyEvidenceId);
