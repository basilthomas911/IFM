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
public sealed record CompositionLeg
{
    [Key(0)] public string InstrumentId { get; init; } = "";
    [Key(1)] public string RawSymbol { get; init; } = "";
    [Key(2)] public string UnderlyingInstrumentId { get; init; } = "";
    [Key(3)] public string InstrumentClass { get; init; } = "";
    [Key(4)] public string Side { get; init; } = "";
    [Key(5)] public int Ratio { get; init; } = 1;
    [Key(6)] public bool? Right { get; init; }
    [Key(7)] public decimal? Strike { get; init; }
    [Key(8)] public DateTime ExpirationUtc { get; init; }
    [Key(9)] public decimal Multiplier { get; init; }
    [Key(10)] public string TickRuleId { get; init; } = "";
    [Key(11)] public OptionPricingQuote Quote { get; init; } = default!;
    [Key(12)] public CompositionValuation? Valuation { get; init; }
    [Key(13)] public string DefinitionHash { get; init; } = "";
}
