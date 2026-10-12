using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Contains the deterministic Market Structure specialist result.</summary>
[MessagePackObject]
public sealed record MarketStructureRegimeResult
{
    /// <summary>Gets whether all required Market Structure evidence was complete.</summary>
    [Key(0)] public bool IsComplete { get; init; }
    /// <summary>Gets the structure classification.</summary>
    [Key(1)] public MarketStructureClassification Classification { get; init; }
    /// <summary>Gets the optional directional structure bias.</summary>
    [Key(2)] public RegimeDirection Direction { get; init; }
    /// <summary>Gets the breakout state.</summary>
    [Key(3)] public MarketBreakoutState Breakout { get; init; }
    /// <summary>Gets the signed normalized structure score.</summary>
    [Key(4)] public decimal Score { get; init; }
    /// <summary>Gets the normalized Market Structure confidence.</summary>
    [Key(5)] public decimal Confidence { get; init; }
    /// <summary>Gets the Market Structure confidence band.</summary>
    [Key(6)] public RegimeConfidenceBand ConfidenceBand { get; init; }
    /// <summary>Gets normalized Market Structure evidence in deterministic order.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(7)] public RegimeDiscoveryEvidence[] Evidence { get; init; } = [];
    /// <summary>Gets stable Market Structure reasons in deterministic order.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(8)] public RegimeDiscoveryReason[] Reasons { get; init; } = [];
}
