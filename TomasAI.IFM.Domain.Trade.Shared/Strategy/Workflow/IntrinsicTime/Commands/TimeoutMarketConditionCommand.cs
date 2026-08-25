using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;

/// <summary>Times out the active Market Condition workflow stage when its revision still matches.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record TimeoutMarketConditionCommand : ICommand<IntrinsicTimeStrategyWorkflowEntityId>
{
    /// <summary>Workflow Command actor name.</summary>
    [IgnoreMember] public const string Actor = "IntrinsicTimeStrategyWorkflowCommand";
    /// <summary>Command verb.</summary>
    [IgnoreMember] public const string Verb = "TimeoutMarketCondition";
    /// <summary>Stable command error identifier.</summary>
    [IgnoreMember] public const int ErrorId = 21013;

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
    /// <summary>Gets the workflow revision that may be timed out.</summary>
    [Key(7)] public long ExpectedWorkflowRevision { get; init; }
    /// <summary>Gets the stage that may be timed out.</summary>
    [Key(8)] public StrategyWorkflowStage ExpectedStage { get; init; }
    /// <summary>Gets the timeout operation identity.</summary>
    [Key(9)] public Guid TimeoutId { get; init; }
    /// <summary>Gets the UTC timeout timestamp.</summary>
    [Key(10)] public DateTime TimedOutAtUtc { get; init; }

    /// <summary>Gets the concrete command contract name.</summary>
    [IgnoreMember] public string CommandName => nameof(TimeoutMarketConditionCommand);
    /// <summary>Gets the event-source stream identity.</summary>
    [IgnoreMember] public string StreamId => Subject.StreamId;
    /// <summary>Gets the workflow command event source.</summary>
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    /// <summary>Gets the local observation timestamp for command diagnostics.</summary>
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    /// <summary>Gets the local command-origin user for diagnostics.</summary>
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Initializes an empty command for serialization.</summary>
    public TimeoutMarketConditionCommand()
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
    /// <param name="expectedWorkflowRevision">Workflow revision that may be timed out.</param>
    /// <param name="expectedStage">Stage that may be timed out.</param>
    /// <param name="timeoutId">Timeout operation identity.</param>
    /// <param name="timedOutAtUtc">UTC timeout timestamp.</param>
    [SerializationConstructor]
    public TimeoutMarketConditionCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        int errorCode,
        BoundedContextName routeTo,
        StrategyWorkflowId workflowId,
        long expectedWorkflowRevision,
        StrategyWorkflowStage expectedStage,
        Guid timeoutId,
        DateTime timedOutAtUtc)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        WorkflowId = workflowId;
        ExpectedWorkflowRevision = expectedWorkflowRevision;
        ExpectedStage = expectedStage;
        TimeoutId = timeoutId;
        TimedOutAtUtc = timedOutAtUtc;
    }
}
