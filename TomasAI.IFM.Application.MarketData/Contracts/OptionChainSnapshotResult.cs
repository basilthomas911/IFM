using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>One immutable prepared version and provenance; reason codes describe unavailable outcomes.</summary>
public sealed record OptionChainSnapshotResult(OptionChainSnapshotOutcome Outcome, string ReasonCode,
    MarketCompositionSnapshot? Snapshot = null, long ChainVersion = 0)
{
    /// <summary>Whether the returned snapshot is eligible for composition at the instant of reading.</summary>
    public bool IsReady => Outcome == OptionChainSnapshotOutcome.Ready;
}
