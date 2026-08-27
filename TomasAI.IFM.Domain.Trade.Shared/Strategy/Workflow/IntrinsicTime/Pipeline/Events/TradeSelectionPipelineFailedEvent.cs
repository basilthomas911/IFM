using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;

/// <summary>Reports terminal failure of the Trade Selection pipeline calculation.</summary>
/// <remarks>
/// The future pipeline Command actor persists this standard failure event and projects its ScyllaDB read model before
/// publishing the same logical event realtime to the Workflow Strategy Realtime actor.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public sealed record TradeSelectionPipelineFailedEvent : IErrorEvent<IntrinsicTimeStrategyWorkflowEntityId>
{
    /// <summary>Future pipeline Realtime actor name.</summary>
    [IgnoreMember] public const string Actor = "TradeSelectionPipelineRealtime";
    /// <summary>Stable pipeline failure verb.</summary>
    [IgnoreMember] public const string Verb = "TradeSelectionPipelineFailed";
    /// <summary>Stable event error identifier.</summary>
    [IgnoreMember] public const int ErrorId = 24009;

    /// <summary>Gets the persisted pipeline event subject.</summary>
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <summary>Gets the workflow routing identity.</summary>
    [Key(1)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    /// <summary>Gets the stable logical pipeline failure identity.</summary>
    [Key(2)] public Guid Id { get; init; }
    /// <summary>Gets the UTC failure timestamp.</summary>
    [Key(3)] public DateTime ErrorDate { get; init; }
    /// <summary>Gets the pipeline event-stream sequence identity.</summary>
    [Key(4)] public long EventId { get; init; }
    /// <summary>Gets the Start pipeline command identity.</summary>
    [Key(5)] public Guid CommandId { get; init; }
    /// <summary>Gets the pipeline Command event source.</summary>
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    /// <summary>Gets the safe pipeline failure message.</summary>
    [Key(7)] public string ErrorMessage { get; init; } = string.Empty;
    /// <summary>Gets the stable pipeline failure code.</summary>
    [Key(8)] public int ErrorCode { get; init; }
    /// <summary>Gets the standard failure classification.</summary>
    [Key(9)] public ErrorType ErrorType { get; init; }
    /// <summary>Gets optional failure diagnostic data.</summary>
    [Key(10)] public string ErrorData { get; init; } = string.Empty;
    /// <summary>Gets the UTC event receipt timestamp.</summary>
    [Key(11)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the pipeline aggregate identity.</summary>
    [Key(12)] public string AggregateId { get; init; } = string.Empty;
    /// <summary>Gets the originating command contract name.</summary>
    [Key(13)] public string CommandName { get; init; } = string.Empty;
    /// <summary>Gets safe originating command diagnostic data.</summary>
    [Key(14)] public string CommandData { get; init; } = string.Empty;
    /// <summary>Gets the originating bounded-context route text.</summary>
    [Key(15)] public string RouteTo { get; init; } = string.Empty;
    /// <summary>Gets the workflow execution identity.</summary>
    [Key(16)] public StrategyWorkflowId WorkflowId { get; init; }
    /// <summary>Gets the immutable workflow revision supplied to the pipeline.</summary>
    [Key(17)] public long InputWorkflowRevision { get; init; }
    /// <summary>Gets the workflow correlation identity.</summary>
    [Key(18)] public Guid CorrelationId { get; init; }
    /// <summary>Gets the causative pipeline command or processing-event identity.</summary>
    [Key(19)] public Guid CausationId { get; init; }
    /// <summary>Gets the pipeline workflow stage.</summary>
    [Key(20)] public StrategyWorkflowStage PipelineStage { get; init; }

    /// <summary>Gets the concrete pipeline event contract name.</summary>
    [IgnoreMember] public string EventName => nameof(TradeSelectionPipelineFailedEvent);
    /// <summary>Gets the local pipeline event-source user for diagnostics.</summary>
    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    /// <summary>Gets the standard error-event classification.</summary>
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;

    /// <summary>Initializes an empty pipeline failure event for serialization.</summary>
    public TradeSelectionPipelineFailedEvent() { }

    /// <summary>Initializes the complete keyed MessagePack pipeline failure event.</summary>
    /// <param name="subject">Persisted pipeline event subject.</param>
    /// <param name="entityId">Workflow routing identity.</param>
    /// <param name="id">Stable logical pipeline failure identity.</param>
    /// <param name="errorDate">UTC failure timestamp.</param>
    /// <param name="eventId">Pipeline event-stream sequence identity.</param>
    /// <param name="commandId">Start pipeline command identity.</param>
    /// <param name="eventSource">Pipeline Command event source.</param>
    /// <param name="errorMessage">Safe pipeline failure message.</param>
    /// <param name="errorCode">Stable pipeline failure code.</param>
    /// <param name="errorType">Standard failure classification.</param>
    /// <param name="errorData">Optional failure diagnostic data.</param>
    /// <param name="receivedOn">UTC event receipt timestamp.</param>
    /// <param name="aggregateId">Pipeline aggregate identity.</param>
    /// <param name="commandName">Originating command contract name.</param>
    /// <param name="commandData">Safe originating command diagnostic data.</param>
    /// <param name="routeTo">Originating bounded-context route text.</param>
    /// <param name="workflowId">Workflow execution identity.</param>
    /// <param name="inputWorkflowRevision">Immutable workflow input revision.</param>
    /// <param name="correlationId">Workflow correlation identity.</param>
    /// <param name="causationId">Causative command or processing-event identity.</param>
    /// <param name="pipelineStage">Pipeline workflow stage.</param>
    [SerializationConstructor]
    public TradeSelectionPipelineFailedEvent(
        ActorSubject subject,
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        Guid id,
        DateTime errorDate,
        long eventId,
        Guid commandId,
        string eventSource,
        string errorMessage,
        int errorCode,
        ErrorType errorType,
        string errorData,
        DateTime receivedOn,
        string aggregateId,
        string commandName,
        string commandData,
        string routeTo,
        StrategyWorkflowId workflowId,
        long inputWorkflowRevision,
        Guid correlationId,
        Guid causationId,
        StrategyWorkflowStage pipelineStage)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        ErrorDate = errorDate;
        EventId = eventId;
        CommandId = commandId;
        EventSource = eventSource ?? string.Empty;
        ErrorMessage = errorMessage ?? string.Empty;
        ErrorCode = errorCode;
        ErrorType = errorType;
        ErrorData = errorData ?? string.Empty;
        ReceivedOn = receivedOn;
        AggregateId = aggregateId ?? string.Empty;
        CommandName = commandName ?? string.Empty;
        CommandData = commandData ?? string.Empty;
        RouteTo = routeTo ?? string.Empty;
        WorkflowId = workflowId;
        InputWorkflowRevision = inputWorkflowRevision;
        CorrelationId = correlationId;
        CausationId = causationId;
        PipelineStage = pipelineStage;
    }
}
