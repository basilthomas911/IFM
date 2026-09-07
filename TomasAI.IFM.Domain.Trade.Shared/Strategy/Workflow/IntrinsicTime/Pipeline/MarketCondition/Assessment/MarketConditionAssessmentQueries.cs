using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

[MessagePackObject]
public sealed record GetMarketConditionAssessmentQuery : IQuery<MarketConditionAssessmentCompletedEvent>
{
    public const string Actor = "MarketConditionPipelineQuery";
    public const string Verb = "GetAssessmentByWorkflowId";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public StrategyWorkflowId WorkflowId { get; init; }
    [IgnoreMember] public int ErrorCode => 23220;
    [IgnoreMember] public string? QueryParams { get; init; }
}
[MessagePackObject]
public sealed record GetMarketConditionAssessmentHistoryQuery : IQuery<MarketConditionAssessmentCompletedEvent[]>
{
    public const string Actor = GetMarketConditionAssessmentQuery.Actor;
    public const string Verb = "GetAssessmentHistory";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public string MarketProfileId { get; init; } = "";
    [Key(3)] public string InstrumentRoot { get; init; } = "ES";
    [Key(4)] public TimeFrameType TargetHorizon { get; init; }
    [Key(5)] public DateTime BeforeUtc { get; init; } = DateTime.SpecifyKind(DateTime.MaxValue,DateTimeKind.Utc);
    [Key(6)] public int PageSize { get; init; } = 25;
    [IgnoreMember] public int ErrorCode => 23221;
    [IgnoreMember] public string? QueryParams { get; init; }
}

public interface IMarketConditionAssessmentQueryApi
{
    ValueTask<ServiceResult<MarketConditionAssessmentCompletedEvent>> GetAsync(StrategyWorkflowId workflowId, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<MarketConditionAssessmentCompletedEvent[]>> HistoryAsync(string profile, string root, TimeFrameType horizon, DateTime beforeUtc, int pageSize = 25, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<MarketConditionAssessmentCompletedEvent[]>> LatestAsync(string profile, string root, TimeFrameType horizon, CancellationToken cancellationToken = default);
}
