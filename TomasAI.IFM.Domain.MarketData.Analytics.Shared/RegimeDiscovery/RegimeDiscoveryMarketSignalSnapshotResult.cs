using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;

/// <summary>Returns either one immutable snapshot or explicit capture issues.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryMarketSignalSnapshotResult
{
    /// <summary>Gets whether every required observation is available.</summary>
    [Key(0)] public bool IsSuccess { get; init; }
    /// <summary>Gets the snapshot when capture succeeded.</summary>
    [Key(1)] public RegimeDiscoveryMarketSignalSnapshot? Snapshot { get; init; }
    /// <summary>Gets unavailable required and optional observations.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public model contract.")]
    [Key(2)] public RegimeDiscoverySignalObservation[] Issues { get; init; } = [];
}
