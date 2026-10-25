using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Broker;

[MessagePackObject]
public sealed record BrokerOrderDefinition
{
    [Key(0)] public int SchemaVersion { get; init; } = 2;
    [Key(1)] public BrokerOrderId Id { get; init; }
    [Key(2)] public TradeOrderDefinition Order { get; init; } = new();
    [Key(3)] public Guid OperationId { get; init; }
    [Key(4)] public BrokerOrderStatus Status { get; init; }
    [Key(5)] public int Revision { get; init; }
    [Key(6)] public string DispatchCategory { get; init; } = string.Empty;
    [Key(7)] public string DispatchDetail { get; init; } = string.Empty;
    [Key(8)] public DateTime ChangedAtUtc { get; init; }
    [Key(9)] public BrokerOrderObservationEvidence? LastObservation { get; init; }
    [Key(10)] public decimal CurrentSignedNetDebitLimit { get; init; }
    [Key(11)] public BrokerMutationKind PendingMutation { get; init; }
    [Key(12)] public BrokerOrderStatus StatusBeforeMutation { get; init; }
    [Key(13)] public int BrokerRevision { get; init; }
    [Key(14)] public Guid PriorOperationId { get; init; }
}
