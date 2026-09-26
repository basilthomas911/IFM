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

/// <summary>Gets an event-ordered workflow timeline page.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetIntrinsicTimeStrategyWorkflowTimelineQuery : IQuery<IntrinsicTimeStrategyWorkflowTimelineReadModel[]>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetIntrinsicTimeStrategyWorkflowTimelineQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="workflowId">The WorkflowId field.</param>
    /// <param name="afterEventId">The AfterEventId field.</param>
    /// <param name="pageSize">The PageSize field.</param>
    [SerializationConstructor]
    public GetIntrinsicTimeStrategyWorkflowTimelineQuery(ActorSubject subject, IActorEntityId entityId, StrategyWorkflowId workflowId, long afterEventId, int pageSize)
    {
        Subject = subject;
        EntityId = entityId;
        WorkflowId = workflowId;
        AfterEventId = afterEventId;
        PageSize = pageSize;
    }
    /// <summary>Query actor name.</summary>
    [IgnoreMember] public const string Actor = GetIntrinsicTimeStrategyWorkflowByIdQuery.Actor;
    /// <summary>Query verb.</summary>
    [IgnoreMember] public const string Verb = "GetTimeline";
    /// <summary>Stable query error code.</summary>
    [IgnoreMember] public const int ErrorId = 25005;
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    /// <inheritdoc />
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [IgnoreMember] public string? QueryParams { get; init; }
    /// <summary>Gets the requested workflow identity.</summary>
    [Key(2)] public StrategyWorkflowId WorkflowId { get; init; }
    /// <summary>Gets the exclusive event-id page cursor.</summary>
    [Key(3)] public long AfterEventId { get; init; }
    /// <summary>Gets the maximum returned item count.</summary>
    [Key(4)] public int PageSize { get; init; } = 100;
}
