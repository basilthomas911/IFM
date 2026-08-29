using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

// Read query parameters
internal readonly record struct GetOptionTrades(int orderId) : IBindValue
{
    public object Bind() => new object?[] { orderId };
}
internal readonly record struct GetOptionTrade(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId };
}
internal readonly record struct GetOptionTradeSpreadData(int orderId, int tradeId, DateOnly valueDate, string tradeType) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, valueDate, tradeType };
}
internal readonly record struct GetOptionTradeSpreadBarData(int orderId, int tradeId, DateOnly valueDate, string tradeType, DateTime startDate, DateTime endDate) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, valueDate, tradeType, startDate, endDate };
}
internal readonly record struct GetOptionLegs(int tradeId) : IBindValue
{
    public object Bind() => new object?[] { tradeId };
}
internal readonly record struct GetOptionLegsByOrderAndTrade(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId };
}
internal readonly record struct GetOptionLegsWithValueDate(int tradeId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { tradeId };
}
internal readonly record struct GetTradePositions(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId };
}
internal readonly record struct GetTradePositionsById(int orderId, int tradeId, DateOnly valueDate, string tradeStatus, int daysToExpiry) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, valueDate, tradeStatus, daysToExpiry };
}
internal readonly record struct GetOptionLegData(int orderId, int tradeId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, valueDate };
}
internal readonly record struct GetTradePosition(int orderId, int tradeId, DateOnly valueDate, string tradeStatus, int daysToExpiry, string tradeType) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, valueDate, tradeStatus, daysToExpiry, tradeType };
}
internal readonly record struct GetTradeHistory(int orderId) : IBindValue
{
    public object Bind() => new object?[] { orderId };
}
internal readonly record struct GetTradeOrders(DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { startDate, endDate };
}
internal readonly record struct GetTradeLimit(int tradeId) : IBindValue
{
    public object Bind() => new object?[] { tradeId };
}
internal readonly record struct GetTradeTypeLimit(int tradeId, string tradeType) : IBindValue
{
    public object Bind() => new object?[] { tradeId, tradeType };
}
internal readonly record struct GetTradeTypeLimits(int tradeId) : IBindValue
{
    public object Bind() => new object?[] { tradeId };
}
internal readonly record struct GetTradePlacementSignal(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct GetTradeFills(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId };
}
internal readonly record struct GetTradeFillData(int orderId, int tradeId, DateTime fillDate) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, fillDate };
}
internal readonly record struct GetTradePlans(int orderId) : IBindValue
{
    public object Bind() => new object?[] { orderId };
}
internal readonly record struct GetTradePlansByTradeId(int orderId, int tradeId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, valueDate };
}
internal readonly record struct GetLastTradePlans(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId };
}
internal readonly record struct GetTradePlanStopLossLimit(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId };
}
internal readonly record struct GetTradePlansByDateRange(int orderId, int tradeId, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, startDate, endDate };
}
internal readonly record struct GetTradePlanForwardLossRatios(DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { startDate, endDate };
}
internal readonly record struct GetLastTradePlanForwardLossRatio(DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { valueDate };
}
internal readonly record struct GetTradePlanForwardLossLimit(int orderId, int tradeId, DateOnly valueDate, string tradeType) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, valueDate, tradeType };
}
internal readonly record struct GetTradeLiveFeed(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId };
}
internal readonly record struct GetTradeOrder(DateOnly valueDate, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { tradeId, valueDate };
}
internal readonly record struct GetTradeOrdersByValueDate(DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { valueDate };
}
internal readonly record struct GetTradeFillDataByTradeId(int tradeId) : IBindValue
{
    public object Bind() => new object?[] { tradeId };
}

