using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;

/// <summary>Records a failed Regime Discovery pipeline execution.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record FailRegimeDiscoveryCommand : ICommand<IntrinsicTimeStrategyWorkflowEntityId>
{
    /// <summary>Workflow Command actor name.</summary>
    [IgnoreMember] public const string Actor = "IntrinsicTimeStrategyWorkflowCommand";
    /// <summary>Command verb.</summary>
    [IgnoreMember] public const string Verb = "FailRegimeDiscovery";
    /// <summary>Stable command error identifier.</summary>
    [IgnoreMember] public const int ErrorId = 21007;

    /// <summary>Gets the unique command identity.</summary>
    [Key(0)] public Guid CommandId { get; init; }
    /// <summary>Gets the workflow actor subject.</summary>
    [Key(1)] public ActorSubject Subject { get; init; }
    /// <summary>Gets whether committed domain events should be projected.</summary>
    [Key(2)] public bool PostEvents { get; init; }
    /// <summary>Gets the workflow routing entity identity.</summary>
    [Key(3)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    /// <summary>Gets the command error identifier.</summary>
    [Key(4)] public int ErrorCode { get; init; }
    /// <summary>Gets the workflow bounded-context route.</summary>
    [Key(5)] public BoundedContextName RouteTo { get; init; }
    /// <summary>Gets the workflow execution identity.</summary>
    [Key(6)] public StrategyWorkflowId WorkflowId { get; init; }
    /// <summary>Gets the workflow revision supplied to the pipeline.</summary>
    [Key(7)] public long InputWorkflowRevision { get; init; }
    /// <summary>Gets the pipeline failure event identity.</summary>
    [Key(8)] public Guid SourceEventId { get; init; }
    /// <summary>Gets the standard pipeline failure.</summary>
    [Key(9)] public StrategyPipelineFailure Failure { get; init; } = new();
    /// <summary>Gets the workflow correlation identity.</summary>
    [Key(10)] public Guid CorrelationId { get; init; }
    /// <summary>Gets the causative pipeline event identity.</summary>
    [Key(11)] public Guid CausationId { get; init; }
    /// <summary>Gets the UTC pipeline failure timestamp.</summary>
    [Key(12)] public DateTime FailedAtUtc { get; init; }

    /// <summary>Gets the concrete command contract name.</summary>
    [IgnoreMember] public string CommandName => nameof(FailRegimeDiscoveryCommand);
    /// <summary>Gets the event-source stream identity.</summary>
    [IgnoreMember] public string StreamId => Subject.StreamId;
    /// <summary>Gets the workflow command event source.</summary>
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    /// <summary>Gets the local observation timestamp for command diagnostics.</summary>
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    /// <summary>Gets the local command-origin user for diagnostics.</summary>
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Initializes an empty command for serialization.</summary>
    public FailRegimeDiscoveryCommand()
    {
        PostEvents = true;
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.IntrinsicTimeStrategyWorkflowBoundedContext;
    }

    /// <summary>Initializes the complete keyed MessagePack command contract.</summary>
    /// <param name="commandId">Unique command identity.</param>
    /// <param name="subject">Workflow actor subject.</param>
    /// <param name="postEvents">Whether events are projected.</param>
    /// <param name="entityId">Workflow routing identity.</param>
    /// <param name="errorCode">Command error identifier.</param>
    /// <param name="routeTo">Bounded-context route.</param>
    /// <param name="workflowId">Workflow execution identity.</param>
    /// <param name="inputWorkflowRevision">Workflow revision supplied to the pipeline.</param>
    /// <param name="sourceEventId">Pipeline failure event identity.</param>
    /// <param name="failure">Standard pipeline failure.</param>
    /// <param name="correlationId">Workflow correlation identity.</param>
    /// <param name="causationId">Causative pipeline event identity.</param>
    /// <param name="failedAtUtc">UTC pipeline failure timestamp.</param>
    [SerializationConstructor]
    public FailRegimeDiscoveryCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        int errorCode,
        BoundedContextName routeTo,
        StrategyWorkflowId workflowId,
        long inputWorkflowRevision,
        Guid sourceEventId,
        StrategyPipelineFailure failure,
        Guid correlationId,
        Guid causationId,
        DateTime failedAtUtc)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        WorkflowId = workflowId;
        InputWorkflowRevision = inputWorkflowRevision;
        SourceEventId = sourceEventId;
        Failure = failure;
        CorrelationId = correlationId;
        CausationId = causationId;
        FailedAtUtc = failedAtUtc;
    }
}
