using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Records one normalized input or calculation fact used by Regime Discovery.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryEvidence
{
    /// <summary>Gets the calculation area that produced the evidence.</summary>
    [Key(0)] public RegimeEvidenceArea Area { get; init; }
    /// <summary>Gets the stable evidence identity.</summary>
    [Key(1)] public string EvidenceId { get; init; } = string.Empty;
    /// <summary>Gets the originating signal family when the evidence represents a signal.</summary>
    [Key(2)] public MarketAnalyticsSignalKind SignalKind { get; init; }
    /// <summary>Gets the observation timeframe.</summary>
    [Key(3)] public TimeFrameType TimeFrame { get; init; }
    /// <summary>Gets the normalized signed or unsigned evidence value.</summary>
    [Key(4)] public decimal Value { get; init; }
    /// <summary>Gets the configured evidence weight.</summary>
    [Key(5)] public decimal Weight { get; init; }
    /// <summary>Gets the normalized freshness factor.</summary>
    [Key(6)] public decimal FreshnessFactor { get; init; }
    /// <summary>Gets whether the evidence is required.</summary>
    [Key(7)] public bool IsRequired { get; init; }
    /// <summary>Gets whether the evidence was available and accepted.</summary>
    [Key(8)] public bool IsAvailable { get; init; }
    /// <summary>Gets the UTC source market-data timestamp.</summary>
    [Key(9)] public DateTime MarketDataAsOfUtc { get; init; }
    /// <summary>Gets the stable source signal identity.</summary>
    [Key(10)] public string SignalIdentity { get; init; } = string.Empty;
}

/// <summary>Records one stable, deterministically ordered Regime Discovery reason.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryReason
{
    /// <summary>Gets the stable machine-readable reason code.</summary>
    [Key(0)] public string Code { get; init; } = string.Empty;
    /// <summary>Gets the reason severity.</summary>
    [Key(1)] public RegimeReasonSeverity Severity { get; init; }
    /// <summary>Gets the calculation area that owns the reason.</summary>
    [Key(2)] public RegimeEvidenceArea Area { get; init; }
    /// <summary>Gets the optional observation timeframe.</summary>
    [Key(3)] public TimeFrameType TimeFrame { get; init; }
    /// <summary>Gets the optional stable signal identity used for deterministic ordering.</summary>
    [Key(4)] public string SignalIdentity { get; init; } = string.Empty;
}
