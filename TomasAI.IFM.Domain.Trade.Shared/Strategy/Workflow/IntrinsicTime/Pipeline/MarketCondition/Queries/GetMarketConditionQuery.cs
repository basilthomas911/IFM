using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Reference;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Queries;

[MessagePackObject]
public sealed record GetMarketConditionQuery : IQuery<MarketConditionReadModel>
{
    [IgnoreMember] public const string Actor = "MarketConditionPipelineQuery";
    [IgnoreMember] public const string Verb = "GetByWorkflowId";
    [IgnoreMember] public const int ErrorId = 23202;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public StrategyWorkflowId WorkflowId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    [IgnoreMember] public string? QueryParams { get; init; }
}

[MessagePackObject]
public sealed record GetLatestMarketConditionQuery : IQuery<MarketConditionReadModel>
{
    [IgnoreMember] public const string Actor = GetMarketConditionQuery.Actor;
    [IgnoreMember] public const string Verb = "GetLatest";
    [IgnoreMember] public const int ErrorId = 23203;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public int FundId { get; init; }
    [Key(3)] public string InstrumentRoot { get; init; } = "ES";
    [Key(4)] public TimeFrameType TargetHorizon { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    [IgnoreMember] public string? QueryParams { get; init; }
}

[MessagePackObject]
public sealed record GetMarketConditionHistoryQuery : IQuery<ICollection<MarketConditionReadModel>>
{
    [IgnoreMember] public const string Actor = GetMarketConditionQuery.Actor;
    [IgnoreMember] public const string Verb = "GetHistory";
    [IgnoreMember] public const int ErrorId = 23204;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public int FundId { get; init; }
    [Key(3)] public string InstrumentRoot { get; init; } = "ES";
    [Key(4)] public TimeFrameType TargetHorizon { get; init; }
    [Key(5)] public DateTime BeforeUtc { get; init; } = DateTime.MaxValue;
    [Key(6)] public int PageSize { get; init; } = 25;
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    [IgnoreMember] public string? QueryParams { get; init; }
}

/// <summary>Generates the current representative Market Condition decision reference without persistence.</summary>
[MessagePackObject]
public sealed record GetMarketConditionDecisionReferenceQuery : IQuery<MarketConditionDecisionReferenceDto[]>
{
    [IgnoreMember] public const string Actor = GetMarketConditionQuery.Actor;
    [IgnoreMember] public const string Verb = "GetDecisionReference";
    [IgnoreMember] public const int ErrorId = 23206;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    [IgnoreMember] public string? QueryParams { get; init; }
}
