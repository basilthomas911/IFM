using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;

/// <summary>Requests execution of a new Intrinsic Time Strategy workflow from an eligible ITI signal.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record ExecuteIntrinsicTimeStrategyWorkflowCommand : ICommand<IntrinsicTimeStrategyWorkflowEntityId>
{
    /// <summary>Workflow Command actor name.</summary>
    [IgnoreMember] public const string Actor = "IntrinsicTimeStrategyWorkflowCommand";
    /// <summary>Command verb.</summary>
    [IgnoreMember] public const string Verb = "Execute";
    /// <summary>Stable command error identifier.</summary>
    [IgnoreMember] public const int ErrorId = 21001;

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
    /// <summary>Gets the proposed workflow execution identity.</summary>
    [Key(6)] public StrategyWorkflowId ProposedWorkflowId { get; init; }
    /// <summary>Gets the source ITI event identity.</summary>
    [Key(7)] public Guid TriggerEventId { get; init; }
    /// <summary>Gets the original ITI signal event.</summary>
    [Key(8)] public FuturesItiSignalGeneratedEvent TriggerEvent { get; init; } = new();
    /// <summary>Gets the workflow correlation identity.</summary>
    [Key(9)] public Guid CorrelationId { get; init; }
    /// <summary>Gets the causative event identity.</summary>
    [Key(10)] public Guid CausationId { get; init; }
    /// <summary>Gets the UTC request timestamp.</summary>
    [Key(11)] public DateTime RequestedAtUtc { get; init; }
    /// <summary>Gets the workflow definition version.</summary>
    [Key(12)] public int WorkflowDefinitionVersion { get; init; }
    /// <summary>Gets the exact immutable Regime Discovery parameters selected for this workflow.</summary>
    [Key(13)] public RegimeDiscoveryParameterSet RegimeDiscoveryParameterSet { get; init; } = new();
    /// <summary>Gets the canonical SHA-256 hash of the selected parameter payload.</summary>
    [Key(14)] public string RegimeDiscoveryParameterPayloadSha256 { get; init; } = string.Empty;

    /// <summary>Gets the concrete command contract name.</summary>
    [IgnoreMember] public string CommandName => nameof(ExecuteIntrinsicTimeStrategyWorkflowCommand);
    /// <summary>Gets the event-source stream identity.</summary>
    [IgnoreMember] public string StreamId => Subject.StreamId;
    /// <summary>Gets the workflow command event source.</summary>
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    /// <summary>Gets the local observation timestamp for command diagnostics.</summary>
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    /// <summary>Gets the local command-origin user for diagnostics.</summary>
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Initializes an empty command for serialization.</summary>
    public ExecuteIntrinsicTimeStrategyWorkflowCommand()
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
    /// <param name="proposedWorkflowId">Proposed workflow execution identity.</param>
    /// <param name="triggerEventId">Source ITI event identity.</param>
    /// <param name="triggerEvent">Original ITI signal event.</param>
    /// <param name="correlationId">Workflow correlation identity.</param>
    /// <param name="causationId">Causative event identity.</param>
    /// <param name="requestedAtUtc">UTC request timestamp.</param>
    /// <param name="workflowDefinitionVersion">Workflow definition version.</param>
    [SerializationConstructor]
    public ExecuteIntrinsicTimeStrategyWorkflowCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        int errorCode,
        BoundedContextName routeTo,
        StrategyWorkflowId proposedWorkflowId,
        Guid triggerEventId,
        FuturesItiSignalGeneratedEvent triggerEvent,
        Guid correlationId,
        Guid causationId,
        DateTime requestedAtUtc,
        int workflowDefinitionVersion,
        RegimeDiscoveryParameterSet regimeDiscoveryParameterSet,
        string regimeDiscoveryParameterPayloadSha256)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        ProposedWorkflowId = proposedWorkflowId;
        TriggerEventId = triggerEventId;
        TriggerEvent = triggerEvent;
        CorrelationId = correlationId;
        CausationId = causationId;
        RequestedAtUtc = requestedAtUtc;
        WorkflowDefinitionVersion = workflowDefinitionVersion;
        RegimeDiscoveryParameterSet = regimeDiscoveryParameterSet;
        RegimeDiscoveryParameterPayloadSha256 = regimeDiscoveryParameterPayloadSha256 ?? string.Empty;
    }
}
