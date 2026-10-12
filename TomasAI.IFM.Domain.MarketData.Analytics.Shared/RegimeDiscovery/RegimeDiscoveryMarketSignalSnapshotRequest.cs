using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;

/// <summary>Requests one atomic market-signal snapshot for a workflow horizon.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryMarketSignalSnapshotRequest
{
    /// <summary>Gets the requested provider-neutral market series.</summary>
    [Key(0)] public MarketSeriesIdentity MarketSeriesIdentity { get; init; }
    /// <summary>Gets the single Daily, Weekly, or Monthly target horizon.</summary>
    [Key(1)] public TimeFrameType TargetHorizon { get; init; }
    /// <summary>Gets exact required and optional metrics.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public model contract.")]
    [Key(2)] public RegimeDiscoverySignalRequirement[] Requirements { get; init; } = [];
    /// <summary>Gets the maximum tolerated future clock skew in seconds.</summary>
    [Key(3)] public int FutureClockSkewSeconds { get; init; }
    /// <summary>Gets supported upstream schema versions.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public model contract.")]
    [Key(4)] public ushort[] SupportedSchemaVersions { get; init; } = [];
    /// <summary>Gets approved upstream calculation versions.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public model contract.")]
    [Key(5)] public string[] ApprovedCalculationVersions { get; init; } = [];
    /// <summary>Gets the bounded stable-revision capture attempt count.</summary>
    [Key(6)] public int CaptureAttempts { get; init; } = 3;
}
