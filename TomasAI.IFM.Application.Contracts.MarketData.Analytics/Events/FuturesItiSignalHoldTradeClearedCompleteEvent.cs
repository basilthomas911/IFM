using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

[MessagePackObject(AllowPrivate = true)]
public record FuturesItiSignalHoldTradeClearedCompleteEvent : ICompleteEvent<FuturesItiSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesItiSignalEvent";
    [IgnoreMember] public const string Verb = "HoldTradeClearedComplete";
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesItiSignalEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public DateTime CreatedOn { get; init; }
    [Key(9)] public string CreatedBy { get; init; }
    [Key(10)] public FuturesItiSignalV2ReadModel? FuturesItiSignal { get; init; }

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(FuturesItiSignalHoldTradeClearedCompleteEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FuturesItiSignalHoldTradeClearedCompleteEvent() { }

    [SerializationConstructor]
    public FuturesItiSignalHoldTradeClearedCompleteEvent(
        ActorSubject subject,
        FuturesItiSignalEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        DateTime createdOn,
        string createdBy,
        FuturesItiSignalV2ReadModel? futuresItiSignal)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
        FuturesItiSignal = futuresItiSignal;
    }
}
