using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

public enum PortfolioRiskDecisionStatus : byte { ExecuteTradeOrders = 1, NoTradeOrders = 2 }

/// <summary>Trade-owned durable copy of the atomic Portfolio order-composition receipt.</summary>
[MessagePackObject]
public sealed record PortfolioRiskDecision
{
    [Key(0)] public Guid CompositionId { get; init; }
    [Key(1)] public Guid WorkflowId { get; init; }
    [Key(2)] public PortfolioRiskDecisionStatus Status { get; init; }
    [Key(3)] public TradeOrderDefinition[] TradeOrders { get; init; } = [];
    [Key(4)] public long FinancialRevision { get; init; }
    [Key(5)] public int AcceptedFundCount { get; init; }
    [Key(6)] public int RejectedFundCount { get; init; }
    [Key(7)] public int PortfolioId { get; init; }
    [Key(8)] public VolatilityWorkflowInput? VolatilityEvidence { get; init; }
}
