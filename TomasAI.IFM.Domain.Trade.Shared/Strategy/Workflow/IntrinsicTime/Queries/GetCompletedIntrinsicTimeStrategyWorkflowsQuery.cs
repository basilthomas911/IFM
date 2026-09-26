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

/// <summary>Gets successfully completed workflows for a bounded date range.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetCompletedIntrinsicTimeStrategyWorkflowsQuery : IQuery<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetCompletedIntrinsicTimeStrategyWorkflowsQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="startDate">The StartDate field.</param>
    /// <param name="endDate">The EndDate field.</param>
    /// <param name="pageSize">The PageSize field.</param>
    [SerializationConstructor]
    public GetCompletedIntrinsicTimeStrategyWorkflowsQuery(ActorSubject subject, IActorEntityId entityId, DateOnly startDate, DateOnly endDate, int pageSize)
    {
        Subject = subject;
        EntityId = entityId;
        StartDate = startDate;
        EndDate = endDate;
        PageSize = pageSize;
    }
    /// <summary>Query actor name.</summary>
    [IgnoreMember] public const string Actor = GetIntrinsicTimeStrategyWorkflowByIdQuery.Actor;
    /// <summary>Query verb.</summary>
    [IgnoreMember] public const string Verb = "GetCompleted";
    /// <summary>Stable query error code.</summary>
    [IgnoreMember] public const int ErrorId = 25007;
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    /// <inheritdoc />
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [IgnoreMember] public string? QueryParams { get; init; }
    /// <summary>Gets the inclusive start date.</summary>
    [Key(2)] public DateOnly StartDate { get; init; }
    /// <summary>Gets the inclusive end date.</summary>
    [Key(3)] public DateOnly EndDate { get; init; }
    /// <summary>Gets the maximum returned item count.</summary>
    [Key(4)] public int PageSize { get; init; } = 100;
}
