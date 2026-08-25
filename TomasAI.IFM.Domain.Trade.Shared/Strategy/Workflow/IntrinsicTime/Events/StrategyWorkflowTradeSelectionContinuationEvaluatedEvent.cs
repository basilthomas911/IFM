using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;

/// <summary>Records the workflow continuation decision after the Trade Selection result.</summary>
/// <remarks>This event is persisted in the workflow Command event log and is not routed to a durable Event actor.</remarks>
[MessagePackObject(AllowPrivate = true)]
public sealed record StrategyWorkflowTradeSelectionContinuationEvaluatedEvent : IEvent<IntrinsicTimeStrategyWorkflowEntityId>
{
    /// <summary>Logical workflow event source name; no Event actor is registered for it.</summary>
    [IgnoreMember] public const string Actor = "IntrinsicTimeStrategyWorkflow";
    /// <summary>Stable event verb.</summary>
    [IgnoreMember] public const string Verb = "TradeSelectionContinuationEvaluated";
    /// <summary>Stable event error identifier.</summary>
    [IgnoreMember] public const int ErrorId = 22015;

    [IgnoreMember]
    string[] _reasonCodes = [];

    /// <summary>Gets the persisted event subject.</summary>
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <summary>Gets the event instance identity.</summary>
    [Key(1)] public Guid Id { get; init; }
    /// <summary>Gets the workflow routing entity identity.</summary>
    [Key(2)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    /// <summary>Gets the event-stream sequence identity.</summary>
    [Key(3)] public long EventId { get; init; }
    /// <summary>Gets the source command identity.</summary>
    [Key(4)] public Guid CommandId { get; init; }
    /// <summary>Gets the workflow aggregate identity.</summary>
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    /// <summary>Gets the workflow Command event source.</summary>
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    /// <summary>Gets the UTC event receipt timestamp.</summary>
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the workflow execution identity.</summary>
    [Key(8)] public StrategyWorkflowId WorkflowId { get; init; }
    /// <summary>Gets the resulting workflow revision.</summary>
    [Key(9)] public long WorkflowRevision { get; init; }
    /// <summary>Gets the workflow correlation identity.</summary>
    [Key(10)] public Guid CorrelationId { get; init; }
    /// <summary>Gets the causative result identity.</summary>
    [Key(11)] public Guid CausationId { get; init; }
    /// <summary>Gets the evaluated workflow stage.</summary>
    [Key(12)] public StrategyWorkflowStage Stage { get; init; }
    /// <summary>Gets the continuation decision.</summary>
    [Key(13)] public StrategyWorkflowContinuationDecision Decision { get; init; }
    /// <summary>Gets the stable continuation rule-set identity.</summary>
    [Key(14)] public string RuleSetId { get; init; } = string.Empty;
    /// <summary>Gets the continuation rule-set version.</summary>
    [Key(15)] public int RuleSetVersion { get; init; }
    /// <summary>Gets a defensive copy of the continuation reason codes.</summary>
    [Key(16)]
    public string[] ReasonCodes
    {
        get => [.. _reasonCodes];
        init => _reasonCodes = value is null ? [] : [.. value];
    }
    /// <summary>Gets the UTC evaluation timestamp.</summary>
    [Key(17)] public DateTime EvaluatedAtUtc { get; init; }

    /// <summary>Gets the local event-source user for diagnostics.</summary>
    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    /// <summary>Gets the concrete event contract name.</summary>
    [IgnoreMember] public string EventName => nameof(StrategyWorkflowTradeSelectionContinuationEvaluatedEvent);
    /// <summary>Gets the domain-event classification.</summary>
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>Initializes an empty event for serialization.</summary>
    public StrategyWorkflowTradeSelectionContinuationEvaluatedEvent() { }

    /// <summary>Initializes the complete keyed MessagePack event contract.</summary>
    /// <param name="subject">Persisted event subject.</param>
    /// <param name="id">Event instance identity.</param>
    /// <param name="entityId">Workflow routing identity.</param>
    /// <param name="eventId">Event-stream sequence identity.</param>
    /// <param name="commandId">Source command identity.</param>
    /// <param name="aggregateId">Workflow aggregate identity.</param>
    /// <param name="eventSource">Command event source.</param>
    /// <param name="receivedOn">UTC event receipt timestamp.</param>
    /// <param name="workflowId">Workflow execution identity.</param>
    /// <param name="workflowRevision">Resulting workflow revision.</param>
    /// <param name="correlationId">Workflow correlation identity.</param>
    /// <param name="causationId">Causative result identity.</param>
    /// <param name="stage">Evaluated workflow stage.</param>
    /// <param name="decision">Continuation decision.</param>
    /// <param name="ruleSetId">Stable continuation rule-set identity.</param>
    /// <param name="ruleSetVersion">Continuation rule-set version.</param>
    /// <param name="reasonCodes">Continuation reason codes.</param>
    /// <param name="evaluatedAtUtc">UTC evaluation timestamp.</param>
    [SerializationConstructor]
    public StrategyWorkflowTradeSelectionContinuationEvaluatedEvent(
        ActorSubject subject,
        Guid id,
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        StrategyWorkflowId workflowId,
        long workflowRevision,
        Guid correlationId,
        Guid causationId,
        StrategyWorkflowStage stage,
        StrategyWorkflowContinuationDecision decision,
        string ruleSetId,
        int ruleSetVersion,
        string[] reasonCodes,
        DateTime evaluatedAtUtc)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        WorkflowId = workflowId;
        WorkflowRevision = workflowRevision;
        CorrelationId = correlationId;
        CausationId = causationId;
        Stage = stage;
        Decision = decision;
        RuleSetId = ruleSetId ?? string.Empty;
        RuleSetVersion = ruleSetVersion;
        ReasonCodes = reasonCodes;
        EvaluatedAtUtc = evaluatedAtUtc;
    }
}
