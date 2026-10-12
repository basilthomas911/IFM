using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>
/// Event published when futures RSI signals have been generated successfully.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesRsiSignalsGeneratedCompleteEvent : ICompleteEvent<FuturesRsiSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesRsiSignalEvent";
    [IgnoreMember] public const string Verb = "SignalsGeneratedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesRsiSignalEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FuturesRsiSignalsId FuturesRsiSignalsId { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public model contract.")]
    [Key(9)] public FuturesRsiSignalReadModel[] FuturesRsiSignals { get; init; }
    [Key(10)] public int PeriodLength { get; init; }
    [Key(11)] public DateTime CreatedOn { get; init; }
    [Key(12)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FuturesRsiSignalsGeneratedCompleteEvent() { }

    [SerializationConstructor]
    public FuturesRsiSignalsGeneratedCompleteEvent(
        ActorSubject subject,
        FuturesRsiSignalEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesRsiSignalsId futuresRsiSignalsId,
        FuturesRsiSignalReadModel[] futuresRsiSignals,
        int periodLength,
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
        FuturesRsiSignalsId = futuresRsiSignalsId;
        FuturesRsiSignals = futuresRsiSignals ?? [];
        PeriodLength = periodLength;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }
}
