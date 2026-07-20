using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.Trade.Events
{

    [MessagePackObject(AllowPrivate = true)]
    public record OptionTradeLegDataUpdatedEvent : IEvent
    {
        [IgnoreMember] public const string Actor = "OptionTradeEvent";
        [IgnoreMember] public const string Verb = "LegDataUpdated";
        [IgnoreMember] public const int ErrorCode = 0;
        [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

        [Key(0)] public ActorSubject Subject { get; init; }
        [Key(1)] public Guid Id { get; init; }
        [Key(2)] public string EntityId { get; init; }
        [Key(3)] public long EventId { get; init; }
        [Key(4)] public Guid CommandId { get; init; }
        [Key(5)] public string AggregateId { get; init; }
        [Key(6)] public string EventSource { get; init; }
        [Key(7)] public DateTime ReceivedOn { get; init; }

        // payload (keys 8..)
        [Key(8)] public int OrderId { get; init; }
        [Key(9)] public TradePositionEntityId Key { get; init; }
        [Key(10)] public OptionTradeLegDataReadModel OptionLegData { get; init; }
        [Key(11)] public decimal AssetPrice { get; init; }
        [Key(12)] public DateTime CreatedOn { get; init; }
        [Key(13)] public string CreatedBy { get; init; }
        [Key(14)] public DateTime UpdatedOn { get; init; }
        [Key(15)] public string UpdatedBy { get; init; }

        [IgnoreMember] public string UserName => CachedUserName;
        [IgnoreMember] public string EventName => nameof(OptionTradeLegDataUpdatedEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;

        public OptionTradeLegDataUpdatedEvent() { }

        [SerializationConstructor]
        public OptionTradeLegDataUpdatedEvent(
            ActorSubject subject,
            Guid id,
            string entityId,
            long eventId,
            Guid commandId,
            string aggregateId,
            string eventSource,
            DateTime receivedOn,
            int orderId,
            TradePositionEntityId key,
            OptionTradeLegDataReadModel optionLegData,
            decimal assetPrice,
            DateTime createdOn,
            string createdBy,
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
            Key = key;
            OptionLegData = optionLegData;
            AssetPrice = assetPrice;
            CreatedOn = createdOn;
            CreatedBy = createdBy ?? string.Empty;
            UpdatedOn = updatedOn;
            UpdatedBy = updatedBy ?? string.Empty;
        }
    }

}