// Delete parameters
internal readonly record struct DeleteOptionTrade(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId };
}
internal readonly record struct DeleteOptionLegById(int orderId, int tradeId, string contractId) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, contractId };
}
internal readonly record struct DeleteOptionLeg(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId };
}
internal readonly record struct DeleteOptionLegData(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId };
}
internal readonly record struct DeleteOptionLegDataById(int orderId, int tradeId, DateOnly valueDate, string tradeType, int daysToExpiry, string tradeStatus, string optionLegId) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, valueDate, optionLegId };
}
internal readonly record struct DeleteTradePosition(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId };
}
internal readonly record struct DeleteTradePositionLowerCase(int orderid, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { orderid, tradeId };
}
internal readonly record struct DeleteOptionLegDataLowerCase(int orderid, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { orderid, tradeId };
}
internal readonly record struct DeleteTradePositionByPrimaryKey(int orderId, int tradeId, string tradeType, DateOnly valueDate, int daysToExpiry, string tradeStatus) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, valueDate, tradeStatus, daysToExpiry, tradeType };
}
internal readonly record struct DeleteTradeLimitByTradeType(int tradeId, string tradeType) : IBindValue
{
    public object Bind() => new object?[] { tradeId, tradeType };
}
internal readonly record struct DeleteTradeTypeLimit(int tradeId) : IBindValue
{
    public object Bind() => new object?[] { tradeId };
}
internal readonly record struct DeleteTradeLimit(int tradeId) : IBindValue
{
    public object Bind() => new object?[] { tradeId };
}
internal readonly record struct DeleteTradeFill(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId };
}
internal readonly record struct DeleteTradeFillData(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId };
}
internal readonly record struct DeleteTradePlacementSignal(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct DeleteOptionTradeSpreadData(int orderId, int tradeId, DateOnly valueDate, string tradeType) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, valueDate, tradeType };
}
internal readonly record struct DeleteOptionTradeSpreadBarData(int orderId, int tradeId, DateOnly valueDate, string tradeType) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, valueDate, tradeType };
}
internal readonly record struct DeleteTradePositionState(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId };
}
internal readonly record struct DeleteTradeLiveFeed(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId };
}
internal readonly record struct DeleteTradeLiveFeeds(int orderId) : IBindValue
{
    public object Bind() => new object?[] { orderId };
}
internal readonly record struct DeleteTradePlanForwardLossLimit(int orderId, int tradeId, DateOnly valueDate, string tradeType) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, valueDate, tradeType };
}
internal readonly record struct DeleteTradeOrder(int fundId, int orderId, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { fundId, orderId, tradeId };
}

