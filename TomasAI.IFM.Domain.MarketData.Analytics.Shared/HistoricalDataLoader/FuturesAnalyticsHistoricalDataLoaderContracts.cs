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
    Trades = 2
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
}

/// <summary>Requests one durable, idempotent futures Analytics history data load.</summary>
[MessagePackObject]
public sealed record LoadFuturesAnalyticsHistoricalDataCommand
    : ICommand<FuturesAnalyticsHistoricalDataLoaderEntityId>
{
    /// <summary>Gets the command actor name.</summary>
    public const string Actor = "FuturesAnalyticsHistoricalDataLoaderCommand";
    /// <summary>Gets the command verb.</summary>
    public const string Verb = "Load";
    /// <summary>Gets the stable command error code.</summary>
    public const int ErrorId = 26020;

    /// <inheritdoc />
    [Key(0)] public Guid CommandId { get; init; }
    /// <inheritdoc />
    [Key(1)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(2)] public bool PostEvents { get; init; } = true;
    /// <inheritdoc />
    [Key(3)] public FuturesAnalyticsHistoricalDataLoaderEntityId EntityId { get; init; }
    /// <inheritdoc />
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [Key(5)] public BoundedContextName RouteTo { get; init; }
    /// <summary>Gets the immutable provider-neutral request parameters.</summary>
    [Key(6)] public FuturesAnalyticsHistoricalDataLoaderParameters Parameters { get; init; } = new();
    /// <inheritdoc />
    [IgnoreMember] public string CommandName => nameof(LoadFuturesAnalyticsHistoricalDataCommand);
    /// <inheritdoc />
    [IgnoreMember] public string StreamId => Subject.StreamId;
    /// <inheritdoc />
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    /// <inheritdoc />
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    /// <inheritdoc />
    [IgnoreMember] public string OriginatedBy => Parameters.RequestedBy;
}

/// <summary>Records that a validated data load request entered durable processing.</summary>
[MessagePackObject]
public sealed record FuturesAnalyticsHistoricalDataLoaderRequestedEvent
    : IEvent<FuturesAnalyticsHistoricalDataLoaderEntityId>
{
    /// <summary>Gets the durable Event actor name.</summary>
    public const string Actor = "FuturesAnalyticsHistoricalDataLoaderEvent";
    /// <summary>Gets the event verb.</summary>
    public const string Verb = "Requested";
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public Guid Id { get; init; }
    /// <inheritdoc />
    [Key(2)] public FuturesAnalyticsHistoricalDataLoaderEntityId EntityId { get; init; }
    /// <inheritdoc />
    [Key(3)] public long EventId { get; init; }
    /// <inheritdoc />
    [Key(4)] public Guid CommandId { get; init; }
    /// <inheritdoc />
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the immutable data load parameters.</summary>
    [Key(8)] public FuturesAnalyticsHistoricalDataLoaderParameters Parameters { get; init; } = new();
    /// <inheritdoc />
    [IgnoreMember] public string UserName => Parameters.RequestedBy;
    /// <inheritdoc />
    [IgnoreMember] public string EventName => nameof(FuturesAnalyticsHistoricalDataLoaderRequestedEvent);
    /// <inheritdoc />
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}

