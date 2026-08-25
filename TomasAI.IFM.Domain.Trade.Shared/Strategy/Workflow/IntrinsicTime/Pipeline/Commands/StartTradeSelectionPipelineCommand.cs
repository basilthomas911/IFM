using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;

/// <summary>Requests processing by the Trade Selection strategy pipeline.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record StartTradeSelectionPipelineCommand : ICommand<IntrinsicTimeStrategyWorkflowEntityId>
{
    /// <summary>Future pipeline Command actor name.</summary>
    [IgnoreMember] public const string Actor = "TradeSelectionPipelineCommand";
    /// <summary>Pipeline start verb.</summary>
    [IgnoreMember] public const string Verb = "Start";
    /// <summary>Stable command error identifier.</summary>
    [IgnoreMember] public const int ErrorId = 23003;

    /// <summary>Gets the deterministic pipeline command identity.</summary>
    [Key(0)] public Guid CommandId { get; init; }
    /// <summary>Gets the pipeline Command actor subject.</summary>
    [Key(1)] public ActorSubject Subject { get; init; }
    /// <summary>Gets whether committed pipeline events should be projected.</summary>
    [Key(2)] public bool PostEvents { get; init; }
    /// <summary>Gets the workflow routing entity identity.</summary>
    [Key(3)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    /// <summary>Gets the command error identifier.</summary>
    [Key(4)] public int ErrorCode { get; init; }
    /// <summary>Gets the pipeline bounded-context route.</summary>
    [Key(5)] public BoundedContextName RouteTo { get; init; }
    /// <summary>Gets the workflow execution identity.</summary>
    [Key(6)] public StrategyWorkflowId WorkflowId { get; init; }
    /// <summary>Gets the immutable workflow revision supplied to the pipeline.</summary>
    [Key(7)] public long InputWorkflowRevision { get; init; }
    /// <summary>Gets the immutable workflow snapshot supplied to the pipeline.</summary>
    [Key(8)] public IntrinsicTimeStrategyWorkflowState WorkflowState { get; init; } = new();
    /// <summary>Gets the original ITI signal event.</summary>
    [Key(9)] public FuturesItiSignalGeneratedEvent TriggerEvent { get; init; } = new();
    /// <summary>Gets the workflow correlation identity.</summary>
    [Key(10)] public Guid CorrelationId { get; init; }
    /// <summary>Gets the causative workflow lifecycle event identity.</summary>
    [Key(11)] public Guid CausationId { get; init; }
    /// <summary>Gets the UTC pipeline request timestamp.</summary>
    [Key(12)] public DateTime RequestedAtUtc { get; init; }
    /// <summary>Gets the optional UTC pipeline completion deadline.</summary>
    [Key(13)] public DateTime? ExpectedCompletionAtUtc { get; init; }

    /// <summary>Gets the concrete command contract name.</summary>
    [IgnoreMember] public string CommandName => nameof(StartTradeSelectionPipelineCommand);
    /// <summary>Gets the pipeline event-source stream identity.</summary>
    [IgnoreMember] public string StreamId => Subject.StreamId;
    /// <summary>Gets the pipeline Command event source.</summary>
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    /// <summary>Gets the local observation timestamp for diagnostics.</summary>
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    /// <summary>Gets the local command-origin user for diagnostics.</summary>
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Initializes an empty pipeline command for serialization.</summary>
    public StartTradeSelectionPipelineCommand()
    {
        PostEvents = true;
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.TradeSelectionPipelineBoundedContext;
    }

    /// <summary>Initializes the complete keyed MessagePack pipeline command.</summary>
    /// <param name="commandId">Deterministic pipeline command identity.</param>
    /// <param name="subject">Pipeline Command actor subject.</param>
    /// <param name="postEvents">Whether committed events are projected.</param>
    /// <param name="entityId">Workflow routing identity.</param>
    /// <param name="errorCode">Command error identifier.</param>
    /// <param name="routeTo">Pipeline bounded-context route.</param>
    /// <param name="workflowId">Workflow execution identity.</param>
    /// <param name="inputWorkflowRevision">Immutable workflow input revision.</param>
    /// <param name="workflowState">Immutable workflow snapshot.</param>
    /// <param name="triggerEvent">Original ITI signal event.</param>
    /// <param name="correlationId">Workflow correlation identity.</param>
    /// <param name="causationId">Causative workflow lifecycle event identity.</param>
    /// <param name="requestedAtUtc">UTC request timestamp.</param>
    /// <param name="expectedCompletionAtUtc">Optional UTC completion deadline.</param>
    [SerializationConstructor]
    public StartTradeSelectionPipelineCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        int errorCode,
        BoundedContextName routeTo,
        StrategyWorkflowId workflowId,
        long inputWorkflowRevision,
        IntrinsicTimeStrategyWorkflowState workflowState,
        FuturesItiSignalGeneratedEvent triggerEvent,
        Guid correlationId,
        Guid causationId,
        DateTime requestedAtUtc,
        DateTime? expectedCompletionAtUtc)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        WorkflowId = workflowId;
        InputWorkflowRevision = inputWorkflowRevision;
        WorkflowState = workflowState;
        TriggerEvent = triggerEvent;
        CorrelationId = correlationId;
        CausationId = causationId;
        RequestedAtUtc = requestedAtUtc;
        ExpectedCompletionAtUtc = expectedCompletionAtUtc;
    }
}
