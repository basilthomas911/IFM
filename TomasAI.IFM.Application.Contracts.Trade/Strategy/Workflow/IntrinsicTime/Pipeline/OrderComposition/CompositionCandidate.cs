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
public sealed record CompositionCandidate
{
    [Key(0)] public short SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CandidateId { get; init; }
    [Key(2)] public long OrderId { get; init; }
    [Key(3)] public long PrimaryTradeId { get; init; }
    [Key(4)] public int PortfolioId { get; init; }
    [Key(5)] public int FundId { get; init; }
    [Key(6)] public long AssignmentVersion { get; init; }
    [Key(7)] public CatalogKey DeploymentKey { get; init; } = default!;
    [Key(8)] public CatalogKey StrategyKey { get; init; } = default!;
    [Key(9)] public CatalogKey StructureKey { get; init; } = default!;
    [Key(10)] public CatalogKey VariantKey { get; init; } = default!;
    [Key(11)] public SelectionProduct Product { get; init; } = new();
    [Key(12)] public TimeFrameType TargetHorizon { get; init; }
    [Key(13)] public string Side { get; init; } = "";
    [Key(14)] public string Bias { get; init; } = "";
    [Key(15)] public string PremiumMode { get; init; } = "";
    [Key(16)] public ImmutableArray<CompositionLeg> Legs { get; init; } = [];
    [Key(17)] public int UnitQuantity { get; init; } = 1;
    [Key(18)] public int LiquidityCapacityUnits { get; init; }
    [Key(19)] public CompositionPrices Pricing { get; init; } = new();
    [Key(20)] public CompositionGreeks Greeks { get; init; } = new();
    [Key(21)] public CompositionRisk RiskEvidence { get; init; } = new();
    [Key(22)] public CompositionExecutionEnvelope ExecutionEnvelope { get; init; } = new();
    [Key(23)] public string ParameterResolutionHash { get; init; } = "";
    [Key(24)] public string SnapshotHash { get; init; } = "";
    [Key(25)] public string BindingHash { get; init; } = "";
    [Key(26)] public string PricerVersion { get; init; } = "";
    [Key(27)] public DateTime EvaluatedAtUtc { get; init; }
    [Key(28)] public DateTime ValidUntilUtc { get; init; }
    [Key(29)] public string ApprovalState { get; init; } = "Unapproved";
    [Key(30)] public string CandidateHash { get; init; } = "";
    [Key(31)] public VolatilityWorkflowInput? VolatilityEvidence { get; init; }
    /// <summary>Exact immutable market inputs accepted for this financial decision.</summary>
    [Key(32)] [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketDecisionEvidence? DecisionEvidence { get; init; }
}
