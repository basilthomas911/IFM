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
public sealed record CompositionValuation
{
    [Key(0)] public decimal ImpliedVolatility { get; init; }
    [Key(1)] public decimal Delta { get; init; }
    [Key(2)] public decimal Gamma { get; init; }
    [Key(3)] public decimal Theta { get; init; }
    [Key(4)] public decimal Vega { get; init; }
    [Key(5)] public decimal Rho { get; init; }
    [Key(6)] public decimal TheoreticalPrice { get; init; }
    [Key(7)] public decimal TimeToExpiry { get; init; }
    [Key(8)] public string ContextDigest { get; init; } = "";
}
