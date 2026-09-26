using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

/// <summary>Reads the bounded stage timeline for a strategy position and value date.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetPositionExitWorkflowTimelineQuery : IQuery<PositionExitWorkflowHistoryPage>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetPositionExitWorkflowTimelineQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="positionId">The PositionId field.</param>
    /// <param name="valueDate">The ValueDate field.</param>
    /// <param name="pageSize">The PageSize field.</param>
    /// <param name="pagingState">The PagingState field.</param>
    [SerializationConstructor]
    public GetPositionExitWorkflowTimelineQuery(ActorSubject subject, IActorEntityId entityId, StrategyPositionId positionId, DateOnly valueDate, int pageSize, byte[]? pagingState)
    {
        Subject = subject;
        EntityId = entityId;
        PositionId = positionId;
        ValueDate = valueDate;
        PageSize = pageSize;
        PagingState = pagingState;
    }
    [IgnoreMember] public const string Actor = GetPositionExitWorkflowQuery.Actor;
    [IgnoreMember] public const string Verb = "GetPositionExitWorkflowTimeline";
    [IgnoreMember] public const int ErrorId = 27232;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public StrategyPositionId PositionId { get; init; }
    [Key(3)] public DateOnly ValueDate { get; init; }
    [Key(4)] public int PageSize { get; init; } = 100;
    [Key(5)] public byte[]? PagingState { get; init; }
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => null;
}