/// <summary>Reports one completed data load without carrying provider records.</summary>
[MessagePackObject]
public sealed record FuturesAnalyticsHistoricalDataLoaderCompletedEvent
    : IEvent<FuturesAnalyticsHistoricalDataLoaderEntityId>
{
    /// <summary>Gets the Event actor name.</summary>
    public const string Actor = FuturesAnalyticsHistoricalDataLoaderRequestedEvent.Actor;
    /// <summary>Gets the event verb.</summary>
    public const string Verb = "Completed";
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public Guid Id { get; init; }
    /// <inheritdoc />
    [Key(2)] public FuturesAnalyticsHistoricalDataLoaderEntityId EntityId { get; init; }
    /// <inheritdoc />
    [Key(3)] public long EventId { get; init; }
    /// <inheritdoc />
    [Key(4)] public Guid CommandId { get; init; }
    /// <inheritdoc />
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the immutable provider manifest identity.</summary>
    [Key(8)] public Guid ManifestId { get; init; }
    /// <summary>Gets the count of valid Daily sessions.</summary>
    [Key(9)] public int ValidSessionCount { get; init; }
    /// <summary>Gets the audited gap count.</summary>
    [Key(10)] public int GapCount { get; init; }
    /// <summary>Gets the audited roll count.</summary>
    [Key(11)] public int RollCount { get; init; }
    /// <summary>Gets the stable request hash.</summary>
    [Key(12)] public string RequestSha256 { get; init; } = string.Empty;
    /// <summary>Gets the UTC completion time.</summary>
    [Key(13)] public DateTime CompletedAtUtc { get; init; }
    /// <inheritdoc />
    [IgnoreMember] public string UserName => string.Empty;
    /// <inheritdoc />
    [IgnoreMember] public string EventName => nameof(FuturesAnalyticsHistoricalDataLoaderCompletedEvent);
    /// <inheritdoc />
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}

/// <summary>Reports one terminal data load failure and its last durable checkpoint.</summary>
[MessagePackObject]
public sealed record FuturesAnalyticsHistoricalDataLoaderFailedEvent
    : IEvent<FuturesAnalyticsHistoricalDataLoaderEntityId>
{
    /// <summary>Gets the Event actor name.</summary>
    public const string Actor = FuturesAnalyticsHistoricalDataLoaderRequestedEvent.Actor;
    /// <summary>Gets the event verb.</summary>
    public const string Verb = "Failed";
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public Guid Id { get; init; }
    /// <inheritdoc />
    [Key(2)] public FuturesAnalyticsHistoricalDataLoaderEntityId EntityId { get; init; }
    /// <inheritdoc />
    [Key(3)] public long EventId { get; init; }
    /// <inheritdoc />
    [Key(4)] public Guid CommandId { get; init; }
    /// <inheritdoc />
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the sanitized terminal failure text.</summary>
    [Key(8)] public string ErrorMessage { get; init; } = string.Empty;
    /// <summary>Gets the last completely persisted batch ordinal.</summary>
    [Key(9)] public int LastCompletedBatchOrdinal { get; init; }
    /// <summary>Gets the last completely persisted record ordinal.</summary>
    [Key(10)] public long LastCompletedRecordOrdinal { get; init; }
    /// <inheritdoc />
    [IgnoreMember] public string UserName => string.Empty;
    /// <inheritdoc />
    [IgnoreMember] public string EventName => nameof(FuturesAnalyticsHistoricalDataLoaderFailedEvent);
    /// <inheritdoc />
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
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

/// <summary>Gets diagnostics for one durable historical data-load attempt.</summary>
[MessagePackObject]
public sealed record GetFuturesAnalyticsHistoricalDataLoaderQuery
    : IQuery<FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel>
{
    /// <summary>Gets the Query actor name.</summary>
    public const string Actor = "FuturesAnalyticsHistoricalDataLoaderQuery";
    /// <summary>Gets the query verb.</summary>
    public const string Verb = "GetDataLoad";
    /// <summary>Gets the stable query error code.</summary>
    public const int ErrorId = 26021;
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public IActorEntityId EntityId { get; init; }
    /// <summary>Gets the attempt identity.</summary>
    [Key(2)] public Guid DataLoadAttemptId { get; init; }
    /// <inheritdoc />
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [IgnoreMember] public string? QueryParams => DataLoadAttemptId.ToString("D");

    /// <summary>Initializes an empty serialization instance.</summary>
    public GetFuturesAnalyticsHistoricalDataLoaderQuery() => EntityId = new FuturesAnalyticsHistoricalDataLoaderEntityId();

    /// <summary>Initializes a query for one attempt.</summary>
    public GetFuturesAnalyticsHistoricalDataLoaderQuery(Guid dataLoadAttemptId)
    {
        DataLoadAttemptId = dataLoadAttemptId;
        EntityId = new FuturesAnalyticsHistoricalDataLoaderEntityId(dataLoadAttemptId);
    }
}
