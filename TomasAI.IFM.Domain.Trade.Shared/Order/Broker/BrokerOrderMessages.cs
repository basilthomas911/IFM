using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Broker;

public static class BrokerOrderActorNames
{
    public const string Command = "BrokerOrderCommand";
    public const string Event = "BrokerOrderEvent";
    public const string Query = "BrokerOrderQuery";
}

public enum BrokerOrderStatus : byte
{
    Unknown = 0, PlacePending = 1, Dispatched = 2, Working = 3, PartiallyFilled = 4,
    Filled = 5, CancelPending = 6, Cancelled = 7, Rejected = 8, OutcomeUnknown = 9,
    UpdatePending = 10
}

public enum BrokerMutationKind : byte { Unknown = 0, Place = 1, UpdateLimit = 2, Cancel = 3 }

public enum BrokerDispatchResult : byte { RejectedLocally = 0, AcceptedForDispatch = 1, OutcomeUnknown = 2 }

public enum BrokerOrderObservationKind : byte
{
    Unknown = 0, Acknowledged = 1, Execution = 2, Commission = 3, Rejected = 4,
    Cancelled = 5, OrderCompleted = 6
}

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
