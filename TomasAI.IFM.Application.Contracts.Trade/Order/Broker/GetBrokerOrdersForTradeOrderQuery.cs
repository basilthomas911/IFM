using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Broker;

/// <summary>Reads every latest logical broker-order projection for one Trade Order.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetBrokerOrdersForTradeOrderQuery : IQuery<BrokerOrderDefinition[]>
{

    /// <summary>Creates an empty query for serialization and existing callers.</summary>
    public GetBrokerOrdersForTradeOrderQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="tradeOrderId">The TradeOrderId field.</param>
    [SerializationConstructor]
    public GetBrokerOrdersForTradeOrderQuery(ActorSubject subject, IActorEntityId entityId, TradeOrderId tradeOrderId)
    {
        Subject = subject;
        EntityId = entityId;
        TradeOrderId = tradeOrderId;
    }
    public const string Actor = BrokerOrderActorNames.Query;
    public const string Verb = "GetBrokerOrdersForTradeOrder";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public TradeOrderId TradeOrderId { get; init; }
    [IgnoreMember] public int ErrorCode => 25211;
    [IgnoreMember] public string? QueryParams => null;
}
