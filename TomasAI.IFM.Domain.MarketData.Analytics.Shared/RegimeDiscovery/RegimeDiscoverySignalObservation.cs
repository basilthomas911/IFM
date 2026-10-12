using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;

/// <summary>Contains one immutable normalized metric and its complete provenance.</summary>
[MessagePackObject]
public sealed record RegimeDiscoverySignalObservation
{
    /// <summary>Gets the normalized metric.</summary>
    [Key(0)] public RegimeDiscoverySignalMetric Metric { get; init; }
    /// <summary>Gets the source analytics signal key.</summary>
    [Key(1)] public MarketAnalyticsSignalKey SignalKey { get; init; }
    /// <summary>Gets the normalized numeric value.</summary>
    [Key(2)] public decimal Value { get; init; }
    /// <summary>Gets the UTC market-data timestamp.</summary>
    [Key(3)] public DateTime MarketDataAsOfUtc { get; init; }
    /// <summary>Gets the UTC calculation timestamp.</summary>
    [Key(4)] public DateTime CalculatedAtUtc { get; init; }
    /// <summary>Gets the upstream source sequence.</summary>
    [Key(5)] public long SourceSequence { get; init; }
    /// <summary>Gets the serialized upstream schema version.</summary>
    [Key(6)] public ushort SchemaVersion { get; init; }
    /// <summary>Gets the upstream calculation version.</summary>
    [Key(7)] public string CalculationVersion { get; init; } = string.Empty;
    /// <summary>Gets whether upstream warm-up completed.</summary>
    [Key(8)] public bool IsWarm { get; init; }
    /// <summary>Gets whether the upstream value is valid.</summary>
    [Key(9)] public bool IsValid { get; init; }
    /// <summary>Gets the evaluated availability for this request.</summary>
    [Key(10)] public RegimeDiscoverySignalAvailability Availability { get; init; }
    /// <summary>Gets normalized freshness in the range zero through one.</summary>
    [Key(11)] public decimal FreshnessFactor { get; init; }
    /// <summary>Gets the stable upstream signal identity.</summary>
    [Key(12)] public string SignalIdentity { get; init; } = string.Empty;
}
