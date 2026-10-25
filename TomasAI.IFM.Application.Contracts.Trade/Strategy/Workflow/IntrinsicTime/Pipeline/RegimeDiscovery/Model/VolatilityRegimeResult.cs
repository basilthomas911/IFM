using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Contains the deterministic Volatility specialist result.</summary>
[MessagePackObject]
public sealed record VolatilityRegimeResult
{
    /// <summary>Gets whether all required Volatility evidence was complete.</summary>
    [Key(0)] public bool IsComplete { get; init; }
    /// <summary>Gets the composite volatility level.</summary>
    [Key(1)] public VolatilityRegimeLevel Level { get; init; }
    /// <summary>Gets the volatility expansion or contraction state.</summary>
    [Key(2)] public VolatilityRegimeChange Change { get; init; }
    /// <summary>Gets the VX term-structure classification.</summary>
    [Key(3)] public VxTermStructureRegime TermStructure { get; init; }
    /// <summary>Gets the normalized unsigned volatility score.</summary>
    [Key(4)] public decimal Score { get; init; }
    /// <summary>Gets the normalized Volatility confidence.</summary>
    [Key(5)] public decimal Confidence { get; init; }
    /// <summary>Gets the Volatility confidence band.</summary>
    [Key(6)] public RegimeConfidenceBand ConfidenceBand { get; init; }
    /// <summary>Gets whether Volatility recommends no new trade.</summary>
    [Key(7)] public bool NoNewTrade { get; init; }
    /// <summary>Gets normalized Volatility evidence in deterministic order.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(8)] public RegimeDiscoveryEvidence[] Evidence { get; init; } = [];
    /// <summary>Gets stable Volatility reasons in deterministic order.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(9)] public RegimeDiscoveryReason[] Reasons { get; init; } = [];
}