// Insert parameters
internal readonly record struct InsertOptionLeg(int orderId, int tradeId, string contractId, int quantity, decimal strikePrice, string optionLegType, string optionLegAction, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, contractId, quantity, strikePrice, optionLegType, optionLegAction, createdOn, createdBy, updatedOn, updatedBy };
}
internal readonly record struct InsertOptionLegData(int orderId, int tradeId, string tradeType, DateOnly valueDate, int daysToExpiry, string tradeStatus, string optionLegId, decimal bidPrice, decimal askPrice, double impliedVolatility, double delta, double gamma, double theta, double vega, double rho, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, tradeType, valueDate, daysToExpiry, tradeStatus, optionLegId, bidPrice, askPrice, impliedVolatility, delta, gamma, theta, vega, rho, createdOn, createdBy, updatedOn, updatedBy };
}
internal readonly record struct InsertOptionTrade(int tradeId, int orderId, DateOnly tradeDate, DateOnly maturityDate, string tradeType, string tradeState, string tradeStrategy, string tradeAction, string underlyingContractId, string underlyingAssetType, bool isPrimaryTrade, bool isHedgeTrade, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, tradeDate, maturityDate, tradeType, tradeState, tradeStrategy, tradeAction, underlyingContractId, underlyingAssetType, isPrimaryTrade, isHedgeTrade, createdOn, createdBy, updatedOn, updatedBy };
}
internal readonly record struct InsertTradePosition(int orderId, int tradeId, string tradeType, DateOnly valueDate, int daysToExpiry, string tradeStatus, decimal commission, int deltaHedge, decimal netSpread, decimal tradeValue, decimal tradePnl, decimal assetPrice, double otmProbability, decimal forwardPrice, double forwardLossRatio, double lossProbability, double riskFreeRate, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, tradeType, valueDate, daysToExpiry, tradeStatus, commission, deltaHedge, netSpread, tradeValue, tradePnl, assetPrice, otmProbability, forwardPrice, forwardLossRatio, lossProbability, riskFreeRate, createdOn, createdBy, updatedOn, updatedBy };
}
internal readonly record struct InsertTradeLimit(int tradeId, string tradeType, decimal riskMargin, decimal maxProfit, decimal maxLoss, decimal maxReturn, decimal maxLossLimit, decimal minProfitLimit, decimal maxProfitLimit, decimal minProfitTarget, decimal dailyProfitTarget, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { tradeId, tradeType, riskMargin, maxProfit, maxLoss, maxReturn, maxLossLimit, minProfitLimit, maxProfitLimit, minProfitTarget, dailyProfitTarget, createdOn, createdBy, updatedOn, updatedBy };
}
internal readonly record struct InsertTradeLimitNoMaxLoss(int tradeId, string tradeType, decimal riskMargin, decimal maxProfit, decimal maxReturn, decimal maxLossLimit, decimal minProfitLimit, decimal maxProfitLimit, decimal minProfitTarget, decimal dailyProfitTarget, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { tradeId, tradeType, riskMargin, maxProfit, null, maxReturn, maxLossLimit, minProfitLimit, maxProfitLimit, minProfitTarget, dailyProfitTarget, createdOn, createdBy, updatedOn, updatedBy };
}
internal readonly record struct InsertTradeTypeLimit(int tradeId, string tradeType, decimal maxLossLimit, decimal minProfitLimit, decimal maxProfitLimit) : IBindValue
{
    public object Bind() => new object?[] { tradeId, tradeType, maxLossLimit, minProfitLimit, maxProfitLimit };
}
internal readonly record struct InsertTradeFill(int orderId, int tradeId, DateTime fillDate, int fillQuantity, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, fillDate, fillQuantity, createdOn, createdBy };
}
internal readonly record struct InsertTradeFillData(int orderId, int tradeId, string contractId, DateTime fillDate, decimal bidPrice, decimal askPrice, decimal commission, string optionLegAction, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, contractId, fillDate, bidPrice, askPrice, commission, optionLegAction, createdOn, createdBy };
}
internal readonly record struct InsertOptionTradeSpreadData(int orderId, int tradeId, DateOnly valueDate, string tradeType, long sequenceId, decimal lossLimit, decimal winLimit, decimal forwardSpread, decimal netSpread, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, tradeType, valueDate, sequenceId, lossLimit, winLimit, forwardSpread, netSpread, createdOn, createdBy };
}
internal readonly record struct InsertOptionTradeSpreadBarData(int orderId, int tradeId, string tradeType, DateOnly valueDate, DateTime barDate, decimal lossLimit, decimal winLimit, decimal forwardSpread, decimal netSpread) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, tradeType, valueDate, barDate, lossLimit, winLimit, forwardSpread, netSpread };
}
internal readonly record struct InsertTradeLiveFeed(int orderId, int tradeId, bool liveFeed) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, liveFeed };
}
internal readonly record struct InsertTradePositionState(int orderId, int tradeId, string tradePositionState, DateTime openedOn, string openedBy) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, tradePositionState, openedOn, openedBy };
}
internal readonly record struct InsertTradeOrder(int fundId, int orderId, int tradeId, DateOnly valueDate, string tradeType, string tradeSubType, DateOnly tradeDate, DateOnly maturityDate, string tradeOrderState, string underlyingContractId, string underlyingAssetType, string orderDescription, string orderAction, string orderActionType, int orderQuantity, string orderType, decimal orderPrice, decimal orderAmount, decimal commission, decimal totalAmount, decimal tradePnl, string tradeFillType, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { fundId, orderId, tradeId, valueDate, tradeType, tradeSubType, tradeDate, maturityDate, tradeOrderState, underlyingContractId, underlyingAssetType, orderDescription, orderAction, orderActionType, orderQuantity, orderType, orderPrice, orderAmount, commission, totalAmount, tradePnl, tradeFillType, createdOn, createdBy, updatedOn, updatedBy };
}
internal readonly record struct InsertTradePlanForwardLossLimit(int orderId, int tradeId, DateOnly valueDate, string tradeType, string limitType) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, tradeType, valueDate, limitType };
}
internal readonly record struct InsertTradePlanForwardLossRatio(int partitionId, DateOnly valueDate, double forwardLossRatio, long sequenceId) : IBindValue
{
    public object Bind() => new object?[] { partitionId, valueDate, forwardLossRatio, sequenceId };
}
internal readonly record struct InsertTradePlacementSignal(long sequenceId, string contractId, DateOnly valueDate, string tradePlacementSignal, decimal tradePrice, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new object?[] { sequenceId, contractId, valueDate, tradePlacementSignal, tradePrice, createdOn, createdBy };
}
internal readonly record struct InsertTradePlan(long sequenceId, int orderId, int tradeId, string tradeType, DateOnly tradeDate, DateOnly valueDate, DateOnly maturityDate, DateTime actionDate, string actionType, string actionSubType, string actionState, string actionReason, decimal tradePnl, double forwardLossRatio, double lossProbability, double mscore, decimal maxProfit, decimal maxLoss, decimal minProfitTarget, decimal dailyProfitTarget, decimal assetPrice, double assetStdDev, double assetMean, double assetPriceChange, string marketTrend, string marketVolatility, string marketDirection, string vixVolatility, string tradeRisk, double fiftyDayMA, double fiveDayXMA, double putOTMProbability, double callOTMProbability, double shortPutGamma, double shortCallGamma, string gammaRisk, decimal netPrice, decimal forwardPrice, double forwardDelta, double stopLossLimit, string trendType, string trendStrength, double rsi, double rsiSlope, string tdi, string tdiStrength, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new object?[] { sequenceId, orderId, tradeId, tradeType, tradeDate, valueDate, maturityDate, actionDate, actionType, actionSubType, actionState, actionReason, tradePnl, forwardLossRatio, lossProbability, mscore, maxProfit, maxLoss, minProfitTarget, dailyProfitTarget, assetPrice, assetStdDev, assetMean, assetPriceChange, marketTrend, marketVolatility, marketDirection, vixVolatility, tradeRisk, fiftyDayMA, fiveDayXMA, putOTMProbability, callOTMProbability, shortPutGamma, shortCallGamma, gammaRisk, netPrice, forwardPrice, forwardDelta, stopLossLimit, trendType, trendStrength, rsi, rsiSlope, tdi, tdiStrength, createdOn, createdBy };
}

