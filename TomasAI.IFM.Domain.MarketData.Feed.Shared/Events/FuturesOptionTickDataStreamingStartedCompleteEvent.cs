using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

/// <summary>
/// Event published when futures option tick data streaming has been started successfully.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionTickDataStreamingStartedCompleteEvent : ICompleteEvent<FuturesOptionTickEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionTickDataEvent";
    [IgnoreMember] public const string Verb = "StreamingStartedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesOptionTickEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FuturesOptionContractReadModel Contract { get; init; }
    [Key(9)] public FuturesContractV3ReadModel BaseContract { get; init; }
    [Key(10)] public DateOnly ValueDate { get; init; }
    [Key(11)] public DateOnly MaturityDate { get; init; }
    [Key(12)] public double RiskFreeRate { get; init; }
    [Key(13)] public DateTime StartedOn { get; init; }
    [Key(14)] public string StartedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FuturesOptionTickDataStreamingStartedCompleteEvent() { }

    [SerializationConstructor]
    public FuturesOptionTickDataStreamingStartedCompleteEvent(
        ActorSubject subject,
        FuturesOptionTickEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesOptionContractReadModel contract,
        FuturesContractV3ReadModel baseContract,
        DateOnly valueDate,
        DateOnly maturityDate,
        double riskFreeRate,
        DateTime startedOn,
        string startedBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        Contract = contract;
        BaseContract = baseContract;
        ValueDate = valueDate;
        MaturityDate = maturityDate;
        RiskFreeRate = riskFreeRate;
        StartedOn = startedOn;
        StartedBy = startedBy ?? string.Empty;
    }
}
