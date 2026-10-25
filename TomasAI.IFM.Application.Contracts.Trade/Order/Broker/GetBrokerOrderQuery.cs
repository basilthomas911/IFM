using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Broker;

/// <summary>Reads the latest projected state for one logical broker order.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetBrokerOrderQuery : IQuery<BrokerOrderDefinition>
{

    /// <summary>Creates an empty query for serialization and existing callers.</summary>
    public GetBrokerOrderQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="brokerOrderId">The BrokerOrderId field.</param>
    [SerializationConstructor]
    public GetBrokerOrderQuery(ActorSubject subject, IActorEntityId entityId, BrokerOrderId brokerOrderId)
    {
        Subject = subject;
        EntityId = entityId;
        BrokerOrderId = brokerOrderId;
    }
    public const string Actor = BrokerOrderActorNames.Query;
    public const string Verb = "GetBrokerOrder";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public BrokerOrderId BrokerOrderId { get; init; }
    [IgnoreMember] public int ErrorCode => 25211;
    [IgnoreMember] public string? QueryParams => null;
}
