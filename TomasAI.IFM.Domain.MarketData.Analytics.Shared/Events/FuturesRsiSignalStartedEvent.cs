using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>
/// Represents a domain event indicating that a futures RSI signal workflow has been started.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesRsiSignalStartedEvent : IEvent<FuturesRsiSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesRsiSignalEvent";
    [IgnoreMember] public const string Verb = "Started";
    [IgnoreMember] public const int ErrorCode = 19003;

    // base metadata (keys 0..7)
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesRsiSignalEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public DateOnly ValueDate { get; init; }
    [Key(9)] public DateTime StartedOn { get; init; }
    [Key(10)] public string StartedBy { get; init; }

    [Key(11)] public int HistoricalSeedCount { get; init; }
    [Key(12)] public string HistoricalSeedReason { get; init; } = string.Empty;
    [Key(13)] public TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels.FuturesRsiSignalReadModel? RestoredSignal { get; init; }
    /// <summary>Gets the accumulator checkpoint restored at this lifecycle snapshot boundary.</summary>
    [Key(14)] public TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels.FuturesRsiAccumulatorCheckpoint? RestoredCheckpoint { get; init; }
    /// <summary>Warm seed values preceding the separately published final seed signal.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public model contract.")]
    [Key(15)] public TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels.FuturesRsiSignalReadModel[] HistoricalWarmSignals { get; init; } = [];
    [Key(16)] public bool ResetForHistoricalSeed { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FuturesRsiSignalStartedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesRsiSignalStartedEvent(
        ActorSubject subject,
        Guid id,
        FuturesRsiSignalEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        DateOnly valueDate,
        DateTime startedOn,
        string startedBy, int historicalSeedCount = 0, string historicalSeedReason = "", TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels.FuturesRsiSignalReadModel? restoredSignal = null, TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels.FuturesRsiAccumulatorCheckpoint? restoredCheckpoint = null, TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels.FuturesRsiSignalReadModel[]? historicalWarmSignals = null, bool resetForHistoricalSeed = false)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        ValueDate = valueDate;
        StartedOn = startedOn;
        StartedBy = startedBy ?? string.Empty;
        HistoricalSeedCount = historicalSeedCount; HistoricalSeedReason = historicalSeedReason ?? string.Empty; RestoredSignal = restoredSignal; RestoredCheckpoint = restoredCheckpoint;
        HistoricalWarmSignals = historicalWarmSignals ?? [];
        ResetForHistoricalSeed = resetForHistoricalSeed;
    }

    /// <summary>
    /// Convert this denormalize event into a completed event which indicates successful handling.
    /// </summary>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FuturesRsiSignalEntityId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesRsiSignalEntityId).FullName}.");

        ICompleteEvent<FuturesRsiSignalEntityId> completed = new FuturesRsiSignalStartedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesRsiSignalStartedCompleteEvent.Actor, FuturesRsiSignalStartedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            StartedOn = this.StartedOn,
            StartedBy = this.StartedBy
        };

        return (ICompleteEvent<TEntityId>)completed;
    }

    /// <summary>
    /// Convert this denormalize event into a failed error event describing the provided exception.
    /// </summary>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (typeof(TEntityId) != typeof(FuturesRsiSignalEntityId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesRsiSignalEntityId).FullName}.");

        IErrorEvent<FuturesRsiSignalEntityId> failed = new FuturesRsiSignalStartedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesRsiSignalStartedFailEvent.Actor, FuturesRsiSignalStartedFailEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            ErrorDate = DateTime.UtcNow,
            EventId = this.EventId,
            CommandId = this.CommandId == Guid.Empty ? Guid.NewGuid() : this.CommandId,
            EventSource = this.EventSource,
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode,
            ErrorData = ex.ToString(),
            ReceivedOn = this.ReceivedOn,
            AggregateId = this.AggregateId
        };

        return (IErrorEvent<TEntityId>)failed;
    }
}
