namespace TomasAI.IFM.Application.TradeBroker.Contracts;

/// <summary>Normalized immutable authoritative callback fact.</summary>
public sealed record BrokerObservation
{
    public required BrokerObservationKind Kind { get; init; }
    public required Guid ObservationId { get; init; }
    public required string AccountAlias { get; init; }
    public required string BrokerOrderId { get; init; }
    public Guid OperationId { get; init; }
    public Guid ComponentId { get; init; }
    public Guid LegId { get; init; }
    public string ContractId { get; init; } = string.Empty;
    public string ExternalExecutionId { get; init; } = string.Empty;
    public int SignedQuantity { get; init; }
    public decimal Price { get; init; }
    public decimal Commission { get; init; }
    public int OrderRevision { get; init; }
    public long SourceEpoch { get; init; }
    public long SourceSequence { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
}
