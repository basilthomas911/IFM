using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

[MessagePackObject]
public sealed record PortfolioOrderCandidate
{
    [Key(0)] public Guid CompositionId { get; init; }
    [Key(1)] public Guid WorkflowId { get; init; }
    [Key(2)] public string DecisionHorizon { get; init; } = string.Empty;
    [Key(3)] public PortfolioExecutionStrategyKind StrategyKind { get; init; }
    [Key(4)] public DateOnly ValueDate { get; init; }
    [Key(5)] public DateTime ValidUntilUtc { get; init; }
    [Key(6)] public string Origin { get; init; } = string.Empty;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(7)] public PortfolioExecutionComponent[] Components { get; init; } = [];
    [Key(8)] public decimal RequiredCapital { get; init; }
    [Key(9)] public string EvidenceHash { get; init; } = string.Empty;
    [Key(10)] public CatalogKey DeploymentKey { get; init; }
    [Key(11)] public decimal MaximumLoss { get; init; }
    [Key(12)] public decimal StressLoss { get; init; }
    [Key(13)] public decimal Notional { get; init; }
    [Key(14)] public string ProductSymbol { get; init; } = string.Empty;
    [Key(15)] public string ProductExchange { get; init; } = string.Empty;
    [Key(16)] public string ProductCurrency { get; init; } = string.Empty;
    [Key(17)] public decimal Delta { get; init; }
    [Key(18)] public decimal Gamma { get; init; }
    [Key(19)] public decimal Vega { get; init; }
    [Key(20)] public PortfolioExecutionPositionType PositionType { get; init; }
    [Key(21)] public string BrokerAccountAlias { get; init; } = string.Empty;
    [Key(22)] public PortfolioBrokerEnvironment BrokerEnvironment { get; init; }
    [Key(23)] public Guid PortfolioApprovalId { get; init; }
    [Key(24)] public string MicroExecutionProfileId { get; init; } = string.Empty;
    [Key(25)] public int MicroExecutionProfileVersion { get; init; }
    [Key(26)] public string MicroExecutionProfileHash { get; init; } = string.Empty;
    [Key(27)] public string AccountPromotionApprovalReference { get; init; } = string.Empty;
    [Key(28)] public PortfolioBrokerOrderType BrokerOrderType { get; init; } = PortfolioBrokerOrderType.Limit;
    [Key(29)] public PortfolioBrokerAlgorithm BrokerAlgorithm { get; init; } = PortfolioBrokerAlgorithm.None;
    [Key(30)] public VolatilityWorkflowInput? VolatilityEvidence { get; init; }
    [Key(31)] public string TimeInForce { get => timeInForce; init => timeInForce = string.IsNullOrEmpty(value) ? "Day" : value; }
    private string timeInForce = "Day";
    [Key(32)] public string AlgorithmPace { get => algorithmPace; init => algorithmPace = string.IsNullOrEmpty(value) ? "Normal" : value; }
    private string algorithmPace = "Normal";
    /// <summary>Exact immutable market inputs accepted for this financial decision.</summary>
    [Key(33)] [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketDecisionEvidence? DecisionEvidence { get; init; }
    /// <summary>Manual setup identity propagated into backend execution.</summary>
    [Key(34)] [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TomasAI.IFM.Domain.Portfolio.Shared.Contracts.FundTradeSetupReference? SetupTrade { get; init; }
}
