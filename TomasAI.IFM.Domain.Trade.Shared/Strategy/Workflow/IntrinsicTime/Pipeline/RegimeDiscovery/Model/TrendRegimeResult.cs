using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Contains the deterministic Trend specialist result.</summary>
[MessagePackObject]
public sealed record TrendRegimeResult
{
    /// <summary>Gets whether all required Trend evidence was complete.</summary>
    [Key(0)] public bool IsComplete { get; init; }
    /// <summary>Gets the calculated trend direction.</summary>
    [Key(1)] public RegimeDirection Direction { get; init; }
    /// <summary>Gets the calculated trend strength.</summary>
    [Key(2)] public TrendRegimeStrength Strength { get; init; }
    /// <summary>Gets the calculated trend lifecycle phase.</summary>
    [Key(3)] public TrendRegimePhase Phase { get; init; }
    /// <summary>Gets the signed normalized trend score.</summary>
    [Key(4)] public decimal Score { get; init; }
    /// <summary>Gets the normalized trend confidence.</summary>
    [Key(5)] public decimal Confidence { get; init; }
    /// <summary>Gets the trend confidence band.</summary>
    [Key(6)] public RegimeConfidenceBand ConfidenceBand { get; init; }
    /// <summary>Gets normalized cross-timeframe agreement.</summary>
    [Key(7)] public decimal TimeFrameAgreement { get; init; }
    /// <summary>Gets the normalized Trend evidence in deterministic order.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(8)] public RegimeDiscoveryEvidence[] Evidence { get; init; } = [];
    /// <summary>Gets stable Trend reason codes in deterministic order.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(9)] public RegimeDiscoveryReason[] Reasons { get; init; } = [];
}
