using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;

[MessagePackObject]
public sealed record CompositionPrices
{
    [Key(0)] public decimal NaturalDebit { get; init; }
    [Key(1)] public decimal MidDebit { get; init; }
    [Key(2)] public decimal BestDebit { get; init; }
    [Key(3)] public decimal LimitDebit { get; init; }
    [Key(4)] public decimal WorstDebit { get; init; }
    [Key(5)] public decimal ComboTick { get; init; }
    [Key(6)] public decimal ComboSpread { get; init; }
    [Key(7)] public decimal CostReserve { get; init; }
}
