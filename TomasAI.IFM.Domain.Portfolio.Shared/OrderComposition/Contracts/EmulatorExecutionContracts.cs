using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Exact executable order frozen by the workflow from its accepted composition and sized Risk result.</summary>
[MessagePackObject]
public sealed record FinancialExecutionOrder
{
    [Key(0)] public Guid ExecutionId { get; init; }
    [Key(1)] public int PortfolioId { get; init; }
    [Key(2)] public int FundId { get; init; }
    [Key(3)] public int BookId { get; init; }
    [Key(4)] public int OrderId { get; init; }
    [Key(5)] public Guid ReservationId { get; init; }
    [Key(6)] public string SizedOrderHash { get; init; } = "";
    [Key(7)] public int StrategyUnits { get; init; }
    [Key(8)] public FinancialExecutionLeg[] Legs { get; init; } = [];
    [Key(9)] public string Currency { get; init; } = "USD";
    [Key(10)] public string Environment { get; init; } = "Emulator";
    [Key(11)] public decimal SignedDebitPerUnit { get; init; }
    [Key(12)] public decimal EntryFees { get; init; }
    [Key(13)] public DateTime ValidUntilUtc { get; init; }
    [Key(14)] public string ContentHash { get; init; } = "";
    public string Hash()=>FinancialCanonicalHash.Compute(this with { ContentHash="" });
}

[MessagePackObject]
public sealed record FinancialExecutionLeg([property:Key(0)] int TradeId,[property:Key(1)] string InstrumentId,
    [property:Key(2)] string RawSymbol,[property:Key(3)] string InstrumentClass,[property:Key(4)] string Side,
    [property:Key(5)] int Contracts,[property:Key(6)] decimal Multiplier);

[MessagePackObject]
public sealed record SubmitEmulatorOrderRequest([property:Key(0)] FinancialExecutionOrder Order,
    [property:Key(1)] Guid ConsumptionOperationId,[property:Key(2)] Guid ConsumptionCompletedEventId,
    [property:Key(3)] string ConsumptionInputHash);

[MessagePackObject]
public sealed record EmulatorOrderReceipt([property:Key(0)] Guid ExecutionId,[property:Key(1)] FinancialExecutionOrder Order,
    [property:Key(2)] string BrokerOrderReference,[property:Key(3)] long FinancialRevision,[property:Key(4)] DateTime SubmittedAtUtc,
    [property:Key(5)] Guid CompletedEventId);
