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
public sealed record CompositionDecisionContext
{
    [Key(0)] public Guid SelectionResultId { get; init; }
    [Key(1)] public string SelectionResultHash { get; init; } = "";
    [Key(2)] public string InputHash { get; init; } = "";
    [Key(3)] public string BindingHash { get; init; } = "";
    [Key(4)] public string SnapshotHash { get; init; } = "";
    [Key(5)] public DateOnly ValueDate { get; init; }
    [Key(6)] public string PricerVersion { get; init; } = "";
    [Key(7)] public string AlgorithmVersion { get; init; } = "";
    [Key(8)] public int PortfolioId { get; init; }
    [Key(9)] public int FundId { get; init; }
    [Key(10)] public VolatilityWorkflowInput? VolatilityEvidence { get; init; }
}