// Update parameters
internal readonly record struct UpdateOptionTradeState(int orderId, int tradeId, string tradeState, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { tradeState, orderId, tradeId };
}
internal readonly record struct UpdateTradeLiveFeed(int orderId, int tradeId, bool liveFeed) : IBindValue
{
    public object Bind() => new object?[] { liveFeed, orderId, tradeId };
}
internal readonly record struct UpdateOptionLegDataStatus(int tradeId, DateOnly valueDate, string OptionLegId, string newTradeStatus, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { newTradeStatus, updatedOn, updatedBy, tradeId, valueDate, OptionLegId };
}
internal readonly record struct UpdateOptionLegData(int orderId, int tradeId, DateOnly valueDate, string optionLegId, decimal bidPrice, decimal askPrice, double impliedVolatility, double delta, double gamma, double theta, double vega, double rho, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { bidPrice, askPrice, impliedVolatility, delta, gamma, theta, vega, rho, updatedOn, updatedBy, orderId, tradeId, valueDate, optionLegId };
}
internal readonly record struct UpdateTradeOrderState(int tradeId, DateOnly valueDate, string tradeOrderState, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { tradeOrderState, updatedOn, updatedBy, tradeId, valueDate };
}
internal readonly record struct UpdateTradeOrderOrderPrice(int tradeId, DateOnly valueDate, decimal orderPrice, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { orderPrice, updatedOn, updatedBy, tradeId, valueDate };
}
internal readonly record struct UpdateTradePosition(int orderId, int tradeId, DateOnly valueDate, string tradeStatus, int daysToExpiry, string tradeType, decimal commission, int deltaHedge, decimal netSpread, decimal tradeValue, decimal tradePnl, decimal assetPrice, double OTMProbability, double winRatio, decimal maxPrice, double hedgeProbability, double riskFreeRate, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { commission, deltaHedge, netSpread, tradeValue, tradePnl, assetPrice, OTMProbability, null, null, riskFreeRate, updatedOn, updatedBy, orderId, tradeId, valueDate, tradeStatus, daysToExpiry, tradeType };
}

