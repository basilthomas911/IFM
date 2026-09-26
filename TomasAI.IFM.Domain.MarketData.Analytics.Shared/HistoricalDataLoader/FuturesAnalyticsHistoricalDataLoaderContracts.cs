using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;

/// <summary>Identifies one durable futures Analytics historical data-load attempt.</summary>
[MessagePackObject]
public readonly record struct FuturesAnalyticsHistoricalDataLoaderEntityId(
    [property: Key(0)] Guid Value) : IActorEntityId
{
    /// <summary>Formats the attempt identity for actor routing.</summary>
    public string Format() => Value.ToString("N");
}

/// <summary>Identifies the provider-neutral historical record shape requested for a series.</summary>
public enum FuturesAnalyticsHistoricalSchema : byte
{
    /// <summary>One-minute OHLCV observations.</summary>
    OhlcvOneMinute = 1,
    /// <summary>Exact normalized trades.</summary>
    Trades = 2,
    /// <summary>Daily OHLCV observations.</summary>
    OhlcvDaily = 3
}

/// <summary>Defines one provider-neutral series in a data load request.</summary>
[MessagePackObject]
public sealed record FuturesAnalyticsHistorySeriesRequest
{
    /// <summary>Gets the continuation-series or exact-contract identity.</summary>
    [Key(0)] public MarketSeriesIdentity MarketSeriesIdentity { get; init; }
    /// <summary>Gets an optional explicit provider contract/symbol.</summary>
    [Key(1)] public string ContractId { get; init; } = string.Empty;
    /// <summary>Gets the requested historical record shape.</summary>
    [Key(2)] public FuturesAnalyticsHistoricalSchema Schema { get; init; }
    /// <summary>Gets whether exact trades are mandatory for this series.</summary>
    [Key(3)] public bool ExactTradesRequired { get; init; }
}

/// <summary>Contains the immutable, parameter-only data load input carried by Command and Requested Event messages.</summary>
[MessagePackObject]
public sealed record FuturesAnalyticsHistoricalDataLoaderParameters
{
    /// <summary>Gets the requested market series.</summary>
    [Key(0)] public FuturesAnalyticsHistorySeriesRequest[] Series { get; init; } = [];
    /// <summary>Gets the first requested trading date.</summary>
    [Key(1)] public DateOnly StartDate { get; init; }
    /// <summary>Gets the final requested trading date.</summary>
    [Key(2)] public DateOnly EndDate { get; init; }
    /// <summary>Gets the requested analytics signal-family names.</summary>
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

/// <summary>Provides provider-neutral data load diagnostics to Query clients.</summary>
[MessagePackObject]
public sealed record FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel
{
    /// <summary>Gets the data load attempt identity.</summary>
    [Key(0)] public Guid DataLoadAttemptId { get; init; }
    /// <summary>Gets the stable request hash.</summary>
    [Key(1)] public string RequestSha256 { get; init; } = string.Empty;
    /// <summary>Gets the provider-neutral lifecycle status.</summary>
    [Key(2)] public string Status { get; init; } = string.Empty;
    /// <summary>Gets the manifest identity after completion.</summary>
    [Key(3)] public Guid? ManifestId { get; init; }
    /// <summary>Gets the last completed batch ordinal.</summary>
    [Key(4)] public int LastCompletedBatchOrdinal { get; init; }
    /// <summary>Gets the last completed record ordinal.</summary>
    [Key(5)] public long LastCompletedRecordOrdinal { get; init; }
    /// <summary>Gets the number of audited valid sessions.</summary>
    [Key(6)] public int ValidSessionCount { get; init; }
    /// <summary>Gets the number of audited gaps.</summary>
    [Key(7)] public int GapCount { get; init; }
    /// <summary>Gets the number of audited rolls.</summary>
    [Key(8)] public int RollCount { get; init; }
    /// <summary>Gets the sanitized terminal failure text.</summary>
    [Key(9)] public string ErrorMessage { get; init; } = string.Empty;
    /// <summary>Gets the last durable update time.</summary>
    [Key(10)] public DateTimeOffset UpdatedAtUtc { get; init; }
}
