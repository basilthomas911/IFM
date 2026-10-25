using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

/// <summary>Configures snapshot compatibility and capture quality.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryDataQualityConfiguration
{
    /// <summary>Gets supported upstream signal schema versions.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(0)] public ushort[] SupportedSignalSchemaVersions { get; init; } = [1];
    /// <summary>Gets approved upstream calculation versions.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(1)] public string[] ApprovedCalculationVersions { get; init; } = ["1"];
    /// <summary>Gets the bounded number of revision-stable snapshot capture attempts.</summary>
    [Key(2)] public int SnapshotCaptureAttempts { get; init; } = 3;
}
