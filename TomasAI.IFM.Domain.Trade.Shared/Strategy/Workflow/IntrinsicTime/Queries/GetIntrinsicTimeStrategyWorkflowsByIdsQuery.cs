using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;

/// <summary>Gets the active workflow for one stable workflow entity.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetIntrinsicTimeStrategyWorkflowsByIdsQuery : IQuery<IntrinsicTimeStrategyWorkflowReadModel[]>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetIntrinsicTimeStrategyWorkflowsByIdsQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="workflowIds">The WorkflowIds field.</param>
    /// <param name="minimumRevisions">The MinimumRevisions field.</param>
    [SerializationConstructor]
    public GetIntrinsicTimeStrategyWorkflowsByIdsQuery(ActorSubject subject, IActorEntityId entityId, StrategyWorkflowId[] workflowIds, long[] minimumRevisions)
    {
        Subject = subject;
        EntityId = entityId;
        WorkflowIds = workflowIds;
        MinimumRevisions = minimumRevisions;
    }
    [IgnoreMember] public const string Actor = GetIntrinsicTimeStrategyWorkflowByIdQuery.Actor;
    [IgnoreMember] public const string Verb = "GetByIds";
    [IgnoreMember] public const int ErrorId = 25012;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    [IgnoreMember] public string? QueryParams { get; init; }
    [Key(2)] public StrategyWorkflowId[] WorkflowIds { get; init; } = [];
    [Key(3)] public long[] MinimumRevisions { get; init; } = [];
}
