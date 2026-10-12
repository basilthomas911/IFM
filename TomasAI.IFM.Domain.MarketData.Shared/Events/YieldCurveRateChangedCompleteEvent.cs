using System;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.Events;

/// <summary>
/// Event published when a yield curve rate entry has been changed successfully.
/// Carries metadata from the original event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record YieldCurveRateChangedCompleteEvent : ICompleteEvent<YieldCurveRateEntityId>
{
    [IgnoreMember] public const string Actor = "YieldCurveRateEvent";
    [IgnoreMember] public const string Verb = "ChangedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public YieldCurveRateEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public YieldCurveRateReadModel YieldCurveRate { get; init; }
    [Key(9)] public DateTime UpdatedOn { get; init; }
    [Key(10)] public string UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public YieldCurveRateChangedCompleteEvent() { }

    [SerializationConstructor]
    public YieldCurveRateChangedCompleteEvent(
        ActorSubject subject,
        YieldCurveRateEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        YieldCurveRateReadModel yieldCurveRate,
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
        YieldCurveRate = yieldCurveRate ?? throw new ArgumentNullException(nameof(yieldCurveRate));
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }
}
