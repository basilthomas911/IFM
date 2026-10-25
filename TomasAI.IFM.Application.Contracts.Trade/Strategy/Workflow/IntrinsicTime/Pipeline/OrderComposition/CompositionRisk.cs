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
public sealed record CompositionRisk
{
    [Key(0)] public string RiskBound { get; init; } = "";
    [Key(1)] public decimal? MaximumLoss { get; init; }
    [Key(2)] public decimal? MaximumProfit { get; init; }
    [Key(3)] public decimal? PayoffRewardToRisk { get; init; }
    [Key(4)] public decimal? Notional { get; init; }
    [Key(5)] public decimal? PlannedLoss { get; init; }
    [Key(6)] public decimal? StressLoss { get; init; }
}
