using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;

/// <summary>Contains one immutable, revision-stable Regime Discovery market snapshot.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryMarketSignalSnapshot
{
    /// <summary>Gets the unique snapshot identity.</summary>
    [Key(0)] public Guid SnapshotId { get; init; }
    /// <summary>Gets the monotonic cache revision captured atomically.</summary>
    [Key(1)] public long CacheRevision { get; init; }
    /// <summary>Gets the provider-neutral market series.</summary>
    [Key(2)] public MarketSeriesIdentity MarketSeriesIdentity { get; init; }
    /// <summary>Gets the single target workflow horizon.</summary>
    [Key(3)] public TimeFrameType TargetHorizon { get; init; }
    /// <summary>Gets the UTC snapshot capture timestamp.</summary>
    [Key(4)] public DateTime CapturedAtUtc { get; init; }
    /// <summary>Gets the latest included market-data timestamp.</summary>
    [Key(5)] public DateTime MarketDataAsOfUtc { get; init; }
    /// <summary>Gets accepted and unavailable observations in deterministic request order.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public model contract.")]
    [Key(6)] public RegimeDiscoverySignalObservation[] Observations { get; init; } = [];
}
