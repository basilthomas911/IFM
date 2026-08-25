using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;

/// <summary>Reports that the Regime Discovery pipeline accepted its Start command and began processing.</summary>
/// <remarks>
/// The future pipeline Command actor persists this event and projects its ScyllaDB read model before publishing the
/// same logical event realtime to the Workflow Strategy Realtime actor.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public sealed record RegimeDiscoveryPipelineProcessingEvent : IEvent<IntrinsicTimeStrategyWorkflowEntityId>
{
    /// <summary>Future pipeline Realtime actor name.</summary>
    [IgnoreMember] public const string Actor = "RegimeDiscoveryPipelineRealtime";
    /// <summary>Stable pipeline lifecycle verb.</summary>
    [IgnoreMember] public const string Verb = "Processing";
    /// <summary>Stable event error identifier.</summary>
    [IgnoreMember] public const int ErrorId = 24001;

    /// <summary>Gets the persisted pipeline event subject.</summary>
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <summary>Gets the stable logical pipeline event identity.</summary>
    [Key(1)] public Guid Id { get; init; }
    /// <summary>Gets the workflow routing identity.</summary>
    [Key(2)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    /// <summary>Gets the pipeline event-stream sequence identity.</summary>
    [Key(3)] public long EventId { get; init; }
    /// <summary>Gets the Start pipeline command identity.</summary>
    [Key(4)] public Guid CommandId { get; init; }
    /// <summary>Gets the pipeline aggregate identity.</summary>
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    /// <summary>Gets the pipeline Command event source.</summary>
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    /// <summary>Gets the UTC event receipt timestamp.</summary>
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the workflow execution identity.</summary>
    [Key(8)] public StrategyWorkflowId WorkflowId { get; init; }
    /// <summary>Gets the immutable workflow revision supplied to the pipeline.</summary>
    [Key(9)] public long InputWorkflowRevision { get; init; }
    /// <summary>Gets the workflow correlation identity.</summary>
    [Key(10)] public Guid CorrelationId { get; init; }
    /// <summary>Gets the causative pipeline command or processing-event identity.</summary>
    [Key(11)] public Guid CausationId { get; init; }
    /// <summary>Gets the pipeline workflow stage.</summary>
    [Key(12)] public StrategyWorkflowStage PipelineStage { get; init; }    /// <summary>Gets the UTC timestamp at which pipeline processing began.</summary>
    [Key(13)] public DateTime ProcessingAtUtc { get; init; }

    /// <summary>Gets the local pipeline event-source user for diagnostics.</summary>
    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    /// <summary>Gets the concrete pipeline event contract name.</summary>
    [IgnoreMember] public string EventName => nameof(RegimeDiscoveryPipelineProcessingEvent);
    /// <summary>Gets the pipeline event classification.</summary>
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>Initializes an empty pipeline event for serialization.</summary>
    public RegimeDiscoveryPipelineProcessingEvent() { }

    /// <summary>Initializes the complete keyed MessagePack pipeline event.</summary>
    /// <param name="subject">Persisted pipeline event subject.</param>
    /// <param name="id">Stable logical pipeline event identity.</param>
    /// <param name="entityId">Workflow routing identity.</param>
    /// <param name="eventId">Pipeline event-stream sequence identity.</param>
    /// <param name="commandId">Start pipeline command identity.</param>
    /// <param name="aggregateId">Pipeline aggregate identity.</param>
    /// <param name="eventSource">Pipeline Command event source.</param>
    /// <param name="receivedOn">UTC event receipt timestamp.</param>
    /// <param name="workflowId">Workflow execution identity.</param>
    /// <param name="inputWorkflowRevision">Immutable workflow input revision.</param>
    /// <param name="correlationId">Workflow correlation identity.</param>
    /// <param name="causationId">Causative command or processing-event identity.</param>
    /// <param name="pipelineStage">Pipeline workflow stage.</param>
    /// <param name="processingAtUtc">UTC timestamp at which processing began.</param>
    [SerializationConstructor]
    public RegimeDiscoveryPipelineProcessingEvent(
        ActorSubject subject,
        Guid id,
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        StrategyWorkflowId workflowId,
        long inputWorkflowRevision,
        Guid correlationId,
        Guid causationId,
        StrategyWorkflowStage pipelineStage,
        DateTime processingAtUtc)
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
        InputWorkflowRevision = inputWorkflowRevision;
        CorrelationId = correlationId;
        CausationId = causationId;
        PipelineStage = pipelineStage;
        ProcessingAtUtc = processingAtUtc;
    }
}
