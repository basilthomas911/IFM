using MessagePack;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

/// <summary>Stable setup identity carried by backend execution; null for orders without a manual setup.</summary>
[MessagePackObject]
public sealed record FundTradeSetupReference
{
    [Key(0)] public int OrderId { get; init; }
    [Key(1)] public int TradeId { get; init; }
    [Key(2)] public int FundId { get; init; }
}

/// <summary>Execution evidence for a concrete Fund lifecycle operation, never an arbitrary target state.</summary>
[MessagePackObject]
public sealed record FundTradeExecutionEvidence
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int FundId { get; init; }
    [Key(2)] public FundTradeSetupReference SetupTrade { get; init; } = new();
    [Key(3)] public int ExecutionOrderId { get; init; }
    [Key(4)] public int ExecutionTradeId { get; init; }
    [Key(5)] public Guid ExecutionAttemptId { get; init; }
    [Key(6)] public DateTime OccurredAtUtc { get; init; }
    [Key(7)] public DateOnly? TradeDate { get; init; }
    [Key(8)] public DateOnly? MaturityDate { get; init; }
    [Key(9)] public bool ClosingExecution { get; init; }
    [Key(10)] public int? OpeningExecutionTradeId { get; init; }
    /// <summary>True only when confirmed fills reverse every remaining leg; partial closes keep the setup active.</summary>
    [Key(11)] public bool FullyClosed { get; init; }
}
