using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;

/// <summary>Executes one complete, deadline-bounded Regime Discovery calculation.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record ExecuteRegimeDiscoveryPipelineCommand : ICommand<RegimeDiscoveryExecutionEntityId>
{
    [IgnoreMember] public const string Actor = "RegimeDiscoveryPipelineCommand";
    [IgnoreMember] public const string Verb = "Execute";
    [IgnoreMember] public const int ErrorId = 23001;

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public RegimeDiscoveryExecutionEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }
    [Key(6)] public long InputWorkflowRevision { get; init; }
    [Key(7)] public IntrinsicTimeStrategyWorkflowView WorkflowView { get; init; } = new();
    [Key(8)] public FuturesItiSignalGeneratedEvent TriggerEvent { get; init; } = new();
    [Key(9)] public Guid CorrelationId { get; init; }
    [Key(10)] public Guid CausationId { get; init; }
    [Key(11)] public DateTime RequestedAtUtc { get; init; }
    [Key(12)] public DateTime ExpiresAtUtc { get; init; }
    [Key(13)] public RegimeDiscoveryParameterSet ParameterSet { get; init; } = new();
    [Key(14)] public string ParameterPayloadSha256 { get; init; } = string.Empty;
    [Key(15)] public TimeFrameType TargetHorizon { get; init; }

    /// <summary>Gets the owning workflow entity without duplicating serialized identity.</summary>
    [IgnoreMember] public IntrinsicTimeStrategyWorkflowEntityId WorkflowEntityId => EntityId.WorkflowEntityId;
    /// <summary>Gets the owning workflow execution without duplicating serialized identity.</summary>
    [IgnoreMember] public StrategyWorkflowId WorkflowId => EntityId.WorkflowId;
    [IgnoreMember] public string CommandName => nameof(ExecuteRegimeDiscoveryPipelineCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Initializes the default route metadata.</summary>
    public ExecuteRegimeDiscoveryPipelineCommand()
    {
        PostEvents = true;
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.RegimeDiscoveryPipelineBoundedContext;
    }

    /// <summary>Initializes the complete keyed Execute contract.</summary>
    [SerializationConstructor]
    public ExecuteRegimeDiscoveryPipelineCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        RegimeDiscoveryExecutionEntityId entityId,
        int errorCode,
        BoundedContextName routeTo,
        long inputWorkflowRevision,
        IntrinsicTimeStrategyWorkflowView workflowView,
        FuturesItiSignalGeneratedEvent triggerEvent,
        Guid correlationId,
        Guid causationId,
        DateTime requestedAtUtc,
        DateTime expiresAtUtc,
        RegimeDiscoveryParameterSet parameterSet,
        string parameterPayloadSha256,
        TimeFrameType targetHorizon)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        InputWorkflowRevision = inputWorkflowRevision;
        WorkflowView = workflowView ?? new IntrinsicTimeStrategyWorkflowView();
        TriggerEvent = triggerEvent ?? new FuturesItiSignalGeneratedEvent();
        CorrelationId = correlationId;
        CausationId = causationId;
        RequestedAtUtc = requestedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        ParameterSet = parameterSet ?? new RegimeDiscoveryParameterSet();
        ParameterPayloadSha256 = parameterPayloadSha256 ?? string.Empty;
        TargetHorizon = targetHorizon;
    }
}
