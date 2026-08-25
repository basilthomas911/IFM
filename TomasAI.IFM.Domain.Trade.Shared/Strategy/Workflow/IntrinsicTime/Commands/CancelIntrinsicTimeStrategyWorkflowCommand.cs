using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;

/// <summary>Cancels the active Intrinsic Time Strategy workflow when its revision still matches.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record CancelIntrinsicTimeStrategyWorkflowCommand : ICommand<IntrinsicTimeStrategyWorkflowEntityId>
{
    /// <summary>Workflow Command actor name.</summary>
    [IgnoreMember] public const string Actor = "IntrinsicTimeStrategyWorkflowCommand";
    /// <summary>Command verb.</summary>
    [IgnoreMember] public const string Verb = "Cancel";
    /// <summary>Stable command error identifier.</summary>
    [IgnoreMember] public const int ErrorId = 21017;

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
    /// <summary>Gets the workflow revision that may be cancelled.</summary>
    [Key(7)] public long ExpectedWorkflowRevision { get; init; }
    /// <summary>Gets the stable cancellation reason code.</summary>
    [Key(8)] public string ReasonCode { get; init; } = string.Empty;
    /// <summary>Gets the UTC cancellation request timestamp.</summary>
    [Key(9)] public DateTime RequestedAtUtc { get; init; }
    /// <summary>Gets the requesting operator or component.</summary>
    [Key(10)] public string RequestedBy { get; init; } = string.Empty;

    /// <summary>Gets the concrete command contract name.</summary>
    [IgnoreMember] public string CommandName => nameof(CancelIntrinsicTimeStrategyWorkflowCommand);
    /// <summary>Gets the event-source stream identity.</summary>
    [IgnoreMember] public string StreamId => Subject.StreamId;
    /// <summary>Gets the workflow command event source.</summary>
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    /// <summary>Gets the local observation timestamp for command diagnostics.</summary>
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    /// <summary>Gets the local command-origin user for diagnostics.</summary>
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Initializes an empty command for serialization.</summary>
    public CancelIntrinsicTimeStrategyWorkflowCommand()
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
    /// <param name="expectedWorkflowRevision">Workflow revision that may be cancelled.</param>
    /// <param name="reasonCode">Stable cancellation reason code.</param>
    /// <param name="requestedAtUtc">UTC cancellation request timestamp.</param>
    /// <param name="requestedBy">Requesting operator or component.</param>
    [SerializationConstructor]
    public CancelIntrinsicTimeStrategyWorkflowCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        int errorCode,
        BoundedContextName routeTo,
        StrategyWorkflowId workflowId,
        long expectedWorkflowRevision,
        string reasonCode,
        DateTime requestedAtUtc,
        string requestedBy)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        WorkflowId = workflowId;
        ExpectedWorkflowRevision = expectedWorkflowRevision;
        ReasonCode = reasonCode ?? string.Empty;
        RequestedAtUtc = requestedAtUtc;
        RequestedBy = requestedBy ?? string.Empty;
    }
}
