using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;

[MessagePackObject(AllowPrivate = true)]
public sealed record ExecuteMarketConditionAssessmentCommand : ICommand<MarketConditionAssessmentExecutionId>
{

    /// <summary>Creates an empty command for serialization and existing callers.</summary>
    public ExecuteMarketConditionAssessmentCommand() { }

    /// <summary>Rehydrates every published command field in permanent numeric-key order.</summary>
    /// <param name="schemaVersion">The SchemaVersion field.</param>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="errorCode">The ErrorCode field.</param>
    /// <param name="routeTo">The RouteTo field.</param>
    /// <param name="inputWorkflowRevision">The InputWorkflowRevision field.</param>
    /// <param name="workflowView">The WorkflowView field.</param>
    /// <param name="triggerEvent">The TriggerEvent field.</param>
    /// <param name="correlationId">The CorrelationId field.</param>
    /// <param name="causationId">The CausationId field.</param>
    /// <param name="requestedAtUtc">The RequestedAtUtc field.</param>
    /// <param name="expiresAtUtc">The ExpiresAtUtc field.</param>
    /// <param name="parameterSet">The ParameterSet field.</param>
    /// <param name="parameterPayloadSha256">The ParameterPayloadSha256 field.</param>
    /// <param name="regimeResultEnvelope">The RegimeResultEnvelope field.</param>
    /// <param name="regimePayloadSha256">The RegimePayloadSha256 field.</param>
    /// <param name="marketProfileId">The MarketProfileId field.</param>
    /// <param name="instrumentRoot">The InstrumentRoot field.</param>
    /// <param name="targetHorizon">The TargetHorizon field.</param>
    [SerializationConstructor]
    public ExecuteMarketConditionAssessmentCommand(short schemaVersion, Guid commandId, ActorSubject subject, bool postEvents, MarketConditionAssessmentExecutionId entityId, int errorCode, BoundedContextName routeTo, long inputWorkflowRevision, IntrinsicTimeStrategyWorkflowView workflowView, FuturesItiSignalGeneratedEvent triggerEvent, Guid correlationId, Guid causationId, DateTime requestedAtUtc, DateTime expiresAtUtc, MarketConditionAssessmentParameterSet parameterSet, string parameterPayloadSha256, StrategyStageResultEnvelope regimeResultEnvelope, string regimePayloadSha256, string marketProfileId, string instrumentRoot, TimeFrameType targetHorizon)
    {
        SchemaVersion = schemaVersion;
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        InputWorkflowRevision = inputWorkflowRevision;
        WorkflowView = workflowView;
        TriggerEvent = triggerEvent;
        CorrelationId = correlationId;
        CausationId = causationId;
        RequestedAtUtc = requestedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        ParameterSet = parameterSet;
        ParameterPayloadSha256 = parameterPayloadSha256;
        RegimeResultEnvelope = regimeResultEnvelope;
        RegimePayloadSha256 = regimePayloadSha256;
        MarketProfileId = marketProfileId;
        InstrumentRoot = instrumentRoot;
        TargetHorizon = targetHorizon;
    }
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
        var canonical = this with
        {
            TriggerEvent = NormalizeTrigger(TriggerEvent),
            WorkflowView = WorkflowView with { TriggerEvent = NormalizeTrigger(WorkflowView.TriggerEvent) }
        };
        // PostgreSQL JSON round trips may normalize decimal scale. Preserve numeric meaning in the identity.
        return MarketConditionAssessmentHash.Compute(canonical);
    }
    /// <summary>Preserves the historical trigger-constructor normalization without a serialization round trip.</summary>
    static FuturesItiSignalGeneratedEvent NormalizeTrigger(FuturesItiSignalGeneratedEvent trigger) => trigger with
    {
        AggregateId = trigger.AggregateId ?? "", EventSource = trigger.EventSource ?? "", CreatedBy = trigger.CreatedBy ?? "",
        ReceivedOn = trigger.ReceivedOn.Kind == DateTimeKind.Local ? trigger.ReceivedOn.ToUniversalTime() : DateTime.SpecifyKind(trigger.ReceivedOn, DateTimeKind.Utc),
        CreatedOn = trigger.CreatedOn.Kind == DateTimeKind.Local ? trigger.CreatedOn.ToUniversalTime() : DateTime.SpecifyKind(trigger.CreatedOn, DateTimeKind.Utc)
    };

}
