using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.Events;

[MessagePackObject(AllowPrivate = true)]
public record TradePositionUpdatedCompleteEvent : ICompleteEvent<OptionTradeEntityId>
{
    [IgnoreMember] public const string Actor = "TradePositionEvent";
    [IgnoreMember] public const string Verb = "UpdatedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public OptionTradeEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public TradePositionChangeSourceType TradePositionChangeSource { get; init; }
    [Key(9)] public string OptionLegId { get; init; }
    [Key(10)] public TradePositionReadModel? PutTradePosition { get; init; }
    [Key(11)] public TradePositionReadModel? CallTradePosition { get; init; }
    [Key(12)] public DateTime UpdatedOn { get; init; }
    [Key(13)] public string UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public TradePositionUpdatedCompleteEvent() { }

    [SerializationConstructor]
    public TradePositionUpdatedCompleteEvent(
        ActorSubject subject,
        OptionTradeEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        TradePositionChangeSourceType tradePositionChangeSource,
        string optionLegId,
        TradePositionReadModel? putTradePosition,
        TradePositionReadModel? callTradePosition,
        DateTime updatedOn,
        string updatedBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        TradePositionChangeSource = tradePositionChangeSource;
        OptionLegId = optionLegId ?? string.Empty;
        PutTradePosition = putTradePosition;
        CallTradePosition = callTradePosition;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }
}
