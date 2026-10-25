using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

/// <summary>
/// Event published when futures end-of-day data has been inserted successfully.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesEodDataInsertedCompleteEvent : ICompleteEvent<FuturesEodDataId>
{
    [IgnoreMember] public const string Actor = "FuturesEodDataEvent";
    [IgnoreMember] public const string Verb = "InsertedComplete";
    [IgnoreMember] public const int ErrorCode = 5001;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesEodDataId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FuturesEodDataV2ReadModel FuturesEodData { get; init; }
    [Key(9)] public DateTime CreatedOn { get; init; }
    [Key(10)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FuturesEodDataInsertedCompleteEvent() { }

    [SerializationConstructor]
    public FuturesEodDataInsertedCompleteEvent(
        ActorSubject subject,
        FuturesEodDataId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesEodDataV2ReadModel futuresEodData,
        DateTime createdOn,
        string createdBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        FuturesEodData = futuresEodData;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }
}
