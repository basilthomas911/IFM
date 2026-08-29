using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;

[MessagePackObject(AllowPrivate = true)]
public sealed record ExecuteMarketConditionPipelineCommand : ICommand<MarketConditionExecutionEntityId>
{
    [IgnoreMember] public const string Actor = "MarketConditionPipelineFunction";
    [IgnoreMember] public const string Verb = "Execute";
    [IgnoreMember] public const int ErrorId = 23002;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public MarketConditionExecutionEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }
    [Key(6)] public long InputWorkflowRevision { get; init; }
    [Key(7)] public IntrinsicTimeStrategyWorkflowView WorkflowView { get; init; } = new();
    [Key(8)] public FuturesItiSignalGeneratedEvent TriggerEvent { get; init; } = new();
    [Key(9)] public Guid CorrelationId { get; init; }
    [Key(10)] public Guid CausationId { get; init; }
    [Key(11)] public DateTime RequestedAtUtc { get; init; }
    [Key(12)] public DateTime ExpiresAtUtc { get; init; }
    [Key(13)] public MarketConditionParameterSet ParameterSet { get; init; } = new();
    [Key(14)] public string ParameterPayloadSha256 { get; init; } = string.Empty;
    [Key(15)] public TimeFrameType TargetHorizon { get; init; }
    [Key(16)] public int FundId { get; init; }
    [Key(17)] public string InstrumentRoot { get; init; } = "ES";

    [IgnoreMember] public IntrinsicTimeStrategyWorkflowEntityId WorkflowEntityId => EntityId.WorkflowEntityId;
    [IgnoreMember] public StrategyWorkflowId WorkflowId => EntityId.WorkflowId;
    [IgnoreMember] public string CommandName => nameof(ExecuteMarketConditionPipelineCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    public ExecuteMarketConditionPipelineCommand()
    {
        PostEvents = true;
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.MarketConditionPipelineBoundedContext;
    }

    [SerializationConstructor]
    public ExecuteMarketConditionPipelineCommand(
        Guid commandId, ActorSubject subject, bool postEvents, MarketConditionExecutionEntityId entityId,
        int errorCode, BoundedContextName routeTo, long inputWorkflowRevision,
        IntrinsicTimeStrategyWorkflowView workflowView, FuturesItiSignalGeneratedEvent triggerEvent,
        Guid correlationId, Guid causationId, DateTime requestedAtUtc, DateTime expiresAtUtc,
        MarketConditionParameterSet parameterSet, string parameterPayloadSha256,
        TimeFrameType targetHorizon, int fundId, string instrumentRoot)
    {
        CommandId = commandId; Subject = subject; PostEvents = postEvents; EntityId = entityId;
        ErrorCode = errorCode; RouteTo = routeTo; InputWorkflowRevision = inputWorkflowRevision;
        WorkflowView = workflowView ?? new(); TriggerEvent = triggerEvent ?? new();
        CorrelationId = correlationId; CausationId = causationId; RequestedAtUtc = requestedAtUtc;
        ExpiresAtUtc = expiresAtUtc; ParameterSet = parameterSet ?? new();
        ParameterPayloadSha256 = parameterPayloadSha256 ?? string.Empty; TargetHorizon = targetHorizon;
        FundId = fundId; InstrumentRoot = instrumentRoot ?? string.Empty;
    }
}