internal readonly record struct GetIntrinsicTimeStrategyWorkflow(Guid WorkflowId) : IBindValue
{
    public object Bind() => new object?[] { WorkflowId };
}

internal readonly record struct GetRegimeDiscovery(Guid WorkflowId) : IBindValue
{
    public object Bind() => new object?[] { WorkflowId };
}

internal readonly record struct GetMarketCondition(Guid WorkflowId) : IBindValue
{
    public object Bind() => new object?[] { WorkflowId };
}

internal readonly record struct GetMarketConditionHistory(
    int FundId, string InstrumentRoot, string TargetHorizon, DateTime BeforeUtc, int PageSize) : IBindValue
{
    public object Bind() => new object?[] { FundId, InstrumentRoot, TargetHorizon, BeforeUtc, PageSize };
}

internal readonly record struct UpsertMarketConditionByFund(
    int FundId, string InstrumentRoot, string TargetHorizon, DateTime EvaluatedAtUtc, Guid WorkflowId,
    string WorkflowEntityId, long InputWorkflowRevision, Guid CommandId, Guid SourceEventId,
    Guid ParameterSetId, int ParameterSetVersion, string ParameterPayloadSha256, Guid SnapshotId,
    string SnapshotSha256, string Tradeability, string ConditionType, string Direction, string Phase,
    decimal Strength, decimal Confidence, string PrimaryReasonCode, byte[] ResultPayload,
    string ResultPayloadSha256, DateTime ValidUntilUtc, DateTime MarketDataAsOfUtc,
    DateTime CompletedAtUtc, DateTime UpdatedAtUtc, string VolatilityBehavior, string LiquidityQuality,
    string DataQuality, string UpstreamAlignment, byte[] EvidencePayload, byte[] ConflictingEvidencePayload,
    byte[] BlockingReasonsPayload, byte[] ReasonsPayload, string SummaryText) : IBindValue
{
    public object Bind() => new object?[] { FundId, InstrumentRoot, TargetHorizon, EvaluatedAtUtc,
        WorkflowId, WorkflowEntityId, InputWorkflowRevision, CommandId, SourceEventId, ParameterSetId,
        ParameterSetVersion, ParameterPayloadSha256, SnapshotId, SnapshotSha256, Tradeability,
        ConditionType, Direction, Phase, Strength, Confidence, PrimaryReasonCode, ResultPayload,
        ResultPayloadSha256, ValidUntilUtc, MarketDataAsOfUtc, CompletedAtUtc, UpdatedAtUtc,
        VolatilityBehavior, LiquidityQuality, DataQuality, UpstreamAlignment, EvidencePayload,
        ConflictingEvidencePayload, BlockingReasonsPayload, ReasonsPayload, SummaryText };
}

