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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(8)] public FinancialExecutionLeg[] Legs { get; init; } = [];
    [Key(9)] public string Currency { get; init; } = "USD";
    [Key(10)] public string Environment { get; init; } = "Emulator";
    [Key(11)] public decimal SignedDebitPerUnit { get; init; }
    [Key(12)] public decimal EntryFees { get; init; }
    [Key(13)] public DateTime ValidUntilUtc { get; init; }
    [Key(14)] public string ContentHash { get; init; } = "";
    public string Hash() => FinancialCanonicalHash.Compute(this with { ContentHash = "" });
}
