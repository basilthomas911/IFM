using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Broker;

[MessagePackObject]
public sealed record BrokerOrderObservationEvidence
{
    [Key(0)] public Guid ObservationId { get; init; }
    [Key(1)] public BrokerOrderObservationKind Kind { get; init; }
    [Key(2)] public string AccountAlias { get; init; } = string.Empty;
    [Key(3)] public Guid OperationId { get; init; }
    [Key(4)] public Guid ComponentId { get; init; }
    [Key(5)] public Guid LegId { get; init; }
    [Key(6)] public string ContractId { get; init; } = string.Empty;
    [Key(7)] public string ExternalExecutionId { get; init; } = string.Empty;
    [Key(8)] public int SignedQuantity { get; init; }
    [Key(9)] public decimal Price { get; init; }
    [Key(10)] public decimal Commission { get; init; }
    [Key(11)] public int OrderRevision { get; init; }
    [Key(12)] public long SourceEpoch { get; init; }
    [Key(13)] public long SourceSequence { get; init; }
    [Key(14)] public DateTime OccurredAtUtc { get; init; }
    [Key(15)] public string Category { get; init; } = string.Empty;
    [Key(16)] public string Detail { get; init; } = string.Empty;
    [Key(17)] public string ContentHash { get; init; } = string.Empty;
}
