using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

public enum PortfolioExecutionAssetFamily : byte { Unknown, Futures, FuturesOption, Equity, FixedIncome, Custom }
public enum PortfolioExecutionStrategyKind : byte { Unknown, FuturesOutright, VanillaOption, VerticalSpread, IronCondor, Custom }
public enum PortfolioExecutionPositionType : byte { Unknown, Opening, Closing }
public enum PortfolioExecutionOrderStatus : byte { Draft, Approved, Ready, Executing, Completed, Cancelled, Expired }
public enum PortfolioBrokerEnvironment : byte { Unknown, Emulator, Paper, Live }
public enum PortfolioBrokerOrderType : byte { Unknown, Market, Limit }
public enum PortfolioBrokerAlgorithm : byte { None, Adaptive }

[MessagePackObject]
public readonly record struct PortfolioExecutionOrderId(
    [property: Key(0)] int PortfolioId,
    [property: Key(1)] int FundId,
    [property: Key(2)] int OrderId)
{
    [IgnoreMember] public bool IsValid => PortfolioId > 0 && FundId > 0 && OrderId > 0;
}

[MessagePackObject]
public readonly record struct PortfolioPositionReference(
    [property: Key(0)] int PortfolioId,
    [property: Key(1)] int FundId,
    [property: Key(2)] int OrderId,
    [property: Key(3)] int TradeId,
    [property: Key(4)] Guid PositionId)
{
    [IgnoreMember] public bool IsValid => PortfolioId > 0 && FundId > 0 && OrderId > 0 && TradeId > 0 && PositionId != Guid.Empty;
    public string Format() => $"{PortfolioId}.{FundId}.{OrderId}.{TradeId}.{PositionId:N}";
}

[MessagePackObject]
public readonly record struct PortfolioExitWorkflowId(
    [property: Key(0)] PortfolioPositionReference Position,
    [property: Key(1)] DateOnly ValueDate,
    [property: Key(2)] Guid ExitDecisionId)
{
    [IgnoreMember] public bool IsValid => Position.IsValid && ValueDate != default && ExitDecisionId != Guid.Empty;
}

[MessagePackObject]
public sealed record PortfolioExecutionLeg
{
    [Key(0)] public Guid TradeLegId { get; init; }
    [Key(1)] public PortfolioExecutionAssetFamily AssetFamily { get; init; }
    [Key(2)] public int SignedQuantity { get; init; }
    [Key(3)] public decimal? LimitPrice { get; init; }
    [Key(4)] public string ContractKey { get; init; } = string.Empty;
    [Key(5)] public DateOnly? Expiry { get; init; }
    [Key(6)] public decimal? Strike { get; init; }
    [Key(7)] public byte? PutCall { get; init; }
    [Key(8)] public string ContractId { get; init; } = string.Empty;
    [Key(9)] public decimal CashMultiplier { get; init; }
}

[MessagePackObject]
public sealed record PortfolioExecutionComponent
{
    [Key(0)] public Guid ComponentId { get; init; }
    [Key(1)] public PortfolioExecutionStrategyKind StrategyKind { get; init; }
    [Key(2)] public PortfolioExecutionLeg[] Legs { get; init; } = [];
    [Key(3)] public bool PermitBalancedPartialAcceptance { get; init; }
    [Key(4)] public int ReservedTradeId { get; init; }
    [Key(5)] public decimal? SignedNetDebitLimit { get; init; }
    [Key(6)] public decimal? MinimumSignedNetDebitLimit { get; init; }
    [Key(7)] public decimal? MaximumSignedNetDebitLimit { get; init; }
    [Key(8)] public decimal? TickIncrement { get; init; }
}

[MessagePackObject]
public sealed record PortfolioPositionLeg
{
    [Key(0)] public Guid TradeLegId { get; init; }
    [Key(1)] public int SignedQuantity { get; init; }
    [Key(2)] public string ContractId { get; init; } = string.Empty;
    [Key(3)] public PortfolioExecutionAssetFamily AssetFamily { get; init; }
}

[MessagePackObject]
public sealed record PortfolioPositionSnapshot
{
    [Key(0)] public PortfolioPositionReference Id { get; init; }
    [Key(1)] public PortfolioExecutionStrategyKind StrategyKind { get; init; }
    [Key(2)] public PortfolioPositionLeg[] Legs { get; init; } = [];
    [Key(3)] public bool IsOpen { get; init; }
}

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
}
