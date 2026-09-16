using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Broker;

/// <summary>Reads the current durable state of broker orders.</summary>
public interface IBrokerOrderQueryApi
{
    /// <summary>Gets one broker order by its full durable identity.</summary>
    ValueTask<ServiceResult<BrokerOrderDefinition>> GetAsync(
        BrokerOrderId brokerOrderId,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the current logical broker orders for one accepted Trade Order.</summary>
    ValueTask<ServiceResult<BrokerOrderDefinition[]>> ListAsync(
        TradeOrderId tradeOrderId,
        CancellationToken cancellationToken = default);
}

/// <summary>Reads the latest projected state for one logical broker order.</summary>
[MessagePackObject]
public sealed record GetBrokerOrderQuery : IQuery<BrokerOrderDefinition>
{
    public const string Actor = BrokerOrderActorNames.Query;
    public const string Verb = "GetBrokerOrder";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public BrokerOrderId BrokerOrderId { get; init; }
    [IgnoreMember] public int ErrorCode => 25211;
    [IgnoreMember] public string? QueryParams => null;
}

/// <summary>Reads every latest logical broker-order projection for one Trade Order.</summary>
[MessagePackObject]
public sealed record GetBrokerOrdersForTradeOrderQuery : IQuery<BrokerOrderDefinition[]>
{
    public const string Actor = BrokerOrderActorNames.Query;
    public const string Verb = "GetBrokerOrdersForTradeOrder";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public TradeOrderId TradeOrderId { get; init; }
    [IgnoreMember] public int ErrorCode => 25211;
    [IgnoreMember] public string? QueryParams => null;
}
