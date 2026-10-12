using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.Events;

[MessagePackObject(AllowPrivate = true)]
public record OptionTradeToCloseEvent : IEvent<OptionTradeEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionTradeEvent";
    [IgnoreMember] public const string Verb = "ToClose";
    [IgnoreMember] public const int ErrorCode = 9061;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public OptionTradeEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public TradeOrderReadModel TradeOrder { get; init; }
    [Key(9)] public OptionTradeReadModel OptionTrade { get; init; }
    [Key(10)] public DateTime ClosedOn { get; init; }
    [Key(11)] public string ClosedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public OptionTradeToCloseEvent() { }

    [SerializationConstructor]
    public OptionTradeToCloseEvent(
        ActorSubject subject,
        Guid id,
        OptionTradeEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        TradeOrderReadModel tradeOrder,
        OptionTradeReadModel optionTrade,
        DateTime closedOn,
        string closedBy)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        TradeOrder = tradeOrder;
        OptionTrade = optionTrade;
        ClosedOn = closedOn;
        ClosedBy = closedBy ?? string.Empty;
    }

    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(OptionTradeEntityId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(OptionTradeEntityId).FullName}.");

        ICompleteEvent<OptionTradeEntityId> completed = new OptionTradeToCloseCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, OptionTradeToCloseCompleteEvent.Actor, OptionTradeToCloseCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            TradeOrder = this.TradeOrder,
            OptionTrade = this.OptionTrade,
            ClosedOn = this.ClosedOn,
            ClosedBy = this.ClosedBy
        };

        return (ICompleteEvent<TEntityId>)completed;
    }

    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (typeof(TEntityId) != typeof(OptionTradeEntityId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(OptionTradeEntityId).FullName}.");

        IErrorEvent<OptionTradeEntityId> failed = new OptionTradeToCloseFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, OptionTradeToCloseFailEvent.Actor, OptionTradeToCloseFailEvent.Verb, this.Subject.EntityId),
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
