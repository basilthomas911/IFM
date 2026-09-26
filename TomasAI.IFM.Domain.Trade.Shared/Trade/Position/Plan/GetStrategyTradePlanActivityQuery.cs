using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

/// <summary>Lists material Trade Plan activity for one bounded value-date partition.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetStrategyTradePlanActivityQuery : IQuery<StrategyTradePlanActivityPage>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetStrategyTradePlanActivityQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="valueDate">The ValueDate field.</param>
    /// <param name="pageSize">The PageSize field.</param>
    /// <param name="pagingState">The PagingState field.</param>
    [SerializationConstructor]
    public GetStrategyTradePlanActivityQuery(ActorSubject subject, IActorEntityId entityId, DateOnly valueDate, int pageSize, byte[]? pagingState)
    {
        Subject = subject;
        EntityId = entityId;
        ValueDate = valueDate;
        PageSize = pageSize;
        PagingState = pagingState;
    }
    [IgnoreMember] public const string Actor = "StrategyTradePlanActivityQuery";
    [IgnoreMember] public const string Verb = "GetStrategyTradePlanActivity";
    [IgnoreMember] public const int ErrorId = 27127;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public DateOnly ValueDate { get; init; }
    [Key(3)] public int PageSize { get; init; } = 100;
    [Key(4)] public byte[]? PagingState { get; init; }
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => null;
}
