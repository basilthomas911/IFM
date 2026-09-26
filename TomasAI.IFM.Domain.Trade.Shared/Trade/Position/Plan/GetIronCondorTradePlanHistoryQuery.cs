using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

/// <summary>Reads material Iron Condor Trade Plan history.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetIronCondorTradePlanHistoryQuery : IQuery<StrategyTradePlanHistoryPage>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetIronCondorTradePlanHistoryQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="planId">The PlanId field.</param>
    /// <param name="pageSize">The PageSize field.</param>
    /// <param name="pagingState">The PagingState field.</param>
    [SerializationConstructor]
    public GetIronCondorTradePlanHistoryQuery(ActorSubject subject, IActorEntityId entityId, IronCondorTradePlanId planId, int pageSize, byte[]? pagingState)
    {
        Subject = subject;
        EntityId = entityId;
        PlanId = planId;
        PageSize = pageSize;
        PagingState = pagingState;
    }
    [IgnoreMember] public const string Actor = GetCurrentIronCondorTradePlanQuery.Actor;
    [IgnoreMember] public const string Verb = "GetIronCondorTradePlanHistory";
    [IgnoreMember] public const int ErrorId = 27122;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public IronCondorTradePlanId PlanId { get; init; }
    [Key(3)] public int PageSize { get; init; } = 100;
    [Key(4)] public byte[]? PagingState { get; init; }
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => null;
}
