using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

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
