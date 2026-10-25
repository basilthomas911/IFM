using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

/// <summary>Immutable, versioned definition of one comparable option-IV series.</summary>
[MessagePackObject]
public sealed record VolatilitySeriesDefinition(
    [property: Key(0)] ushort SchemaVersion,
    [property: Key(1)] VolatilitySeriesIdentity Identity,
    [property: Key(2)] string UnderlyingRoot,
    [property: Key(3)] string Venue,
    [property: Key(4)] string Currency,
    [property: Key(5)] VolatilityDataIdentity DataIdentity,
    [property: Key(6)] VolatilityTenorConvention Tenor,
    [property: Key(7)] VolatilityPricingConvention Pricing,
    [property: Key(8)] VolatilityQualificationPolicy Qualification,
    [property: Key(9)] VolatilitySeriesConstructionPolicy Construction,
    [property: Key(10)] VolatilityMetricPolicy Metrics,
    [property: Key(11)] VolatilitySeriesGovernance Governance)
{
    public const ushort CurrentSchemaVersion = 1;
}
