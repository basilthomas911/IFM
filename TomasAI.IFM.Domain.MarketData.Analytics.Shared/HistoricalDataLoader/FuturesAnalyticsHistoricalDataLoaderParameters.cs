using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;

/// <summary>Contains the immutable, parameter-only data load input carried by Command and Requested Event messages.</summary>
[MessagePackObject]
public sealed record FuturesAnalyticsHistoricalDataLoaderParameters
{
    /// <summary>Gets the requested market series.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public model contract.")]
    [Key(0)] public FuturesAnalyticsHistorySeriesRequest[] Series { get; init; } = [];
    /// <summary>Gets the first requested trading date.</summary>
    [Key(1)] public DateOnly StartDate { get; init; }
    /// <summary>Gets the final requested trading date.</summary>
    [Key(2)] public DateOnly EndDate { get; init; }
    /// <summary>Gets the requested analytics signal-family names.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public model contract.")]
    [Key(3)] public string[] SignalFamilies { get; init; } = [];
    /// <summary>Gets whether exact trade history is mandatory for VWAP.</summary>
    [Key(4)] public bool ExactVwapRequired { get; init; }
    /// <summary>Gets the maximum approved provider cost in USD.</summary>
    [Key(5)] public decimal MaximumCostUsd { get; init; }
    /// <summary>Gets the maximum approved download size.</summary>
    [Key(6)] public long MaximumBytes { get; init; }
    /// <summary>Gets the normalization implementation version.</summary>
    [Key(7)] public string NormalizationVersion { get; init; } = string.Empty;
    /// <summary>Gets the immutable calculation configuration version.</summary>
    [Key(8)] public string CalculationConfigurationVersion { get; init; } = string.Empty;
    /// <summary>Gets the operator or scheduler identity.</summary>
    [Key(9)] public string RequestedBy { get; init; } = string.Empty;
    /// <summary>Gets whether this is the Development UI automatic coverage request.</summary>
    [Key(10)] public bool AutomaticStartupWarmup { get; init; }
    /// <summary>Gets the active contract that receives the reconciled Analytics result.</summary>
    [Key(11)] public string AnalyticsTargetContractId { get; init; } = string.Empty;
    /// <summary>Gets the API process boot that requested automatic startup warmup.</summary>
    [Key(12)] public Guid ProcessBootId { get; init; }
    /// <summary>Gets the Application startup command that owns this warmup attempt.</summary>
    [Key(13)] public Guid StartupCommandId { get; init; }
}
