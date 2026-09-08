using MessagePack;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;

public enum CompositionOutcome : byte { Undefined=0, Composed=1, NoCandidate=2 }

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
}

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

[MessagePackObject]
public sealed record CompositionGreeks
{
    [Key(0)] public decimal Delta { get; init; }
    [Key(1)] public decimal Gamma { get; init; }
    [Key(2)] public decimal Theta { get; init; }
    [Key(3)] public decimal Vega { get; init; }
    [Key(4)] public decimal Rho { get; init; }
    [Key(5)] public string DeltaUnits { get; init; } = "UnderlyingEquivalent";
    [Key(6)] public string VegaUnits { get; init; } = "PerAnnualDecimalVolatility";
    [Key(7)] public string ThetaUnits { get; init; } = "PerYear";
}

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
}

[MessagePackObject]
public sealed record CompositionCounts
{
    [Key(0)] public int Generated { get; init; }
    [Key(1)] public int Eligible { get; init; }
    [Key(2)] public int Rejected { get; init; }
}

[MessagePackObject]
public sealed record CompositionRejection
{
    [Key(0)] public string ReasonCode { get; init; } = "";
    [Key(1)] public int Count { get; init; }
}

[MessagePackObject]
public sealed record CompositionRanking
{
    [Key(0)] public decimal DteDistance { get; init; }
    [Key(1)] public decimal DeltaDistance { get; init; }
    [Key(2)] public decimal LegDeltaDistance { get; init; }
    [Key(3)] public decimal SpreadTicks { get; init; }
    [Key(4)] public decimal RewardToRisk { get; init; }
    [Key(5)] public string CanonicalKey { get; init; } = "";
}

[MessagePackObject]
public sealed record OrderCompositionResult
{
    [Key(0)] public short SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid ResultId { get; init; }
    [Key(2)] public StrategyWorkflowId WorkflowId { get; init; }
    [Key(3)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    [Key(4)] public Guid InvocationId { get; init; }
    [Key(5)] public long InputWorkflowRevision { get; init; }
    [Key(6)] public string InputSha256 { get; init; } = "";
    [Key(7)] public DateTime EvaluatedAtUtc { get; init; }
    [Key(8)] public DateTime ProducedAtUtc { get; init; }
    [Key(9)] public TimeFrameType TargetHorizon { get; init; }
    [Key(10)] public CompositionOutcome Outcome { get; init; }
    [Key(11)] public CompositionCandidate? Candidate { get; init; }
    [Key(12)] public CompositionDecisionContext DecisionContext { get; init; } = new();
    [Key(13)] public CompositionResolvedParameters ResolvedParameters { get; init; } = new();
    [Key(14)] public CompositionCounts CandidateCounts { get; init; } = new();
    [Key(15)] public ImmutableArray<CompositionRejection> CandidateDiagnostics { get; init; } = [];
    [Key(16)] public ImmutableArray<string> Reasons { get; init; } = [];
    [Key(17)] public DateTime? ValidUntilUtc { get; init; }
    [Key(18)] public string SummaryText { get; init; } = "";
    [Key(19)] public CompositionRanking? Ranking { get; init; }
}
