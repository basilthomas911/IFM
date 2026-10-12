using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>
/// Event published when a futures MACD signal has been generated successfully.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesMacdSignalGeneratedCompleteEvent : ICompleteEvent<FuturesMacdSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesMacdSignalEvent";
    [IgnoreMember] public const string Verb = "SignalGeneratedComplete";
    [IgnoreMember] public const int ErrorCode = 19003;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesMacdSignalEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FuturesMacdSignalReadModel FuturesMacdSignal { get; init; }
    [Key(9)] public DateTime CreatedOn { get; init; }
    [Key(10)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FuturesMacdSignalGeneratedCompleteEvent() { }

    [SerializationConstructor]
    public FuturesMacdSignalGeneratedCompleteEvent(
        ActorSubject subject,
        FuturesMacdSignalEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesMacdSignalReadModel futuresMacdSignal,
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
        FuturesMacdSignal = futuresMacdSignal;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }
}
