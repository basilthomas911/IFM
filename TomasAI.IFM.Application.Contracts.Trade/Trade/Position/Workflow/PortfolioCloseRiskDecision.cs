using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

[MessagePackObject]
public sealed record PortfolioCloseRiskDecision
{
    [Key(0)] public Guid PortfolioCompletedEventId { get; init; }
    [Key(1)] public TradeOrderDefinition? TradeOrder { get; init; }
    [Key(2)] public bool ExecuteTradeOrder { get; init; }
    [Key(3)] public string ReasonCode { get; init; } = string.Empty;
    [Key(4)] public long FinancialRevision { get; init; }
}
