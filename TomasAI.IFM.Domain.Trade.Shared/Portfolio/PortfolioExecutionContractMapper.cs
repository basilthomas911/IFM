using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

namespace TomasAI.IFM.Domain.Trade.Shared.Portfolio;

/// <summary>Maps Portfolio-owned execution instructions at the Trade boundary.</summary>
public static class PortfolioExecutionContractMapper
{
    public static TradeOrderDefinition ToTradeOrder(this PortfolioExecutionOrderInstruction value) => new()
    {
        SchemaVersion = 4,
        Id = new(value.Id.PortfolioId, value.Id.FundId, value.Id.OrderId),
        Revision = value.Revision,
        Status = (TradeOrderStatus)value.Status,
        ValueDate = value.ValueDate,
        ValidUntilUtc = value.ValidUntilUtc,
        Origin = value.Origin,
        Components = [.. value.Components.Select(ToTradeComponent)],
        DefinitionHash = value.DefinitionHash,
        PositionType = (TradeOrderPositionType)value.PositionType,
        TargetPositionId = value.TargetPosition is { } target ? ToTradePositionId(target) : null,
        BrokerAccountAlias = value.BrokerAccountAlias,
        BrokerEnvironment = (BrokerEnvironment)value.BrokerEnvironment,
        PortfolioApprovalId = value.PortfolioApprovalId,
        MicroExecutionProfileId = value.MicroExecutionProfileId,
        MicroExecutionProfileVersion = value.MicroExecutionProfileVersion,
        MicroExecutionProfileHash = value.MicroExecutionProfileHash,
        AccountPromotionApprovalReference = value.AccountPromotionApprovalReference,
        RequiredCapital = value.RequiredCapital,
        MaximumLoss = value.MaximumLoss,
        BrokerOrderType = (BrokerOrderType)value.BrokerOrderType,
        BrokerAlgorithm = (BrokerAlgorithm)value.BrokerAlgorithm,
        VolatilityEvidence = value.VolatilityEvidence,
    };

    public static PortfolioExecutionOrderInstruction ToPortfolioInstruction(this TradeOrderDefinition value) => new()
    {
        Id = new(value.Id.PortfolioId, value.Id.FundId, value.Id.OrderId),
        Revision = value.Revision,
        Status = (PortfolioExecutionOrderStatus)value.Status,
        ValueDate = value.ValueDate,
        ValidUntilUtc = value.ValidUntilUtc,
        Origin = value.Origin,
        Components = [.. value.Components.Select(ToPortfolioComponent)],
        DefinitionHash = value.DefinitionHash,
        PositionType = (PortfolioExecutionPositionType)value.PositionType,
        TargetPosition = value.TargetPositionId is { } target ? ToPortfolioPosition(target) : null,
        BrokerAccountAlias = value.BrokerAccountAlias,
        BrokerEnvironment = (PortfolioBrokerEnvironment)value.BrokerEnvironment,
        PortfolioApprovalId = value.PortfolioApprovalId,
        MicroExecutionProfileId = value.MicroExecutionProfileId,
        MicroExecutionProfileVersion = value.MicroExecutionProfileVersion,
        MicroExecutionProfileHash = value.MicroExecutionProfileHash,
        AccountPromotionApprovalReference = value.AccountPromotionApprovalReference,
        RequiredCapital = value.RequiredCapital,
        MaximumLoss = value.MaximumLoss,
        BrokerOrderType = (PortfolioBrokerOrderType)value.BrokerOrderType,
        BrokerAlgorithm = (PortfolioBrokerAlgorithm)value.BrokerAlgorithm,
        VolatilityEvidence = value.VolatilityEvidence,
    };

    public static PortfolioExecutionComponent ToPortfolioComponent(this TradeOrderComponentDefinition value) => new()
    {
        ComponentId = value.ComponentId,
        StrategyKind = (PortfolioExecutionStrategyKind)value.StrategyKind,
        Legs = [.. value.Legs.Select(ToPortfolioLeg)],
        PermitBalancedPartialAcceptance = value.PermitBalancedPartialAcceptance,
        ReservedTradeId = value.ReservedTradeId,
        SignedNetDebitLimit = value.SignedNetDebitLimit,
        MinimumSignedNetDebitLimit = value.MinimumSignedNetDebitLimit,
        MaximumSignedNetDebitLimit = value.MaximumSignedNetDebitLimit,
        TickIncrement = value.TickIncrement,
    };

    public static TradeOrderComponentDefinition ToTradeComponent(this PortfolioExecutionComponent value) => new()
    {
        ComponentId = value.ComponentId,
        StrategyKind = (TradeStrategyKind)value.StrategyKind,
        Legs = [.. value.Legs.Select(ToTradeLeg)],
        PermitBalancedPartialAcceptance = value.PermitBalancedPartialAcceptance,
        ReservedTradeId = value.ReservedTradeId,
        SignedNetDebitLimit = value.SignedNetDebitLimit,
        MinimumSignedNetDebitLimit = value.MinimumSignedNetDebitLimit,
        MaximumSignedNetDebitLimit = value.MaximumSignedNetDebitLimit,
        TickIncrement = value.TickIncrement,
    };

    public static PortfolioPositionSnapshot ToPortfolioPosition(this StrategyPositionSnapshot value) => new()
    {
        Id = ToPortfolioPosition(value.Id),
        StrategyKind = (PortfolioExecutionStrategyKind)value.StrategyKind,
        IsOpen = value.IsOpen,
        Legs = [.. value.Legs.Select(x => new PortfolioPositionLeg
        {
            TradeLegId = x.TradeLegId,
            SignedQuantity = x.SignedQuantity,
            ContractId = x.ContractId,
            AssetFamily = (PortfolioExecutionAssetFamily)x.AssetFamily,
        })],
    };

    public static PortfolioExitWorkflowId ToPortfolioWorkflow(this ExitPositionWorkflowId value) =>
        new(ToPortfolioPosition(value.Position), value.ValueDate, value.ExitDecisionId);

    public static PortfolioPositionReference ToPortfolioPositionReference(this StrategyPositionId value) =>
        ToPortfolioPosition(value);

    static PortfolioExecutionLeg ToPortfolioLeg(TradeLegDefinition value) => new()
    {
        TradeLegId = value.TradeLegId, AssetFamily = (PortfolioExecutionAssetFamily)value.AssetFamily,
        SignedQuantity = value.SignedQuantity, LimitPrice = value.LimitPrice, ContractKey = value.ContractKey,
        Expiry = value.Expiry, Strike = value.Strike, PutCall = value.PutCall, ContractId = value.ContractId,
        CashMultiplier = value.CashMultiplier,
    };

    static TradeLegDefinition ToTradeLeg(PortfolioExecutionLeg value) => new()
    {
        TradeLegId = value.TradeLegId, AssetFamily = (TradeAssetFamily)value.AssetFamily,
        SignedQuantity = value.SignedQuantity, LimitPrice = value.LimitPrice, ContractKey = value.ContractKey,
        Expiry = value.Expiry, Strike = value.Strike, PutCall = value.PutCall, ContractId = value.ContractId,
        CashMultiplier = value.CashMultiplier,
    };

    static PortfolioPositionReference ToPortfolioPosition(StrategyPositionId value) =>
        new(value.Trade.PortfolioId, value.Trade.FundId, value.Trade.OrderId, value.Trade.TradeId, value.PositionId);

    static StrategyPositionId ToTradePositionId(PortfolioPositionReference value) =>
        new(new TradeEntityId(value.PortfolioId, value.FundId, value.OrderId, value.TradeId), value.PositionId);
}
