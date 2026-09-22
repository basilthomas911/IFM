using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

public enum PortfolioCloseOrderCompositionStatus : byte
{
    ExecuteTradeOrder = 1,
    NoTradeOrder = 2
}

[MessagePackObject]
public sealed record PortfolioCloseOrderCompositionReceipt
{
    [Key(0)] public Guid CompositionId { get; init; }
    [Key(1)] public PortfolioExitWorkflowId WorkflowId { get; init; }
    [Key(2)] public PortfolioCloseOrderCompositionStatus Status { get; init; }
    [Key(3)] public PortfolioExecutionOrderInstruction? TradeOrder { get; init; }
    [Key(4)] public long FinancialRevision { get; init; }
    [Key(5)] public int PortfolioId { get; init; }
    [Key(6)] public string ReasonCode { get; init; } = string.Empty;
}
