using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;

// Read query parameters
internal readonly record struct GetOptionTrades(int orderId) : IBindValue
{
    public object Bind() => new { orderId };
}
internal readonly record struct GetOptionTrade(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new { orderId, tradeId };
}
internal readonly record struct GetOptionTradeSpreadData(int orderId, int tradeId, DateOnly valueDate, string tradeType) : IBindValue
{
    public object Bind() => new { orderId, tradeId, valueDate, tradeType };
}
internal readonly record struct GetOptionTradeSpreadBarData(int orderId, int tradeId, DateOnly valueDate, string tradeType, DateTime startDate, DateTime endDate) : IBindValue
{
    public object Bind() => new { orderId, tradeId, valueDate, tradeType, startDate, endDate };
}
internal readonly record struct GetOptionLegs(int tradeId) : IBindValue
{
    public object Bind() => new { tradeId };
}
internal readonly record struct GetOptionLegsWithValueDate(int tradeId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { tradeId, valueDate };
}
internal readonly record struct GetTradePositions(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new { orderId, tradeId };
}
internal readonly record struct GetTradePositionsById(int orderId, int tradeId, DateOnly valueDate, string tradeStatus, int daysToExpiry) : IBindValue
{
    public object Bind() => new { orderId, tradeId, valueDate, tradeStatus, daysToExpiry };
}
internal readonly record struct GetOptionLegData(int orderId, int tradeId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { orderId, tradeId, valueDate };
}
internal readonly record struct GetTradePosition(int orderId, int tradeId, DateOnly valueDate, string tradeStatus, int daysToExpiry, string tradeType) : IBindValue
{
    public object Bind() => new { orderId, tradeId, valueDate, tradeStatus, daysToExpiry, tradeType };
}
internal readonly record struct GetTradeHistory(int orderId) : IBindValue
{
    public object Bind() => new { orderId };
}
internal readonly record struct GetTradeOrders(DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new { startDate, endDate };
}
internal readonly record struct GetTradeLimit(int tradeId) : IBindValue
{
    public object Bind() => new { tradeId };
}
internal readonly record struct GetTradeTypeLimit(int tradeId, string tradeType) : IBindValue
{
    public object Bind() => new { tradeId, tradeType };
}
internal readonly record struct GetTradeTypeLimits(int tradeId) : IBindValue
{
    public object Bind() => new { tradeId };
}
internal readonly record struct GetTradePlacementSignal(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetTradeFills(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new { orderId, tradeId };
}
internal readonly record struct GetTradeFillData(int orderId, int tradeId, DateTime fillDate) : IBindValue
{
    public object Bind() => new { orderId, tradeId, fillDate };
}
internal readonly record struct GetTradePlans(int orderId) : IBindValue
{
    public object Bind() => new { orderId };
}
internal readonly record struct GetTradePlansByTradeId(int orderId, int tradeId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { orderId, tradeId, valueDate };
}
internal readonly record struct GetLastTradePlans(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new { orderId, tradeId };
}
internal readonly record struct GetTradePlanStopLossLimit(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new { orderId, tradeId };
}
internal readonly record struct GetTradePlansByDateRange(int orderId, int tradeId, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new { orderId, tradeId, startDate, endDate };
}
internal readonly record struct GetTradePlanForwardLossRatios(DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new { startDate, endDate };
}
internal readonly record struct GetLastTradePlanForwardLossRatio(DateOnly valueDate) : IBindValue
{
    public object Bind() => new { valueDate };
}
internal readonly record struct GetTradePlanForwardLossLimit(int orderId, int tradeId, DateOnly valueDate, string tradeType) : IBindValue
{
    public object Bind() => new { orderId, tradeId, valueDate, tradeType };
}
internal readonly record struct GetTradeLiveFeed(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new { orderId, tradeId };
}
internal readonly record struct GetTradeOrder(DateOnly valueDate, int tradeId) : IBindValue
{
    public object Bind() => new { valueDate, tradeId };
}
internal readonly record struct GetTradeOrdersByValueDate(DateOnly valueDate) : IBindValue
{
    public object Bind() => new { valueDate };
}
internal readonly record struct GetTradeFillDataByTradeId(int tradeId) : IBindValue
{
    public object Bind() => new { tradeId };
}

// Delete parameters
internal readonly record struct DeleteOptionTrade(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new { orderId, tradeId };
}
internal readonly record struct DeleteOptionLegById(int orderId, int tradeId, string contractId) : IBindValue
{
    public object Bind() => new { orderId, tradeId, contractId };
}
internal readonly record struct DeleteOptionLeg(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new { orderId, tradeId };
}
internal readonly record struct DeleteOptionLegData(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new { orderId, tradeId };
}
internal readonly record struct DeleteOptionLegDataById(int orderId, int tradeId, DateOnly valueDate, string tradeType, int daysToExpiry, string tradeStatus, string optionLegId) : IBindValue
{
    public object Bind() => new { orderId, tradeId, valueDate, tradeType, daysToExpiry, tradeStatus, optionLegId };
}
internal readonly record struct DeleteTradePosition(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new { orderId, tradeId };
}
internal readonly record struct DeleteTradePositionLowerCase(int orderid, int tradeId) : IBindValue
{
    public object Bind() => new { orderid, tradeId };
}
internal readonly record struct DeleteOptionLegDataLowerCase(int orderid, int tradeId) : IBindValue
{
    public object Bind() => new { orderid, tradeId };
}
internal readonly record struct DeleteTradePositionByPrimaryKey(int orderId, int tradeId, string tradeType, DateOnly valueDate, int daysToExpiry, string tradeStatus) : IBindValue
{
    public object Bind() => new { orderId, tradeId, tradeType, valueDate, daysToExpiry, tradeStatus };
}
internal readonly record struct DeleteTradeLimitByTradeType(int tradeId, string tradeType) : IBindValue
{
    public object Bind() => new { tradeId, tradeType };
}
internal readonly record struct DeleteTradeTypeLimit(int tradeId) : IBindValue
{
    public object Bind() => new { tradeId };
}
internal readonly record struct DeleteTradeLimit(int tradeId) : IBindValue
{
    public object Bind() => new { tradeId };
}
internal readonly record struct DeleteTradeFill(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new { orderId, tradeId };
}
internal readonly record struct DeleteTradeFillData(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new { orderId, tradeId };
}
internal readonly record struct DeleteTradePlacementSignal(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct DeleteOptionTradeSpreadData(int orderId, int tradeId, DateOnly valueDate, string tradeType) : IBindValue
{
    public object Bind() => new { orderId, tradeId, valueDate, tradeType };
}
internal readonly record struct DeleteOptionTradeSpreadBarData(int orderId, int tradeId, DateOnly valueDate, string tradeType) : IBindValue
{
    public object Bind() => new { orderId, tradeId, valueDate, tradeType };
}
internal readonly record struct DeleteTradePositionState(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new { orderId, tradeId };
}
internal readonly record struct DeleteTradeLiveFeed(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new { orderId, tradeId };
}
internal readonly record struct DeleteTradeLiveFeeds(int orderId) : IBindValue
{
    public object Bind() => new { orderId };
}
internal readonly record struct DeleteTradePlanForwardLossLimit(int orderId, int tradeId, DateOnly valueDate, string tradeType) : IBindValue
{
    public object Bind() => new { orderId, tradeId, valueDate, tradeType };
}
internal readonly record struct DeleteTradeOrder(int fundId, int orderId, int tradeId) : IBindValue
{
    public object Bind() => new { fundId, orderId, tradeId };
}

// Insert parameters
internal readonly record struct InsertOptionLeg(int orderId, int tradeId, string contractId, int quantity, decimal strikePrice, string optionLegType, string optionLegAction, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new { orderId, tradeId, contractId, quantity, strikePrice, optionLegType, optionLegAction, createdOn, createdBy, updatedOn, updatedBy };
}
internal readonly record struct InsertOptionLegData(int orderId, int tradeId, string tradeType, DateOnly valueDate, int daysToExpiry, string tradeStatus, string optionLegId, decimal bidPrice, decimal askPrice, double impliedVolatility, double delta, double gamma, double theta, double vega, double rho, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new { orderId, tradeId, tradeType, valueDate, daysToExpiry, tradeStatus, optionLegId, bidPrice, askPrice, impliedVolatility, delta, gamma, theta, vega, rho, createdOn, createdBy, updatedOn, updatedBy };
}
internal readonly record struct InsertOptionTrade(int tradeId, int orderId, DateOnly tradeDate, DateOnly maturityDate, string tradeType, string tradeState, string tradeStrategy, string tradeAction, string underlyingContractId, string underlyingAssetType, bool isPrimaryTrade, bool isHedgeTrade, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new { tradeId, orderId, tradeDate, maturityDate, tradeType, tradeState, tradeStrategy, tradeAction, underlyingContractId, underlyingAssetType, isPrimaryTrade, isHedgeTrade, createdOn, createdBy, updatedOn, updatedBy };
}
internal readonly record struct InsertTradePosition(int orderId, int tradeId, string tradeType, DateOnly valueDate, int daysToExpiry, string tradeStatus, decimal commission, int deltaHedge, decimal netSpread, decimal tradeValue, decimal tradePnl, decimal assetPrice, double otmProbability, decimal forwardPrice, double forwardLossRatio, double lossProbability, double riskFreeRate, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new { orderId, tradeId, tradeType, valueDate, daysToExpiry, tradeStatus, commission, deltaHedge, netSpread, tradeValue, tradePnl, assetPrice, otmProbability, forwardPrice, forwardLossRatio, lossProbability, riskFreeRate, createdOn, createdBy, updatedOn, updatedBy };
}
internal readonly record struct InsertTradeLimit(int tradeId, string tradeType, decimal riskMargin, decimal maxProfit, decimal maxLoss, decimal maxReturn, decimal maxLossLimit, decimal minProfitLimit, decimal maxProfitLimit, decimal minProfitTarget, decimal dailyProfitTarget, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new { tradeId, tradeType, riskMargin, maxProfit, maxLoss, maxReturn, maxLossLimit, minProfitLimit, maxProfitLimit, minProfitTarget, dailyProfitTarget, createdOn, createdBy, updatedOn, updatedBy };
}
internal readonly record struct InsertTradeLimitNoMaxLoss(int tradeId, string tradeType, decimal riskMargin, decimal maxProfit, decimal maxReturn, decimal maxLossLimit, decimal minProfitLimit, decimal maxProfitLimit, decimal minProfitTarget, decimal dailyProfitTarget, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new { tradeId, tradeType, riskMargin, maxProfit, maxReturn, maxLossLimit, minProfitLimit, maxProfitLimit, minProfitTarget, dailyProfitTarget, createdOn, createdBy, updatedOn, updatedBy };
}
internal readonly record struct InsertTradeTypeLimit(int tradeId, string tradeType, decimal maxLossLimit, decimal minProfitLimit, decimal maxProfitLimit) : IBindValue
{
    public object Bind() => new { tradeId, tradeType, maxLossLimit, minProfitLimit, maxProfitLimit };
}
internal readonly record struct InsertTradeFill(int orderId, int tradeId, DateTime fillDate, int fillQuantity, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new { orderId, tradeId, fillDate, fillQuantity, createdOn, createdBy };
}
internal readonly record struct InsertTradeFillData(int orderId, int tradeId, string contractId, DateTime fillDate, decimal bidPrice, decimal askPrice, decimal commission, string optionLegAction, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new { orderId, tradeId, contractId, fillDate, bidPrice, askPrice, commission, optionLegAction, createdOn, createdBy };
}
internal readonly record struct InsertOptionTradeSpreadData(int orderId, int tradeId, DateOnly valueDate, string tradeType, long sequenceId, decimal lossLimit, decimal winLimit, decimal forwardSpread, decimal netSpread, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new { orderId, tradeId, valueDate, tradeType, sequenceId, lossLimit, winLimit, forwardSpread, netSpread, createdOn, createdBy };
}
internal readonly record struct InsertOptionTradeSpreadBarData(int orderId, int tradeId, string tradeType, DateOnly valueDate, DateTime barDate, decimal lossLimit, decimal winLimit, decimal forwardSpread, decimal netSpread) : IBindValue
{
    public object Bind() => new { orderId, tradeId, tradeType, valueDate, barDate, lossLimit, winLimit, forwardSpread, netSpread };
}
internal readonly record struct InsertTradeLiveFeed(int orderId, int tradeId, bool liveFeed) : IBindValue
{
    public object Bind() => new { orderId, tradeId, liveFeed };
}
internal readonly record struct InsertTradePositionState(int orderId, int tradeId, string tradePositionState, DateTime openedOn, string openedBy) : IBindValue
{
    public object Bind() => new { orderId, tradeId, tradePositionState, openedOn, openedBy };
}
internal readonly record struct InsertTradeOrder(int fundId, int orderId, int tradeId, DateOnly valueDate, string tradeType, string tradeSubType, DateOnly tradeDate, DateOnly maturityDate, string tradeOrderState, string underlyingContractId, string underlyingAssetType, string orderDescription, string orderAction, string orderActionType, int orderQuantity, string orderType, decimal orderPrice, decimal orderAmount, decimal commission, decimal totalAmount, decimal tradePnl, string tradeFillType, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new { fundId, orderId, tradeId, valueDate, tradeType, tradeSubType, tradeDate, maturityDate, tradeOrderState, underlyingContractId, underlyingAssetType, orderDescription, orderAction, orderActionType, orderQuantity, orderType, orderPrice, orderAmount, commission, totalAmount, tradePnl, tradeFillType, createdOn, createdBy, updatedOn, updatedBy };
}
internal readonly record struct InsertTradePlanForwardLossLimit(int orderId, int tradeId, DateOnly valueDate, string tradeType, string limitType) : IBindValue
{
    public object Bind() => new { orderId, tradeId, valueDate, tradeType, limitType };
}
internal readonly record struct InsertTradePlanForwardLossRatio(int partitionId, DateOnly valueDate, double forwardLossRatio, long sequenceId) : IBindValue
{
    public object Bind() => new { partitionId, valueDate, forwardLossRatio, sequenceId };
}
internal readonly record struct InsertTradePlanForwardLossRatioShort(DateOnly valueDate, double forwardLossRatio, long sequenceId) : IBindValue
{
    public object Bind() => new { valueDate, forwardLossRatio, sequenceId };
}
internal readonly record struct InsertTradePlacementSignal(long sequenceId, string contractId, DateOnly valueDate, string tradePlacementSignal, decimal tradePrice, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new { sequenceId, contractId, valueDate, tradePlacementSignal, tradePrice, createdOn, createdBy };
}
internal readonly record struct InsertTradePlan(long sequenceId, int orderId, int tradeId, string tradeType, DateOnly tradeDate, DateOnly valueDate, DateOnly maturityDate, DateTime actionDate, string actionType, string actionSubType, string actionState, string actionReason, decimal tradePnl, double forwardLossRatio, double lossProbability, double mscore, decimal maxProfit, decimal maxLoss, decimal minProfitTarget, decimal dailyProfitTarget, decimal assetPrice, double assetStdDev, double assetMean, double assetPriceChange, string marketTrend, string marketVolatility, string marketDirection, string vixVolatility, string tradeRisk, double fiftyDayMA, double fiveDayXMA, double putOTMProbability, double callOTMProbability, double shortPutGamma, double shortCallGamma, string gammaRisk, decimal netPrice, decimal forwardPrice, double forwardDelta, double stopLossLimit, string trendType, string trendStrength, double rsi, double rsiSlope, string tdi, string tdiStrength, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new { sequenceId, orderId, tradeId, tradeType, tradeDate, valueDate, maturityDate, actionDate, actionType, actionSubType, actionState, actionReason, tradePnl, forwardLossRatio, lossProbability, mscore, maxProfit, maxLoss, minProfitTarget, dailyProfitTarget, assetPrice, assetStdDev, assetMean, assetPriceChange, marketTrend, marketVolatility, marketDirection, vixVolatility, tradeRisk, fiftyDayMA, fiveDayXMA, putOTMProbability, callOTMProbability, shortPutGamma, shortCallGamma, gammaRisk, netPrice, forwardPrice, forwardDelta, stopLossLimit, trendType, trendStrength, rsi, rsiSlope, tdi, tdiStrength, createdOn, createdBy };
}

// Update parameters
internal readonly record struct UpdateOptionTradeState(int orderId, int tradeId, string tradeState, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new { orderId, tradeId, tradeState, updatedOn, updatedBy };
}
internal readonly record struct UpdateTradeLiveFeed(int orderId, int tradeId, bool liveFeed) : IBindValue
{
    public object Bind() => new { orderId, tradeId, liveFeed };
}
internal readonly record struct UpdateOptionLegDataStatus(int tradeId, DateOnly valueDate, string OptionLegId, string newTradeStatus, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new { tradeId, valueDate, OptionLegId, newTradeStatus, updatedOn, updatedBy };
}
internal readonly record struct UpdateOptionLegData(int orderId, int tradeId, DateOnly valueDate, string optionLegId, decimal bidPrice, decimal askPrice, double impliedVolatility, double delta, double gamma, double theta, double vega, double rho, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new { orderId, tradeId, valueDate, optionLegId, bidPrice, askPrice, impliedVolatility, delta, gamma, theta, vega, rho, updatedOn, updatedBy };
}
internal readonly record struct UpdateTradeOrderState(int tradeId, DateOnly valueDate, string tradeOrderState, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new { tradeId, valueDate, tradeOrderState, updatedOn, updatedBy };
}
internal readonly record struct UpdateTradeOrderOrderPrice(int tradeId, DateOnly valueDate, decimal orderPrice, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new { tradeId, valueDate, orderPrice, updatedOn, updatedBy };
}
internal readonly record struct UpdateTradePosition(int orderId, int tradeId, DateOnly valueDate, string tradeStatus, int daysToExpiry, string tradeType, decimal commission, int deltaHedge, decimal netSpread, decimal tradeValue, decimal tradePnl, decimal assetPrice, double OTMProbability, double winRatio, decimal maxPrice, double hedgeProbability, double riskFreeRate, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new { orderId, tradeId, valueDate, tradeStatus, daysToExpiry, tradeType, commission, deltaHedge, netSpread, tradeValue, tradePnl, assetPrice, OTMProbability, winRatio, maxPrice, hedgeProbability, riskFreeRate, updatedOn, updatedBy };
}
