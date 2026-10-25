using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

/// <summary>
/// Event published when a futures closing price has been inserted successfully.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesClosingPriceInsertedCompleteEvent : ICompleteEvent<FuturesDataId>
{
    [IgnoreMember] public const string Actor = "FuturesClosingPriceEvent";
    [IgnoreMember] public const string Verb = "InsertedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesDataId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FuturesDataId FuturesClosingPriceId { get; init; }
    [Key(9)] public decimal ClosingPrice { get; init; }
    [Key(10)] public DateTime CreatedOn { get; init; }
    [Key(11)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FuturesClosingPriceInsertedCompleteEvent() { }

    [SerializationConstructor]
    public FuturesClosingPriceInsertedCompleteEvent(
        ActorSubject subject,
        FuturesDataId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesDataId futuresClosingPriceId,
        decimal closingPrice,
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
        FuturesClosingPriceId = futuresClosingPriceId;
        ClosingPrice = closingPrice;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }
}
