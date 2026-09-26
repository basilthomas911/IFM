using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Position;

[MessagePackObject(AllowPrivate = true)]
public sealed record GetFuturesTradePositionQuery : IQuery<StrategyPositionSnapshot>
{

    /// <summary>Creates an empty query for serialization and existing callers.</summary>
    public GetFuturesTradePositionQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="positionId">The PositionId field.</param>
    [SerializationConstructor]
    public GetFuturesTradePositionQuery(ActorSubject subject, IActorEntityId entityId, StrategyPositionId positionId)
    {
        Subject = subject;
        EntityId = entityId;
        PositionId = positionId;
    }
    public const string Actor = FuturesPositionActorNames.Query;
    public const string Verb = "GetFuturesTradePosition";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public StrategyPositionId PositionId { get; init; }
    [IgnoreMember] public int ErrorCode => 25211;
    [IgnoreMember] public string? QueryParams => null;
}
