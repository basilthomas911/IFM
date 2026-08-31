using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

[MessagePackObject(AllowPrivate = true)]
public sealed record FundOrderProjectionReadModel
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int FundId { get; init; }
    [Key(2)] public int OrderId { get; init; }
    [Key(3)] public Guid WorkflowId { get; init; }
    [Key(4)] public string Status { get; init; } = string.Empty;
    [Key(5)] public DateTime CreatedOnUtc { get; init; }
    [Key(6)] public string CreatedBy { get; init; } = string.Empty;
    [Key(7)] public Guid CompositionResultId { get; init; }
    [Key(8)] public string CompositionResultHash { get; init; } = string.Empty;
    [Key(9)] public long AggregateVersion { get; init; }
    [Key(10)] public long WorkflowRevision { get; init; }
    [Key(11)] public Guid TradeSelectionResultId { get; init; }
    [Key(12)] public string TradeSelectionResultHash { get; init; } = string.Empty;
    [Key(13)] public Guid TradeTemplateId { get; init; }
    [Key(14)] public long TradeTemplateVersion { get; init; }
    [Key(15)] public Guid OrderCompositionProfileId { get; init; }
    [Key(16)] public long OrderCompositionProfileVersion { get; init; }
    [Key(17)] public string StrategySnapshotHash { get; init; } = string.Empty;
    [Key(18)] public DateTime ExpiresAtUtc { get; init; }
    [Key(19)] public Guid RiskResultId { get; init; }
    [Key(20)] public string RiskResultHash { get; init; } = string.Empty;
    [Key(21)] public string StopReason { get; init; } = string.Empty;
    [Key(22)] public Guid IdempotencyKey { get; init; }
    [Key(23)] public string CanonicalRequestHash { get; init; } = string.Empty;
    [Key(24)] public CompositionOrigin Origin { get; init; }
    [Key(25)] public string OperatorReference { get; init; } = string.Empty;
}

[MessagePackObject(AllowPrivate = true)]
public sealed record FundOrderTradeProjectionReadModel
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int FundId { get; init; }
    [Key(2)] public int OrderId { get; init; }
    [Key(3)] public int TradeId { get; init; }
    [Key(4)] public string TradeFamily { get; init; } = string.Empty;
    [Key(5)] public string InstructionReference { get; init; } = string.Empty;
    [Key(6)] public int LegOrdinal { get; init; }
    [Key(7)] public long AggregateVersion { get; init; }
    [Key(8)] public string DirectionOrBias { get; init; } = string.Empty;
    [Key(9)] public string TradeAction { get; init; } = string.Empty;
    [Key(10)] public string UnderlyingRoot { get; init; } = string.Empty;
    [Key(11)] public DateOnly RequestedTradeDate { get; init; }
    [Key(12)] public DateOnly? RequestedMaturityDate { get; init; }
}

[MessagePackObject(AllowPrivate = true)]
public sealed record FundCompositionWorkflowProjectionReadModel
{
    [Key(0)] public Guid WorkflowId { get; init; }
    [Key(1)] public int PortfolioId { get; init; }
    [Key(2)] public int FundId { get; init; }
    [Key(3)] public int OrderId { get; init; }
    [Key(4)] public Guid CompositionResultId { get; init; }
    [Key(5)] public string CompositionResultHash { get; init; } = string.Empty;
    [Key(6)] public string Status { get; init; } = string.Empty;
    [Key(7)] public DateTime UpdatedOnUtc { get; init; }
    [Key(8)] public long AggregateVersion { get; init; }
}
