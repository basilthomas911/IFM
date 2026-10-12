using TomasAI.IFM.Domain.Trade.Shared;
using System;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder;

namespace TomasAI.IFM.Domain.Trade.Shared.AlgoTrader.Events
{

    /// <summary>
    /// Event published when a trade strategy start operation has been completed successfully.
    /// Carries metadata from the original event.
    /// </summary>
    [MessagePackObject(AllowPrivate = true)]
    public record TradeStrategyStartedCompleteEvent : ICompleteEvent<TradeOrderEntityId>
    {
        [Key(0)] public ActorSubject Subject { get; init; }
        [Key(1)] public TradeOrderEntityId EntityId { get; init; }
        [Key(2)] public Guid Id { get; init; }
        [Key(3)] public long EventId { get; init; }
        [Key(4)] public Guid CommandId { get; init; }
        [Key(5)] public string AggregateId { get; init; }
        [Key(6)] public string EventSource { get; init; }
        [Key(7)] public DateTime ReceivedOn { get; init; }
        [Key(8)] public TradeType TradeType { get; init; }
        [Key(9)] public string? TradeStrategyName { get; init; }

        [IgnoreMember]
        public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";

        [IgnoreMember]
        public string EventName => GetType().Name;

        [IgnoreMember]
        public EventType EventType => EventType.CompletedEvent;

        public TradeStrategyStartedCompleteEvent() { }

        [SerializationConstructor]
        public TradeStrategyStartedCompleteEvent(ActorSubject subject, TradeOrderEntityId entityId, Guid id, long eventId, Guid commandId, string aggregateId, string eventSource, DateTime receivedOn, TradeType tradeType, string? tradeStrategyName)
        {
            Subject = subject;
            EntityId = entityId;
            Id = id;
            EventId = eventId;
            CommandId = commandId;
            AggregateId = aggregateId ?? string.Empty;
            EventSource = eventSource ?? string.Empty;
            ReceivedOn = receivedOn;
            TradeType = tradeType;
            TradeStrategyName = tradeStrategyName;
        }
    }
}
