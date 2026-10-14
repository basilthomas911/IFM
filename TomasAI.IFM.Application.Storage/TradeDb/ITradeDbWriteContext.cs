using TomasAI.IFM.Domain.Trade.Shared;

using TomasAI.IFM.Domain.Trade.Shared.ViewModels;



using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

namespace TomasAI.IFM.Application.Storage.TradeDb;
/// <summary>Typed trade mutations and caller-owned transaction projection writes.</summary>
public interface ITradeDbWriteContext : IEnlistedTradeDbWriter
{
    /// <summary>Upsert Order Composition through the typed trade persistence boundary.</summary>
    Task UpsertOrderCompositionAsync(OrderCompositionFunctionCompletedEvent completed, CancellationToken token = default);
    /// <summary>Upsert Market Condition Assessment through the typed trade persistence boundary.</summary>
    Task UpsertMarketConditionAssessmentAsync(MarketConditionAssessmentCompletedEvent completed, CancellationToken cancellationToken = default);
    /// <summary>Upsert Trade Selection through the typed trade persistence boundary.</summary>
    Task UpsertTradeSelectionAsync(TradeSelectionFunctionCompletedEvent completed, CancellationToken cancellationToken = default);
    /// <summary>Upsert Risk History through the typed trade persistence boundary.</summary>
    Task<RiskHistoryProjectionResult> UpsertRiskHistoryAsync(WorkflowStrategyStateUpdatedEvent snapshot, CancellationToken token = default);
    /// <summary>Projects a whole-trade limit and its spread limits as one accepted configuration.</summary>
    Task ProjectMonitoringLimitsAsync(TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeLimitReadModel limit, IReadOnlyList<TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeTypeLimitReadModel> spreads, CancellationToken token = default) => Task.WhenAll(InsertTradeLimitAsync(limit), InsertTradeTypeLimitsAsync((ICollection<TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeTypeLimitReadModel>)spreads.ToArray()));
    /// <summary>Upsert Trade Order through the typed trade persistence boundary.</summary>
    Task UpsertTradeOrderAsync(TradeOrderDefinition order, CancellationToken token = default);
    /// <summary>Projects an accepted order with the timestamp recorded in its source event.</summary>
    Task UpsertTradeOrderAsync(TradeOrderDefinition order, DateTime acceptedAtUtc, CancellationToken token = default) => UpsertTradeOrderAsync(order, token);
    /// <summary>Upsert Order Execution through the typed trade persistence boundary.</summary>
    Task UpsertOrderExecutionAsync(OrderExecutionDefinition execution, CancellationToken token = default);
    /// <summary>Upsert Established Trade through the typed trade persistence boundary.</summary>
    Task UpsertEstablishedTradeAsync(EstablishedTradeDefinition trade, CancellationToken token = default);
    /// <summary>Upsert Strategy Position through the typed trade persistence boundary.</summary>
    Task UpsertStrategyPositionAsync(StrategyPositionSnapshot position, CancellationToken token = default);
    /// <summary>Replace Open Position Routes through the typed trade persistence boundary.</summary>
    Task ReplaceOpenPositionRoutesAsync(StrategyPositionSnapshot position, CancellationToken token = default);
    /// <summary>Upsert Market Condition through the typed trade persistence boundary.</summary>
    Task UpsertMarketConditionAsync(MarketConditionReadModel result);
    /// <summary>Upsert Market Condition through the typed trade persistence boundary.</summary>
    Task UpsertMarketConditionAsync(MarketConditionReadModel result, CancellationToken cancellationToken);
    /// <summary>Upsert Regime Discovery through the typed trade persistence boundary.</summary>
    Task UpsertRegimeDiscoveryAsync(RegimeDiscoveryReadModel result);
    /// <summary>Upsert Regime Discovery through the typed trade persistence boundary.</summary>
    Task UpsertRegimeDiscoveryAsync(RegimeDiscoveryReadModel result, CancellationToken cancellationToken);
    /// <summary>Upsert Intrinsic Time Strategy Workflow through the typed trade persistence boundary.</summary>
    Task UpsertIntrinsicTimeStrategyWorkflowAsync(IntrinsicTimeStrategyWorkflowReadModel workflow);
    /// <summary>Upsert Intrinsic Time Strategy Workflow through the typed trade persistence boundary.</summary>
    Task UpsertIntrinsicTimeStrategyWorkflowAsync(IntrinsicTimeStrategyWorkflowReadModel workflow, CancellationToken cancellationToken);
    /// <summary>Upsert Active Intrinsic Time Strategy Workflow through the typed trade persistence boundary.</summary>
    Task UpsertActiveIntrinsicTimeStrategyWorkflowAsync(ActiveIntrinsicTimeStrategyWorkflowReadModel workflow);
    /// <summary>Upsert Active Intrinsic Time Strategy Workflow through the typed trade persistence boundary.</summary>
    Task UpsertActiveIntrinsicTimeStrategyWorkflowAsync(ActiveIntrinsicTimeStrategyWorkflowReadModel workflow, CancellationToken cancellationToken);
    /// <summary>Delete Active Intrinsic Time Strategy Workflow through the typed trade persistence boundary.</summary>
    Task DeleteActiveIntrinsicTimeStrategyWorkflowAsync(string workflowEntityId);
    /// <summary>Delete Active Intrinsic Time Strategy Workflow through the typed trade persistence boundary.</summary>
    Task DeleteActiveIntrinsicTimeStrategyWorkflowAsync(string workflowEntityId, CancellationToken cancellationToken);
    /// <summary>Insert Intrinsic Time Strategy Workflow Start Attempt through the typed trade persistence boundary.</summary>
    Task InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync(IntrinsicTimeStrategyWorkflowStartAttemptReadModel attempt);
    /// <summary>Insert Intrinsic Time Strategy Workflow Start Attempt through the typed trade persistence boundary.</summary>
    Task InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync(IntrinsicTimeStrategyWorkflowStartAttemptReadModel attempt, CancellationToken cancellationToken);
    /// <summary>Insert Intrinsic Time Strategy Workflow Timeline through the typed trade persistence boundary.</summary>
    Task InsertIntrinsicTimeStrategyWorkflowTimelineAsync(IntrinsicTimeStrategyWorkflowTimelineReadModel timelineEvent);
    /// <summary>Insert Intrinsic Time Strategy Workflow Timeline through the typed trade persistence boundary.</summary>
    Task InsertIntrinsicTimeStrategyWorkflowTimelineAsync(IntrinsicTimeStrategyWorkflowTimelineReadModel timelineEvent, CancellationToken cancellationToken);
    /// <summary>Upsert Intrinsic Time Strategy Workflow By Entity through the typed trade persistence boundary.</summary>
    Task UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(IntrinsicTimeStrategyWorkflowHistoryReadModel workflow);
    /// <summary>Upsert Intrinsic Time Strategy Workflow By Entity through the typed trade persistence boundary.</summary>
    Task UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(IntrinsicTimeStrategyWorkflowHistoryReadModel workflow, CancellationToken cancellationToken);
    /// <summary>Upsert Intrinsic Time Strategy Workflow By Status Day through the typed trade persistence boundary.</summary>
    Task UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(IntrinsicTimeStrategyWorkflowHistoryReadModel workflow);
    /// <summary>Upsert Intrinsic Time Strategy Workflow By Status Day through the typed trade persistence boundary.</summary>
    Task UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(IntrinsicTimeStrategyWorkflowHistoryReadModel workflow, CancellationToken cancellationToken);
    /// <summary>Delete Option Trade through the typed trade persistence boundary.</summary>
    Task DeleteOptionTradeAsync(int orderId, int tradeId);
    /// <summary>Delete Trade Limit through the typed trade persistence boundary.</summary>
    Task DeleteTradeLimitAsync(int tradeId, TradeType tradeType);
    /// <summary>Delete Trade Type Limit through the typed trade persistence boundary.</summary>
    Task DeleteTradeTypeLimitAsync(int tradeId);
    /// <summary>Delete Trade Live Feed through the typed trade persistence boundary.</summary>
    Task DeleteTradeLiveFeedAsync(int orderId, int tradeId);
    /// <summary>Delete Trade Live Feeds through the typed trade persistence boundary.</summary>
    Task DeleteTradeLiveFeedsAsync(int orderId);
    /// <summary>Delete Trade Plan Forward Loss Limit through the typed trade persistence boundary.</summary>
    Task DeleteTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitEntityId id);
    /// <summary>Delete Trade Position State through the typed trade persistence boundary.</summary>
    Task DeleteTradePositionStateAsync(OptionTradeEntityId id);
    /// <summary>Delete Trade Position through the typed trade persistence boundary.</summary>
    Task DeleteTradePositionAsync(int orderId, int tradeId);
    /// <summary>Delete Trade Position through the typed trade persistence boundary.</summary>
    Task DeleteTradePositionAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus);
    /// <summary>Delete Option Trade Spread Bar Data through the typed trade persistence boundary.</summary>
    Task DeleteOptionTradeSpreadBarDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType);
    /// <summary>Delete Option Trade Spread Data through the typed trade persistence boundary.</summary>
    Task DeleteOptionTradeSpreadDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType);
    /// <summary>Delete Trade Placement Signal through the typed trade persistence boundary.</summary>
    Task DeleteTradePlacementSignalAsync(string contractId, DateOnly valueDate);
    /// <summary>Insert Option Trade through the typed trade persistence boundary.</summary>
    Task InsertOptionTradeAsync(OptionTradeReadModel optionTrade);
    /// <summary>Insert Option Trades through the typed trade persistence boundary.</summary>
    Task InsertOptionTradesAsync(ICollection<OptionTradeReadModel> optionTrades);
    /// <summary>Insert Option Trades through the typed trade persistence boundary.</summary>
    Task<long> InsertOptionTradesAsync(IEnumerable<OptionTradeReadModel> optionTrades);
    /// <summary>Insert Trade Position through the typed trade persistence boundary.</summary>
    Task InsertTradePositionAsync(TradePositionReadModel tradePosition);
    /// <summary>Insert Trade Position through the typed trade persistence boundary.</summary>
    Task InsertTradePositionAsync(ICollection<TradePositionReadModel> tradePositions);
    /// <summary>Insert Trade Positions through the typed trade persistence boundary.</summary>
    Task InsertTradePositionsAsync(ICollection<TradePositionReadModel> tradePositions);
    /// <summary>Insert Trade Positions through the typed trade persistence boundary.</summary>
    Task<long> InsertTradePositionsAsync(IEnumerable<TradePositionReadModel> tradePositions);
    /// <summary>Insert Option Leg through the typed trade persistence boundary.</summary>
    Task InsertOptionLegAsync(OptionTradeLegReadModel optionLeg);
    /// <summary>Insert Option Legs through the typed trade persistence boundary.</summary>
    Task InsertOptionLegsAsync(ICollection<OptionTradeLegReadModel> optionLegs);
    /// <summary>Insert Option Legs through the typed trade persistence boundary.</summary>
    Task<long> InsertOptionLegsAsync(IEnumerable<OptionTradeLegReadModel> optionLegs);
    /// <summary>Insert Option Leg Data through the typed trade persistence boundary.</summary>
    Task InsertOptionLegDataAsync(OptionTradeLegDataReadModel optionLegData);
    /// <summary>Insert Option Leg Data through the typed trade persistence boundary.</summary>
    Task InsertOptionLegDataAsync(ICollection<OptionTradeLegDataReadModel> optionLegData);
    /// <summary>Insert Option Leg Data through the typed trade persistence boundary.</summary>
    Task<long> InsertOptionLegDataAsync(IEnumerable<OptionTradeLegDataReadModel> optionLegData);
    /// <summary>Insert Trade Limit through the typed trade persistence boundary.</summary>
    Task InsertTradeLimitAsync(TradeLimitReadModel tradeLimit);
    /// <summary>Insert Trade Limits through the typed trade persistence boundary.</summary>
    Task InsertTradeLimitsAsync(ICollection<TradeLimitReadModel> tradeLimits);
    /// <summary>Insert Trade Limits through the typed trade persistence boundary.</summary>
    Task<long> InsertTradeLimitsAsync(IEnumerable<TradeLimitReadModel> tradeLimits);
    /// <summary>Insert Trade Type Limit through the typed trade persistence boundary.</summary>
    Task InsertTradeTypeLimitAsync(TradeTypeLimitReadModel tradeTypeLimit);
    /// <summary>Insert Trade Type Limits through the typed trade persistence boundary.</summary>
    Task InsertTradeTypeLimitsAsync(ICollection<TradeTypeLimitReadModel> tradeTypeLimits);
    /// <summary>Insert Trade Type Limits through the typed trade persistence boundary.</summary>
    Task<long> InsertTradeTypeLimitsAsync(IEnumerable<TradeTypeLimitReadModel> tradeTypeLimits);
    /// <summary>Insert Trade Fill through the typed trade persistence boundary.</summary>
    Task InsertTradeFillAsync(ICollection<TradeFillReadModel> tradeFills);
    /// <summary>Insert Trade Fills through the typed trade persistence boundary.</summary>
    Task InsertTradeFillsAsync(ICollection<TradeFillReadModel> tradeFills);
    /// <summary>Insert Trade Fills through the typed trade persistence boundary.</summary>
    Task<long> InsertTradeFillsAsync(IEnumerable<TradeFillReadModel> tradeFills);
    /// <summary>Insert Trade Placement Signal through the typed trade persistence boundary.</summary>
    Task<long> InsertTradePlacementSignalAsync(TradePlacementSignalReadModel tradePlacementSignal);
    /// <summary>Insert Trade Plan through the typed trade persistence boundary.</summary>
    Task InsertTradePlanAsync(TradePlanReadModel tradePlan);
    /// <summary>Insert Trade Plans through the typed trade persistence boundary.</summary>
    Task InsertTradePlansAsync(ICollection<TradePlanByIdReadModel> tradePlans);
    /// <summary>Insert Trade Plans through the typed trade persistence boundary.</summary>
    Task<long> InsertTradePlansAsync(IEnumerable<TradePlanByIdReadModel> tradePlans);
    /// <summary>Insert Trade Plan Forward Loss Limit through the typed trade persistence boundary.</summary>
    Task InsertTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitReadModel tradePlanForwardLossLimit);
    /// <summary>Insert Trade Plan Forward Loss Ratio through the typed trade persistence boundary.</summary>
    Task InsertTradePlanForwardLossRatioAsync(DateOnly valueDate, double forwardLosRatio);
    /// <summary>Insert Trade Live Feed through the typed trade persistence boundary.</summary>
    Task InsertTradeLiveFeedAsync(TradeLiveFeedReadModel tradeLiveFeed);
    /// <summary>Insert Trade Position State through the typed trade persistence boundary.</summary>
    Task InsertTradePositionStateAsync(TradePositionStateReadModel tradePositionState);
    /// <summary>Insert Option Trade Spread Data through the typed trade persistence boundary.</summary>
    Task InsertOptionTradeSpreadDataAsync(OptionTradeSpreadsDataModel optionTradeSpreadData);
    /// <summary>Insert Option Trade Spread Data through the typed trade persistence boundary.</summary>
    Task InsertOptionTradeSpreadDataAsync(ICollection<OptionTradeSpreadsDataModel> optionTradeSpreadData);
    /// <summary>Insert Option Trade Spread Data through the typed trade persistence boundary.</summary>
    Task<long> InsertOptionTradeSpreadDataAsync(IEnumerable<OptionTradeSpreadsDataModel> optionTradeSpreadData);
    /// <summary>Insert Option Trade Spread Bar Data through the typed trade persistence boundary.</summary>
    Task InsertOptionTradeSpreadBarDataAsync(OptionTradeSpreadBarsDataModel optionTradeSpreadBarData);
    /// <summary>Insert Option Trade Spread Bar Data through the typed trade persistence boundary.</summary>
    Task InsertOptionTradeSpreadBarDataAsync(ICollection<OptionTradeSpreadBarsDataModel> optionTradeSpreadBarsData);
    /// <summary>Insert Option Trade Spread Bar Data through the typed trade persistence boundary.</summary>
    Task<long> InsertOptionTradeSpreadBarDataAsync(IEnumerable<OptionTradeSpreadBarsDataModel> optionTradeSpreadBarsData);
    /// <summary>Update Option Trade State through the typed trade persistence boundary.</summary>
    Task UpdateOptionTradeStateAsync(int orderId, int tradeId, TradeState tradeState, DateTime updatedOn, string updatedBy);
    /// <summary>Update Trade Position through the typed trade persistence boundary.</summary>
    Task UpdateTradePositionAsync(TradePositionEntityId key, decimal commission, int deltaHedge, decimal netSpread, decimal tradeValue, decimal tradePnl, decimal assetPrice, double otmProbability, double winRatio, decimal maxPrice, double hedgeProbability, double riskFreeRate, DateTime updatedOn, string updatedBy);
    /// <summary>Update Trade Position Status through the typed trade persistence boundary.</summary>
    Task UpdateTradePositionStatusAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus oldTradeStatus, TradeStatus newTradeStatus, DateTime updatedOn, string updatedBy);
    /// <summary>Update Option Leg Data through the typed trade persistence boundary.</summary>
    Task UpdateOptionLegDataAsync(OptionTradeLegDataReadModel optionLegData);
    /// <summary>Update Trade Limit Daily Profit Target through the typed trade persistence boundary.</summary>
    Task UpdateTradeLimitDailyProfitTarget(int tradeId, TradeType tradeType, decimal dailyProfitTarget, DateTime updatedOn, string updatedBy);
    /// <summary>Update Trade Live Feed through the typed trade persistence boundary.</summary>
    Task UpdateTradeLiveFeedAsync(TradeLiveFeedReadModel tradeLiveFeed);
}