using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;

[MessagePackObject]
public sealed record ExecuteMarketConditionAssessmentCommand : ICommand<MarketConditionAssessmentExecutionId>
{
    public const string Actor = "MarketConditionPipelineFunction";
    public const string Verb = "Assess";
    public const int ErrorId = 23022;
    [Key(0)] public short SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CommandId { get; init; }
    [Key(2)] public ActorSubject Subject { get; init; }
    [Key(3)] public bool PostEvents { get; init; } = true;
    [Key(4)] public MarketConditionAssessmentExecutionId EntityId { get; init; }
    [Key(5)] public int ErrorCode { get; init; } = ErrorId;
    [Key(6)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.MarketConditionPipelineBoundedContext;
    [Key(7)] public long InputWorkflowRevision { get; init; }
    [Key(8)] public IntrinsicTimeStrategyWorkflowView WorkflowView { get; init; } = new();
    [Key(9)] public FuturesItiSignalGeneratedEvent TriggerEvent { get; init; } = new();
    [Key(10)] public Guid CorrelationId { get; init; }
    [Key(11)] public Guid CausationId { get; init; }
    [Key(12)] public DateTime RequestedAtUtc { get; init; }
    [Key(13)] public DateTime ExpiresAtUtc { get; init; }
    [Key(14)] public MarketConditionAssessmentParameterSet ParameterSet { get; init; } = new();
    [Key(15)] public string ParameterPayloadSha256 { get; init; } = string.Empty;
    [Key(16)] public StrategyStageResultEnvelope RegimeResultEnvelope { get; init; } = new();
    [Key(17)] public string RegimePayloadSha256 { get; init; } = string.Empty;
    [Key(18)] public string MarketProfileId { get; init; } = string.Empty;
    [Key(19)] public string InstrumentRoot { get; init; } = string.Empty;
    [Key(20)] public TimeFrameType TargetHorizon { get; init; }
    [IgnoreMember] public IntrinsicTimeStrategyWorkflowEntityId WorkflowEntityId => EntityId.WorkflowEntityId;
    [IgnoreMember] public StrategyWorkflowId WorkflowId => EntityId.WorkflowId;
    [IgnoreMember] public string CommandName => nameof(ExecuteMarketConditionAssessmentCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => RequestedAtUtc;
    [IgnoreMember] public string OriginatedBy => EventSource;
    public string Fingerprint()
    {
        // Older trigger constructors normalize nullable diagnostic strings to empty strings on receipt.
        // Fingerprint the canonical wire value so a retransmission has the same identity.
        var canonical = MessagePackSerializer.Deserialize<ExecuteMarketConditionAssessmentCommand>(MessagePackSerializer.Serialize(this));
        // PostgreSQL JSON round trips may normalize decimal scale. Preserve numeric meaning in the identity.
        return MarketConditionAssessmentHash.Compute(canonical);
    }
}
