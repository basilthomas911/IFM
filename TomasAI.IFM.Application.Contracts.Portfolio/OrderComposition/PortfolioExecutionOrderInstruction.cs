using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

/// <summary>Portfolio-owned, broker-neutral instruction accepted by the Trade boundary.</summary>
[MessagePackObject]
public sealed record PortfolioExecutionOrderInstruction
{
    [Key(0)] public ushort SchemaVersion { get; init; } = 1;
    [Key(1)] public PortfolioExecutionOrderId Id { get; init; }
    [Key(2)] public int Revision { get; init; }
    [Key(3)] public PortfolioExecutionOrderStatus Status { get; init; }
    [Key(4)] public DateOnly ValueDate { get; init; }
    [Key(5)] public DateTime ValidUntilUtc { get; init; }
    [Key(6)] public string Origin { get; init; } = string.Empty;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(7)] public PortfolioExecutionComponent[] Components { get; init; } = [];
    [Key(8)] public string DefinitionHash { get; init; } = string.Empty;
    [Key(9)] public PortfolioExecutionPositionType PositionType { get; init; }
    [Key(10)] public PortfolioPositionReference? TargetPosition { get; init; }
    [Key(11)] public string BrokerAccountAlias { get; init; } = string.Empty;
    [Key(12)] public PortfolioBrokerEnvironment BrokerEnvironment { get; init; }
    [Key(13)] public Guid PortfolioApprovalId { get; init; }
    [Key(14)] public string MicroExecutionProfileId { get; init; } = string.Empty;
    [Key(15)] public int MicroExecutionProfileVersion { get; init; }
    [Key(16)] public string MicroExecutionProfileHash { get; init; } = string.Empty;
    [Key(17)] public string AccountPromotionApprovalReference { get; init; } = string.Empty;
    [Key(18)] public decimal RequiredCapital { get; init; }
    [Key(19)] public decimal MaximumLoss { get; init; }
    [Key(20)] public PortfolioBrokerOrderType BrokerOrderType { get; init; } = PortfolioBrokerOrderType.Limit;
    [Key(21)] public PortfolioBrokerAlgorithm BrokerAlgorithm { get; init; }
    [Key(22)] public VolatilityWorkflowInput? VolatilityEvidence { get; init; }
    [Key(23)] public string TimeInForce { get => timeInForce; init => timeInForce = string.IsNullOrEmpty(value) ? "Day" : value; }
    private string timeInForce = "Day";
    [Key(24)] public string AlgorithmPace { get => algorithmPace; init => algorithmPace = string.IsNullOrEmpty(value) ? "Normal" : value; }
    private string algorithmPace = "Normal";
    /// <summary>Exact immutable market inputs accepted for this financial decision.</summary>
    [Key(25)] [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketDecisionEvidence? DecisionEvidence { get; init; }
    /// <summary>Source setup identity; absent on legacy and fully automated orders.</summary>
    [Key(26)] [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TomasAI.IFM.Domain.Portfolio.Shared.Contracts.FundTradeSetupReference? SetupTrade { get; init; }
}
