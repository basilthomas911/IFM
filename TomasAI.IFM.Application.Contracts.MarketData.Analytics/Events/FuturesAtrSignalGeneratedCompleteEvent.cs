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
public record FuturesAtrSignalGeneratedCompleteEvent : ICompleteEvent<FuturesAtrSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesAtrSignalEvent";
    [IgnoreMember] public const string Verb = "SignalGeneratedComplete";
    [IgnoreMember] public const int ErrorCode = 19004;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesAtrSignalEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FuturesAtrSignalReadModel FuturesAtrSignal { get; init; }
    [Key(9)] public FuturesAtrSignalSourceType AtrSignalSource { get; init; }
    [Key(10)] public DateTime CreatedOn { get; init; }
    [Key(11)] public string CreatedBy { get; init; }

    /// <summary>Gets the complete bounded Wilder calculation checkpoint.</summary>
    [Key(12)] public FuturesAtrAccumulatorCheckpoint? FuturesAtrCheckpoint { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FuturesAtrSignalGeneratedCompleteEvent() { }

    [SerializationConstructor]
    public FuturesAtrSignalGeneratedCompleteEvent(
        ActorSubject subject,
        FuturesAtrSignalEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesAtrSignalReadModel futuresAtrSignal,
        FuturesAtrSignalSourceType atrSignalSource,
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
        AtrSignalSource = atrSignalSource;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
        FuturesAtrCheckpoint = futuresAtrCheckpoint;
    }
}
