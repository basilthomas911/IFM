using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

/// <summary>The durable start snapshot for one strategy-position exit decision.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record ExitPositionWorkflowStartedEvent : IEvent<ExitPositionWorkflowId>
{

    /// <summary>Creates an empty event for serialization and existing callers.</summary>
    public ExitPositionWorkflowStartedEvent() { }

    /// <summary>Rehydrates every published event field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="id">The Id field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="eventId">The EventId field.</param>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="aggregateId">The AggregateId field.</param>
    /// <param name="eventSource">The EventSource field.</param>
    /// <param name="receivedOn">The ReceivedOn field.</param>
    /// <param name="exitPlan">The ExitPlan field.</param>
    /// <param name="strategyKind">The StrategyKind field.</param>
    /// <param name="sourcePlanEventId">The SourcePlanEventId field.</param>
    [SerializationConstructor]
    public ExitPositionWorkflowStartedEvent(ActorSubject subject, Guid id, ExitPositionWorkflowId entityId, long eventId, Guid commandId, string aggregateId, string eventSource, DateTime receivedOn, StrategyTradePlanSnapshot exitPlan, TradeStrategyKind strategyKind, Guid sourcePlanEventId)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        ExitPlan = exitPlan;
        StrategyKind = strategyKind;
        SourcePlanEventId = sourcePlanEventId;
    }
    public const string Verb = "ExitPositionWorkflowStarted";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public ExitPositionWorkflowId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public StrategyTradePlanSnapshot ExitPlan { get; init; } = new();
    [Key(9)] public TradeStrategyKind StrategyKind { get; init; }
    [Key(10)] public Guid SourcePlanEventId { get; init; }
    [IgnoreMember] public string UserName => "TradePlan";
    [IgnoreMember] public string EventName => nameof(ExitPositionWorkflowStartedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
