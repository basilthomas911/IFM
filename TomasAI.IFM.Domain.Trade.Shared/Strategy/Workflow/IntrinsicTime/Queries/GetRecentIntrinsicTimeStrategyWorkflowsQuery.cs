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

/// <summary>Gets recent workflow executions for one entity.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetRecentIntrinsicTimeStrategyWorkflowsQuery : IQuery<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetRecentIntrinsicTimeStrategyWorkflowsQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="workflowEntityId">The WorkflowEntityId field.</param>
    /// <param name="beforeUtc">The BeforeUtc field.</param>
    /// <param name="pageSize">The PageSize field.</param>
    [SerializationConstructor]
    public GetRecentIntrinsicTimeStrategyWorkflowsQuery(ActorSubject subject, IActorEntityId entityId, string workflowEntityId, DateTime beforeUtc, int pageSize)
    {
        Subject = subject;
        EntityId = entityId;
        WorkflowEntityId = workflowEntityId;
        BeforeUtc = beforeUtc;
        PageSize = pageSize;
    }
    /// <summary>Query actor name.</summary>
    [IgnoreMember] public const string Actor = GetIntrinsicTimeStrategyWorkflowByIdQuery.Actor;
    /// <summary>Query verb.</summary>
    [IgnoreMember] public const string Verb = "GetRecent";
    /// <summary>Stable query error code.</summary>
    [IgnoreMember] public const int ErrorId = 25006;
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    /// <inheritdoc />
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [IgnoreMember] public string? QueryParams { get; init; }
    /// <summary>Gets the formatted workflow entity identity.</summary>
    [Key(2)] public string WorkflowEntityId { get; init; } = string.Empty;
    /// <summary>Gets the exclusive UTC page cursor.</summary>
    [Key(3)] public DateTime BeforeUtc { get; init; } = DateTime.MaxValue;
    /// <summary>Gets the maximum returned item count.</summary>
    [Key(4)] public int PageSize { get; init; } = 100;
}
