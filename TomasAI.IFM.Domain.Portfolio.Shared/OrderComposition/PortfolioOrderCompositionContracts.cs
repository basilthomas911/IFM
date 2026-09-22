using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

public enum PortfolioOrderCompositionStatus : byte { ExecuteTradeOrders = 1, NoTradeOrders = 2 }

[MessagePackObject]
public sealed record PortfolioFundOrderDecision(
    [property: Key(0)] int FundId,
    [property: Key(1)] bool Accepted,
    [property: Key(2)] string ReasonCode,
    [property: Key(3)] int? OrderId);

[MessagePackObject]
public sealed record PortfolioAcceptedCapacityEffect
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int FundId { get; init; }
    [Key(2)] public int OrderId { get; init; }
    [Key(3)] public string UnderlyingScopeKey { get; init; } = string.Empty;
    [Key(4)] public decimal RequiredCash { get; init; }
    [Key(5)] public CapacityExposure[] Exposures { get; init; } = [];
}

[MessagePackObject]
public sealed record PortfolioFundFinancialSnapshot(
    [property: Key(0)] int FundId,
    [property: Key(1)] decimal AvailableCash,
    [property: Key(2)] CapacityUsed[] Usage);

[MessagePackObject]
public sealed record PortfolioOrderCompositionReceipt
{
    [Key(0)] public Guid CompositionId { get; init; }
    [Key(1)] public Guid WorkflowId { get; init; }
    [Key(2)] public PortfolioOrderCompositionStatus Status { get; init; }
    [Key(3)] public PortfolioFundOrderDecision[] FundDecisions { get; init; } = [];
    [Key(4)] public PortfolioExecutionOrderInstruction[] TradeOrders { get; init; } = [];
    [Key(5)] public long FinancialRevision { get; init; }
    [Key(6)] public int PortfolioId { get; init; }
    [Key(7)] public PortfolioAcceptedCapacityEffect[] CapacityEffects { get; init; } = [];
    [Key(8)] public VolatilityWorkflowInput? VolatilityEvidence { get; init; }
}
