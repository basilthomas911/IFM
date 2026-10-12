using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Broker-neutral approved order intent.</summary>
[MessagePackObject]
public sealed record TradeOrderDefinition
{
    [Key(0)] public ushort SchemaVersion { get; init; } = 4;
    [Key(1)] public TradeOrderId Id { get; init; }
    [Key(2)] public int Revision { get; init; }
    [Key(3)] public TradeOrderStatus Status { get; init; }
    [Key(4)] public DateOnly ValueDate { get; init; }
    [Key(5)] public DateTime ValidUntilUtc { get; init; }
    [Key(6)] public string Origin { get; init; } = string.Empty;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(7)] public TradeOrderComponentDefinition[] Components { get; init; } = [];
    [Key(8)] public string DefinitionHash { get; init; } = string.Empty;
    [Key(9)] public Guid? BoundExecutionAttemptId { get; init; }
    [Key(10)] public ExecutionChannel? BoundExecutionChannel { get; init; }
    [Key(11)] public DateTime? ExecutionBoundAtUtc { get; init; }
    [Key(12)] public TradeOrderPositionType PositionType { get; init; }
    [Key(13)] public StrategyPositionId? TargetPositionId { get; init; }
    [Key(14)] public string BrokerAccountAlias { get; init; } = string.Empty;
    [Key(15)] public BrokerEnvironment BrokerEnvironment { get; init; }
    [Key(16)] public Guid PortfolioApprovalId { get; init; }
    [Key(17)] public string MicroExecutionProfileId { get; init; } = string.Empty;
    [Key(18)] public int MicroExecutionProfileVersion { get; init; }
    [Key(19)] public string MicroExecutionProfileHash { get; init; } = string.Empty;
    [Key(20)] public string AccountPromotionApprovalReference { get; init; } = string.Empty;
    [Key(21)] public decimal RequiredCapital { get; init; }
    [Key(22)] public decimal MaximumLoss { get; init; }
    [Key(23)] public BrokerOrderType BrokerOrderType { get; init; } = BrokerOrderType.Limit;
    [Key(24)] public BrokerAlgorithm BrokerAlgorithm { get; init; } = BrokerAlgorithm.None;
    /// <summary>Exact immutable analytics evidence accepted for an opening order; null for legacy/closing orders.</summary>
    [Key(25)] public VolatilityWorkflowInput? VolatilityEvidence { get; init; }
    [Key(26)] public string TimeInForce { get => timeInForce; init => timeInForce = string.IsNullOrEmpty(value) ? "Day" : value; }
    private string timeInForce = "Day";
    [Key(27)] public string AlgorithmPace { get => algorithmPace; init => algorithmPace = string.IsNullOrEmpty(value) ? "Normal" : value; }
    private string algorithmPace = "Normal";
    /// <summary>Exact immutable market inputs accepted for this financial decision.</summary>
    [Key(28)] [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketDecisionEvidence? DecisionEvidence { get; init; }
    /// <summary>Source setup identity; absent on legacy and fully automated orders.</summary>
    [Key(29)] [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TomasAI.IFM.Domain.Portfolio.Shared.Contracts.FundTradeSetupReference? SetupTrade { get; init; }
}
