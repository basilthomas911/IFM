using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Events;

[MessagePackObject(AllowPrivate = true)]
public record OptionTradeDailyProfitTargetUpdatedEvent : IEvent<OptionTradeEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionTradeEvent";
    [IgnoreMember] public const string Verb = "DailyProfitTargetUpdated";
    [IgnoreMember] public const int ErrorCode = 9056;

    // base metadata (keys 0..7)
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public OptionTradeEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public int OrderId { get; init; }
    [Key(9)] public int TradeId { get; init; }
    [Key(10)] public TradeType TradeType { get; init; }
    [Key(11)] public decimal DailyProfitTarget { get; init; }
    [Key(12)] public DateTime UpdatedOn { get; init; }
    [Key(13)] public string UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public OptionTradeDailyProfitTargetUpdatedEvent() { }

    [SerializationConstructor]
    public OptionTradeDailyProfitTargetUpdatedEvent(
        ActorSubject subject,
        Guid id,
        OptionTradeEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        int orderId,
        int tradeId,
        TradeType tradeType,
        decimal dailyProfitTarget,
        DateTime updatedOn,
        string updatedBy)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        DailyProfitTarget = dailyProfitTarget;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }

    public ICompleteEvent ToCompletedEvent()
    {
        return new OptionTradeDailyProfitTargetUpdatedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, OptionTradeDailyProfitTargetUpdatedCompleteEvent.Actor, OptionTradeDailyProfitTargetUpdatedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            OrderId = this.OrderId,
            TradeId = this.TradeId,
            TradeType = this.TradeType,
            DailyProfitTarget = this.DailyProfitTarget,
            UpdatedOn = this.UpdatedOn,
            UpdatedBy = this.UpdatedBy
        };
    }

    public IErrorEvent ToFailedEvent(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        return new OptionTradeDailyProfitTargetUpdatedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, OptionTradeDailyProfitTargetUpdatedFailEvent.Actor, OptionTradeDailyProfitTargetUpdatedFailEvent.Verb, this.Subject.EntityId),
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
    }
}