internal readonly record struct UpsertMarketCondition(
    Guid WorkflowId, string WorkflowEntityId, long InputWorkflowRevision, Guid CommandId, Guid SourceEventId,
    int FundId, string InstrumentRoot, string TargetHorizon, Guid ParameterSetId, int ParameterSetVersion,
    string ParameterPayloadSha256, Guid SnapshotId, string SnapshotSha256, string Tradeability,
    string ConditionType, string Direction, string Phase, decimal Strength, decimal Confidence,
    string PrimaryReasonCode, byte[] ResultPayload, string ResultPayloadSha256, DateTime EvaluatedAtUtc,
    DateTime ValidUntilUtc, DateTime MarketDataAsOfUtc, DateTime CompletedAtUtc, DateTime UpdatedAtUtc,
    string VolatilityBehavior, string LiquidityQuality, string DataQuality, string UpstreamAlignment,
    byte[] EvidencePayload, byte[] ConflictingEvidencePayload, byte[] BlockingReasonsPayload,
    byte[] ReasonsPayload, string SummaryText) : IBindValue
{
    public object Bind() => new object?[] { WorkflowId, WorkflowEntityId, InputWorkflowRevision, CommandId,
        SourceEventId, FundId, InstrumentRoot, TargetHorizon, ParameterSetId, ParameterSetVersion,
        ParameterPayloadSha256, SnapshotId, SnapshotSha256, Tradeability, ConditionType, Direction, Phase,
        Strength, Confidence, PrimaryReasonCode, ResultPayload, ResultPayloadSha256, EvaluatedAtUtc,
        ValidUntilUtc, MarketDataAsOfUtc, CompletedAtUtc, UpdatedAtUtc, VolatilityBehavior,
        LiquidityQuality, DataQuality, UpstreamAlignment, EvidencePayload, ConflictingEvidencePayload,
        BlockingReasonsPayload, ReasonsPayload, SummaryText };
}

internal readonly record struct UpsertRegimeDiscovery(
    Guid WorkflowId,
    string WorkflowEntityId,
    long InputWorkflowRevision,
    Guid CommandId,
    Guid SourceEventId,
    long SourceEventSequence,
    string Status,
    string ParameterPayloadSha256,
    Guid SignalSnapshotId,
    byte[] ResultPayload,
    string ResultPayloadSha256,
    int FailureCode,
    string FailureMessage,
    byte[] ReasonsPayload,
    int SchemaVersion,
    DateTime TerminalAtUtc,
    DateTime UpdatedAtUtc) : IBindValue
{
    public object Bind() => new object?[]
    {
        WorkflowId, WorkflowEntityId, InputWorkflowRevision, CommandId, SourceEventId,
        SourceEventSequence, Status, ParameterPayloadSha256, SignalSnapshotId, ResultPayload,
        ResultPayloadSha256, FailureCode, FailureMessage, ReasonsPayload, SchemaVersion,
        TerminalAtUtc, UpdatedAtUtc
    };
}

internal readonly record struct GetActiveIntrinsicTimeStrategyWorkflow(string WorkflowEntityId) : IBindValue
{
    public object Bind() => new object?[] { WorkflowEntityId };
}

internal readonly record struct GetIntrinsicTimeStrategyWorkflowStartAttempts(
    string WorkflowEntityId,
    DateTime BeforeUtc,
    int PageSize) : IBindValue
{
    public object Bind() => new object?[] { WorkflowEntityId, BeforeUtc, PageSize };
}

internal readonly record struct GetIntrinsicTimeStrategyWorkflowTimeline(
    Guid WorkflowId,
    long AfterEventId,
    int PageSize) : IBindValue
{
    public object Bind() => new object?[] { WorkflowId, AfterEventId, PageSize };
}

internal readonly record struct GetIntrinsicTimeStrategyWorkflowsByEntity(
    string WorkflowEntityId,
    DateTime BeforeUtc,
    int PageSize) : IBindValue
{
    public object Bind() => new object?[] { WorkflowEntityId, BeforeUtc, PageSize };
}

internal readonly record struct GetIntrinsicTimeStrategyWorkflowsByStatusDay(
    string Status,
    DateOnly StartedDate,
    int PageSize) : IBindValue
{
    public object Bind() => new object?[] { Status, StartedDate, PageSize };
}

internal readonly record struct UpsertIntrinsicTimeStrategyWorkflow(
    Guid WorkflowId,
    string WorkflowEntityId,
    string WorkflowDefinitionId,
    int WorkflowDefinitionVersion,
    string ContractId,
    DateOnly TimeFrameStartValueDate,
    string TimePeriod,
    Guid TriggerEventId,
    Guid CorrelationId,
    string Status,
    string Outcome,
    string CurrentStage,
    long WorkflowRevision,
    long LastEventId,
    int StateSchemaVersion,
    byte[] StatePayload,
    string StopReasonCode,
    DateTime StartedAtUtc,
    DateTime? TerminalAtUtc,
    DateTime UpdatedAtUtc) : IBindValue
{
    public object Bind() => new object?[]
    {
        WorkflowId, WorkflowEntityId, WorkflowDefinitionId, WorkflowDefinitionVersion,
        ContractId, TimeFrameStartValueDate, TimePeriod, TriggerEventId, CorrelationId,
        Status, Outcome, CurrentStage, WorkflowRevision, LastEventId, StateSchemaVersion,
        StatePayload, StopReasonCode, StartedAtUtc, TerminalAtUtc, UpdatedAtUtc
    };
}

