using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.TradeAlgorithm.Events
{

    [MessagePackObject(AllowPrivate = true)]
    public record LongIronCondorAlgorithmExecutedEvent : IEvent
    {
        [IgnoreMember] public const string Actor = "LongIronCondorEvent";
        [IgnoreMember] public const string Verb = "AlgorithmExecuted";
        [IgnoreMember] public const int ErrorCode = 7038;
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
        [Key(8)] public TradeAlgorithmId TradeAlgorithmId { get; init; }
        [Key(9)] public TradePlanReadModel TradePlan { get; init; }

        [IgnoreMember] public string UserName => CachedUserName;
        [IgnoreMember] public string EventName => nameof(LongIronCondorAlgorithmExecutedEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;

        public LongIronCondorAlgorithmExecutedEvent() { }

        [SerializationConstructor]
        public LongIronCondorAlgorithmExecutedEvent(
            ActorSubject subject,
            Guid id,
            string entityId,
            long eventId,
            Guid commandId,
            string aggregateId,
            string eventSource,
            DateTime receivedOn,
            TradeAlgorithmId tradeAlgorithmId,
            TradePlanReadModel tradePlan)
        {
            Subject = subject;
            Id = id;
            EntityId = entityId;
            EventId = eventId;
            CommandId = commandId;
            AggregateId = aggregateId ?? string.Empty;
            EventSource = eventSource ?? string.Empty;
            ReceivedOn = receivedOn;
            TradeAlgorithmId = tradeAlgorithmId;
            TradePlan = tradePlan;
        }

        public ICompleteEvent ToCompletedEvent() => new LongIronCondorAlgorithmExecutedCompleteEvent
        {
            CommandId = this.CommandId,
            TradeAlgorithmId = this.TradeAlgorithmId,
            TradePlan = this.TradePlan
        };
        public IErrorEvent ToFailedEvent(Exception ex) => new LongIronCondorAlgorithmExecutedFailEvent
        {
            CommandId = this.CommandId,
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode
        };
    }


    public record LongIronCondorAlgorithmExecutedCompleteEvent : CompleteEvent
    {
        public TradeAlgorithmId TradeAlgorithmId { get; init; }
        public TradePlanReadModel TradePlan { get; init; }
    }

    public record LongIronCondorAlgorithmExecutedFailEvent : ErrorEvent
    {
    }


}
