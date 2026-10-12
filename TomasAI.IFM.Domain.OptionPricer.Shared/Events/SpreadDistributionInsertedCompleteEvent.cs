using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;

namespace TomasAI.IFM.Domain.OptionPricer.Shared.Events;

/// <summary>
/// Event published when a spread distribution has been inserted successfully.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record SpreadDistributionInsertedCompleteEvent : ICompleteEvent<SpreadDistributionEntityId>
{
    [IgnoreMember] public const string Actor = "SpreadDistributionEvent";
    [IgnoreMember] public const string Verb = "InsertedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public SpreadDistributionEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public SpreadDistributionReadModel PutSpreadDistribution { get; init; }
    [Key(9)] public SpreadDistributionReadModel CallSpreadDistribution { get; init; }
    [Key(10)] public DateTime CreatedOn { get; init; }
    [Key(11)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public SpreadDistributionInsertedCompleteEvent() { }

    [SerializationConstructor]
    public SpreadDistributionInsertedCompleteEvent(
        ActorSubject subject,
        SpreadDistributionEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        SpreadDistributionReadModel putSpreadDistribution,
        SpreadDistributionReadModel callSpreadDistribution,
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
        PutSpreadDistribution = putSpreadDistribution;
        CallSpreadDistribution = callSpreadDistribution;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }
}
