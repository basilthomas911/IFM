using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using NewTradeOrderId = TomasAI.IFM.Domain.Trade.Shared.TradeOrderId;

namespace TomasAI.IFM.Domain.Trade.Shared.Order;

[MessagePackObject(AllowPrivate = true)]
public sealed record GetTradeOrderQuery : IQuery<TradeOrderDefinition>
{

    /// <summary>Creates an empty query for serialization and existing callers.</summary>
    public GetTradeOrderQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="tradeOrderId">The TradeOrderId field.</param>
    [SerializationConstructor]
    public GetTradeOrderQuery(ActorSubject subject, IActorEntityId entityId, NewTradeOrderId tradeOrderId)
    {
        Subject = subject;
        EntityId = entityId;
        TradeOrderId = tradeOrderId;
    }
    public const string Actor = TradeOrderActorNames.Query;
    public const string Verb = "GetTradeOrder";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public NewTradeOrderId TradeOrderId { get; init; }
    [IgnoreMember] public int ErrorCode => 25209;
    [IgnoreMember] public string? QueryParams => null;
}
