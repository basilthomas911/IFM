using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>
/// Event published when a futures ATR signal has been generated successfully.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesAtrDailySignalGeneratedCompleteEvent : ICompleteEvent<FuturesAtrDailySignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesAtrSignalEvent";
    [IgnoreMember] public const string Verb = "DailySignalGeneratedComplete";
    [IgnoreMember] public const int ErrorCode = 19004;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesAtrDailySignalEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FuturesAtrSignalReadModel FuturesAtrSignal { get; init; }
    [Key(9)] public DateTime CreatedOn { get; init; }
    [Key(10)] public string CreatedBy { get; init; }

    /// <summary>Gets the complete bounded Wilder calculation checkpoint.</summary>
    [Key(11)] public FuturesAtrAccumulatorCheckpoint? FuturesAtrCheckpoint { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FuturesAtrDailySignalGeneratedCompleteEvent() { }

    [SerializationConstructor]
    public FuturesAtrDailySignalGeneratedCompleteEvent(
        ActorSubject subject,
        FuturesAtrDailySignalEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesAtrSignalReadModel futuresAtrSignal,
        DateTime createdOn,
        string createdBy,
        FuturesAtrAccumulatorCheckpoint? futuresAtrCheckpoint)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        FuturesAtrSignal = futuresAtrSignal;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
        FuturesAtrCheckpoint = futuresAtrCheckpoint;
    }
}
