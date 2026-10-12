using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures;

[MessagePackObject(AllowPrivate = true)]
public sealed record GetFuturesTradeQuery : IQuery<EstablishedTradeDefinition>
{

    /// <summary>Creates an empty query for serialization and existing callers.</summary>
    public GetFuturesTradeQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="tradeId">The TradeId field.</param>
    [SerializationConstructor]
    public GetFuturesTradeQuery(ActorSubject subject, IActorEntityId entityId, TradeEntityId tradeId)
    {
        Subject = subject;
        EntityId = entityId;
        TradeId = tradeId;
    }
    public const string Actor = FuturesTradeActorNames.Query;
    public const string Verb = "GetFuturesTrade";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public TradeEntityId TradeId { get; init; }
    [IgnoreMember] public int ErrorCode => 25203;
    [IgnoreMember] public string? QueryParams => null;
}
