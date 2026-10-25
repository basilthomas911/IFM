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
public sealed record CompositionExecutionEnvelope
{
    [Key(0)] public short SchemaVersion { get; init; } = 1;
    [Key(1)] public string OrderType { get; init; } = "Limit";
    [Key(2)] public string TimeInForce { get; init; } = "Day";
    [Key(3)] public bool Atomic { get; init; }
    [Key(4)] public decimal ProposedSignedDebit { get; init; }
    [Key(5)] public decimal WorstSignedDebit { get; init; }
    [Key(6)] public decimal Tick { get; init; }
    [Key(7)] public string TickRuleVersion { get; init; } = "";
    [Key(8)] public DateTime ValidUntilUtc { get; init; }
    [Key(9)] public bool AllowLegging { get; init; }
    [Key(10)] public bool AllowMarketEscalation { get; init; }
}