internal readonly record struct UpsertActiveIntrinsicTimeStrategyWorkflow(
    string WorkflowEntityId,
    Guid WorkflowId,
    string ContractId,
    DateOnly TimeFrameStartValueDate,
    string TimePeriod,
    string CurrentStage,
    long WorkflowRevision,
    long LastEventId,
    int StateSchemaVersion,
    byte[] StatePayload,
    DateTime StartedAtUtc,
    DateTime UpdatedAtUtc) : IBindValue
{
    public object Bind() => new object?[]
    {
        WorkflowEntityId, WorkflowId, ContractId, TimeFrameStartValueDate, TimePeriod,
        CurrentStage, WorkflowRevision, LastEventId, StateSchemaVersion, StatePayload,
        StartedAtUtc, UpdatedAtUtc
    };
}

internal readonly record struct DeleteActiveIntrinsicTimeStrategyWorkflow(string WorkflowEntityId) : IBindValue
{
    public object Bind() => new object?[] { WorkflowEntityId };
}

internal readonly record struct InsertIntrinsicTimeStrategyWorkflowStartAttempt(
    string WorkflowEntityId,
    DateTime RequestedAtUtc,
    Guid RequestedWorkflowId,
    string Decision,
    Guid? ActiveWorkflowId,
    Guid StartCommandId,
    Guid TriggerEventId,
    string ActiveStage,
    string ReasonCode,
    long SourceEventId) : IBindValue
{
    public object Bind() => new object?[]
    {
        WorkflowEntityId, RequestedAtUtc, RequestedWorkflowId, Decision, ActiveWorkflowId,
        StartCommandId, TriggerEventId, ActiveStage, ReasonCode, SourceEventId
    };
}

internal readonly record struct InsertIntrinsicTimeStrategyWorkflowTimeline(
    Guid WorkflowId,
    long EventId,
    string WorkflowEntityId,
    long WorkflowRevision,
    string Stage,
    string EventName,
    int EventSchemaVersion,
    byte[] EventPayload,
    DateTime OccurredAtUtc) : IBindValue
{
    public object Bind() => new object?[]
    {
        WorkflowId, EventId, WorkflowEntityId, WorkflowRevision, Stage, EventName,
        EventSchemaVersion, EventPayload, OccurredAtUtc
    };
}

internal readonly record struct UpsertIntrinsicTimeStrategyWorkflowByEntity(
    string WorkflowEntityId,
    DateTime StartedAtUtc,
    Guid WorkflowId,
    string Status,
    string Outcome,
    string CurrentStage,
    long WorkflowRevision,
    DateTime? TerminalAtUtc,
    string StopReasonCode) : IBindValue
{
    public object Bind() => new object?[]
    {
        WorkflowEntityId, StartedAtUtc, WorkflowId, Status, Outcome, CurrentStage,
        WorkflowRevision, TerminalAtUtc, StopReasonCode
    };
}

internal readonly record struct UpsertIntrinsicTimeStrategyWorkflowByStatusDay(
    string Status,
    DateOnly StartedDate,
    DateTime StartedAtUtc,
    Guid WorkflowId,
    string WorkflowEntityId,
    string Outcome,
    string CurrentStage,
    long WorkflowRevision,
    DateTime? TerminalAtUtc,
    string StopReasonCode) : IBindValue
{
    public object Bind() => new object?[]
    {
        Status, StartedDate, StartedAtUtc, WorkflowId, WorkflowEntityId, Outcome,
        CurrentStage, WorkflowRevision, TerminalAtUtc, StopReasonCode
    };
}
