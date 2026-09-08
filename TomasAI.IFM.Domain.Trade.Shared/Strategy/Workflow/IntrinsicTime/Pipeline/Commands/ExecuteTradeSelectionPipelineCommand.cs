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
public sealed record ExecuteTradeSelectionPipelineCommand : ICommand<TradeSelectionExecutionId>
{
    public const string Actor = "TradeSelectionPipelineFunction";
    public const string Verb = "Execute";
    public const int ErrorId = 23023;
    [Key(0)] public short SchemaVersion { get; init; }
    [Key(1)] public Guid CommandId { get; init; }
    [Key(2)] public ActorSubject Subject { get; init; }
    [Key(3)] public bool PostEvents { get; init; }
    [Key(4)] public TradeSelectionExecutionId EntityId { get; init; }
    [Key(5)] public int ErrorCode { get; init; } = ErrorId;
    [Key(6)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.TradeSelectionPipelineBoundedContext;
    [Key(7)] public long InputWorkflowRevision { get; init; }
    [Key(8)] public IntrinsicTimeStrategyWorkflowView WorkflowView { get; init; } = new();
    [Key(9)] public FuturesItiSignalGeneratedEvent TriggerEvent { get; init; } = new();
    [Key(10)] public Guid CorrelationId { get; init; }
    [Key(11)] public Guid CausationId { get; init; }
    [Key(12)] public DateTime RequestedAtUtc { get; init; }
    [Key(13)] public DateTime ExpiresAtUtc { get; init; }
    [Key(14)] public TradeSelection.TradeSelectionBinding SelectionBinding { get; init; } = new();
    [Key(15)] public DateTime EvaluatedAtUtc { get; init; }
    [Key(16)] public StrategyStageResultEnvelope RegimeResultEnvelope { get; init; } = new();
    [Key(17)] public StrategyStageResultEnvelope AssessmentResultEnvelope { get; init; } = new();
    [IgnoreMember] public IntrinsicTimeStrategyWorkflowEntityId WorkflowEntityId => EntityId.WorkflowEntityId;
    [IgnoreMember] public StrategyWorkflowId WorkflowId => EntityId.WorkflowId;
    [IgnoreMember] public string CommandName => nameof(ExecuteTradeSelectionPipelineCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => RequestedAtUtc;
    [IgnoreMember] public string OriginatedBy => EventSource;
    /// <summary>Hashes canonical typed fields while preserving historical trigger constructor defaults.</summary>
    public string Fingerprint() => MarketConditionAssessmentHash.Compute(NormalizeContent());

    /// <summary>Copies command/trigger records without serializing the command a second time.</summary>
    public ExecuteTradeSelectionPipelineCommand NormalizeContent() => this with
    {
        TriggerEvent = NormalizeTrigger(TriggerEvent),
        WorkflowView = WorkflowView with { TriggerEvent = NormalizeTrigger(WorkflowView.TriggerEvent) }
    };

    static FuturesItiSignalGeneratedEvent NormalizeTrigger(FuturesItiSignalGeneratedEvent trigger) => trigger with
    {
        AggregateId = trigger.AggregateId ?? "", EventSource = trigger.EventSource ?? "", CreatedBy = trigger.CreatedBy ?? "",
        ReceivedOn = trigger.ReceivedOn.Kind == DateTimeKind.Local ? trigger.ReceivedOn.ToUniversalTime() : DateTime.SpecifyKind(trigger.ReceivedOn, DateTimeKind.Utc),
        CreatedOn = trigger.CreatedOn.Kind == DateTimeKind.Local ? trigger.CreatedOn.ToUniversalTime() : DateTime.SpecifyKind(trigger.CreatedOn, DateTimeKind.Utc)
    };
}
