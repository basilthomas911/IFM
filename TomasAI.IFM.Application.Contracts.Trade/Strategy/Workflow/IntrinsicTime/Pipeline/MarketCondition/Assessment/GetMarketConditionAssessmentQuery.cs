using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

[MessagePackObject(AllowPrivate = true)]
public sealed record GetMarketConditionAssessmentQuery : IQuery<MarketConditionAssessmentCompletedEvent>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetMarketConditionAssessmentQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="workflowId">The WorkflowId field.</param>
    [SerializationConstructor]
    public GetMarketConditionAssessmentQuery(ActorSubject subject, IActorEntityId entityId, StrategyWorkflowId workflowId)
    {
        Subject = subject;
        EntityId = entityId;
        WorkflowId = workflowId;
    }
    public const string Actor = "MarketConditionPipelineQuery";
    public const string Verb = "GetAssessmentByWorkflowId";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public StrategyWorkflowId WorkflowId { get; init; }
    [IgnoreMember] public int ErrorCode => 23220;
    [IgnoreMember] public string? QueryParams { get; init; }
}
