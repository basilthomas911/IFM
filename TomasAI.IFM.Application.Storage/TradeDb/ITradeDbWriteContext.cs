using TomasAI.IFM.Domain.BrokerAccount.Query.Model;
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
public interface ITradeDbWriteContext : IEnlistedTradeDbWriter, IBrokerAccountProjectionWriter
{
    /// <summary>Upsert Order Composition through the typed trade persistence boundary.</summary>
    /// <param name="completed">The completed supplied by the caller.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpsertOrderCompositionAsync(OrderCompositionFunctionCompletedEvent completed, CancellationToken token = default);
    /// <summary>Upsert Market Condition Assessment through the typed trade persistence boundary.</summary>
    /// <param name="completed">The completed supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpsertMarketConditionAssessmentAsync(MarketConditionAssessmentCompletedEvent completed, CancellationToken cancellationToken = default);
    /// <summary>Upsert Trade Selection through the typed trade persistence boundary.</summary>
    /// <param name="completed">The completed supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpsertTradeSelectionAsync(TradeSelectionFunctionCompletedEvent completed, CancellationToken cancellationToken = default);
    /// <summary>Upsert Risk History through the typed trade persistence boundary.</summary>
    /// <param name="snapshot">The snapshot supplied by the caller.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<RiskHistoryProjectionResult> UpsertRiskHistoryAsync(WorkflowStrategyStateUpdatedEvent snapshot, CancellationToken token = default);
    /// <summary>Projects a whole-trade limit and its spread limits as one accepted configuration.</summary>
    /// <param name="limit">The maximum number of returned rows.</param>
    /// <param name="spreads">The spreads supplied by the caller.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task ProjectMonitoringLimitsAsync(TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeLimitReadModel limit, IReadOnlyList<TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeTypeLimitReadModel> spreads, CancellationToken token = default) => Task.WhenAll(InsertTradeLimitAsync(limit), InsertTradeTypeLimitsAsync((ICollection<TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeTypeLimitReadModel>)spreads.ToArray()));
    /// <summary>Upsert Trade Order through the typed trade persistence boundary.</summary>
    /// <param name="order">The order supplied by the caller.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpsertTradeOrderAsync(TradeOrderDefinition order, CancellationToken token = default);
    /// <summary>Projects an accepted order with the timestamp recorded in its source event.</summary>
    /// <param name="order">The order supplied by the caller.</param>
    /// <param name="acceptedAtUtc">The accepted at utc supplied by the caller.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpsertTradeOrderAsync(TradeOrderDefinition order, DateTime acceptedAtUtc, CancellationToken token = default) => UpsertTradeOrderAsync(order, token);
    /// <summary>Upsert Order Execution through the typed trade persistence boundary.</summary>
    /// <param name="execution">The execution supplied by the caller.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpsertOrderExecutionAsync(OrderExecutionDefinition execution, CancellationToken token = default);
    /// <summary>Upsert Established Trade through the typed trade persistence boundary.</summary>
    /// <param name="trade">The trade supplied by the caller.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpsertEstablishedTradeAsync(EstablishedTradeDefinition trade, CancellationToken token = default);
    /// <summary>Upsert Strategy Position through the typed trade persistence boundary.</summary>
    /// <param name="position">The position supplied by the caller.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpsertStrategyPositionAsync(StrategyPositionSnapshot position, CancellationToken token = default);
    /// <summary>Replace Open Position Routes through the typed trade persistence boundary.</summary>
    /// <param name="position">The position supplied by the caller.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task ReplaceOpenPositionRoutesAsync(StrategyPositionSnapshot position, CancellationToken token = default);
    /// <summary>Upsert Market Condition through the typed trade persistence boundary.</summary>
    /// <param name="result">The result supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpsertMarketConditionAsync(MarketConditionReadModel result);
    /// <summary>Upsert Market Condition through the typed trade persistence boundary.</summary>
    /// <param name="result">The result supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpsertMarketConditionAsync(MarketConditionReadModel result, CancellationToken cancellationToken);
    /// <summary>Upsert Regime Discovery through the typed trade persistence boundary.</summary>
    /// <param name="result">The result supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpsertRegimeDiscoveryAsync(RegimeDiscoveryReadModel result);
    /// <summary>Upsert Regime Discovery through the typed trade persistence boundary.</summary>
    /// <param name="result">The result supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpsertRegimeDiscoveryAsync(RegimeDiscoveryReadModel result, CancellationToken cancellationToken);
    /// <summary>Upsert Intrinsic Time Strategy Workflow through the typed trade persistence boundary.</summary>
    /// <param name="workflow">The workflow supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpsertIntrinsicTimeStrategyWorkflowAsync(IntrinsicTimeStrategyWorkflowReadModel workflow);
    /// <summary>Upsert Intrinsic Time Strategy Workflow through the typed trade persistence boundary.</summary>
    /// <param name="workflow">The workflow supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpsertIntrinsicTimeStrategyWorkflowAsync(IntrinsicTimeStrategyWorkflowReadModel workflow, CancellationToken cancellationToken);
    /// <summary>Upsert Active Intrinsic Time Strategy Workflow through the typed trade persistence boundary.</summary>
    /// <param name="workflow">The workflow supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpsertActiveIntrinsicTimeStrategyWorkflowAsync(ActiveIntrinsicTimeStrategyWorkflowReadModel workflow);
    /// <summary>Upsert Active Intrinsic Time Strategy Workflow through the typed trade persistence boundary.</summary>
    /// <param name="workflow">The workflow supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpsertActiveIntrinsicTimeStrategyWorkflowAsync(ActiveIntrinsicTimeStrategyWorkflowReadModel workflow, CancellationToken cancellationToken);
    /// <summary>Delete Active Intrinsic Time Strategy Workflow through the typed trade persistence boundary.</summary>
    /// <param name="workflowEntityId">The workflow entity id supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task DeleteActiveIntrinsicTimeStrategyWorkflowAsync(string workflowEntityId);
    /// <summary>Delete Active Intrinsic Time Strategy Workflow through the typed trade persistence boundary.</summary>
    /// <param name="workflowEntityId">The workflow entity id supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task DeleteActiveIntrinsicTimeStrategyWorkflowAsync(string workflowEntityId, CancellationToken cancellationToken);
    /// <summary>Insert Intrinsic Time Strategy Workflow Start Attempt through the typed trade persistence boundary.</summary>
    /// <param name="attempt">The attempt supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync(IntrinsicTimeStrategyWorkflowStartAttemptReadModel attempt);
    /// <summary>Insert Intrinsic Time Strategy Workflow Start Attempt through the typed trade persistence boundary.</summary>
    /// <param name="attempt">The attempt supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync(IntrinsicTimeStrategyWorkflowStartAttemptReadModel attempt, CancellationToken cancellationToken);
    /// <summary>Insert Intrinsic Time Strategy Workflow Timeline through the typed trade persistence boundary.</summary>
    /// <param name="timelineEvent">The timeline event supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertIntrinsicTimeStrategyWorkflowTimelineAsync(IntrinsicTimeStrategyWorkflowTimelineReadModel timelineEvent);
    /// <summary>Insert Intrinsic Time Strategy Workflow Timeline through the typed trade persistence boundary.</summary>
    /// <param name="timelineEvent">The timeline event supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertIntrinsicTimeStrategyWorkflowTimelineAsync(IntrinsicTimeStrategyWorkflowTimelineReadModel timelineEvent, CancellationToken cancellationToken);
    /// <summary>Upsert Intrinsic Time Strategy Workflow By Entity through the typed trade persistence boundary.</summary>
    /// <param name="workflow">The workflow supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(IntrinsicTimeStrategyWorkflowHistoryReadModel workflow);
    /// <summary>Upsert Intrinsic Time Strategy Workflow By Entity through the typed trade persistence boundary.</summary>
    /// <param name="workflow">The workflow supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(IntrinsicTimeStrategyWorkflowHistoryReadModel workflow, CancellationToken cancellationToken);
    /// <summary>Upsert Intrinsic Time Strategy Workflow By Status Day through the typed trade persistence boundary.</summary>
    /// <param name="workflow">The workflow supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(IntrinsicTimeStrategyWorkflowHistoryReadModel workflow);
    /// <summary>Upsert Intrinsic Time Strategy Workflow By Status Day through the typed trade persistence boundary.</summary>
    /// <param name="workflow">The workflow supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(IntrinsicTimeStrategyWorkflowHistoryReadModel workflow, CancellationToken cancellationToken);
    /// <summary>Delete Option Trade through the typed trade persistence boundary.</summary>
    /// <param name="orderId">The order id supplied by the caller.</param>
    /// <param name="tradeId">The trade id supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task DeleteOptionTradeAsync(int orderId, int tradeId);
    /// <summary>Delete Trade Limit through the typed trade persistence boundary.</summary>
    /// <param name="tradeId">The trade id supplied by the caller.</param>
    /// <param name="tradeType">The trade type supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task DeleteTradeLimitAsync(int tradeId, TradeType tradeType);
    /// <summary>Delete Trade Type Limit through the typed trade persistence boundary.</summary>
    /// <param name="tradeId">The trade id supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task DeleteTradeTypeLimitAsync(int tradeId);
    /// <summary>Delete Trade Live Feed through the typed trade persistence boundary.</summary>
    /// <param name="orderId">The order id supplied by the caller.</param>
    /// <param name="tradeId">The trade id supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task DeleteTradeLiveFeedAsync(int orderId, int tradeId);
    /// <summary>Delete Trade Live Feeds through the typed trade persistence boundary.</summary>
    /// <param name="orderId">The order id supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task DeleteTradeLiveFeedsAsync(int orderId);
    /// <summary>Delete Trade Plan Forward Loss Limit through the typed trade persistence boundary.</summary>
    /// <param name="id">The id supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task DeleteTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitEntityId id);
    /// <summary>Delete Trade Position State through the typed trade persistence boundary.</summary>
    /// <param name="id">The id supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task DeleteTradePositionStateAsync(OptionTradeEntityId id);
    /// <summary>Delete Trade Position through the typed trade persistence boundary.</summary>
    /// <param name="orderId">The order id supplied by the caller.</param>
    /// <param name="tradeId">The trade id supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task DeleteTradePositionAsync(int orderId, int tradeId);
    /// <summary>Delete Trade Position through the typed trade persistence boundary.</summary>
    /// <param name="orderId">The order id supplied by the caller.</param>
    /// <param name="tradeId">The trade id supplied by the caller.</param>
    /// <param name="tradeType">The trade type supplied by the caller.</param>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <param name="daysToExpiry">The days to expiry supplied by the caller.</param>
    /// <param name="tradeStatus">The trade status supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task DeleteTradePositionAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus);
    /// <summary>Delete Option Trade Spread Bar Data through the typed trade persistence boundary.</summary>
    /// <param name="orderId">The order id supplied by the caller.</param>
    /// <param name="tradeId">The trade id supplied by the caller.</param>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <param name="tradeType">The trade type supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task DeleteOptionTradeSpreadBarDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType);
    /// <summary>Delete Option Trade Spread Data through the typed trade persistence boundary.</summary>
    /// <param name="orderId">The order id supplied by the caller.</param>
    /// <param name="tradeId">The trade id supplied by the caller.</param>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <param name="tradeType">The trade type supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task DeleteOptionTradeSpreadDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType);
    /// <summary>Delete Trade Placement Signal through the typed trade persistence boundary.</summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task DeleteTradePlacementSignalAsync(string contractId, DateOnly valueDate);
    /// <summary>Insert Option Trade through the typed trade persistence boundary.</summary>
    /// <param name="optionTrade">The option trade supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertOptionTradeAsync(OptionTradeReadModel optionTrade);
    /// <summary>Insert Option Trades through the typed trade persistence boundary.</summary>
    /// <param name="optionTrades">The option trades supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertOptionTradesAsync(ICollection<OptionTradeReadModel> optionTrades);
    /// <summary>Insert Option Trades through the typed trade persistence boundary.</summary>
    /// <param name="optionTrades">The option trades supplied by the caller.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<long> InsertOptionTradesAsync(IEnumerable<OptionTradeReadModel> optionTrades);
    /// <summary>Insert Trade Position through the typed trade persistence boundary.</summary>
    /// <param name="tradePosition">The trade position supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertTradePositionAsync(TradePositionReadModel tradePosition);
    /// <summary>Insert Trade Position through the typed trade persistence boundary.</summary>
    /// <param name="tradePositions">The trade positions supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertTradePositionAsync(ICollection<TradePositionReadModel> tradePositions);
    /// <summary>Insert Trade Positions through the typed trade persistence boundary.</summary>
    /// <param name="tradePositions">The trade positions supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertTradePositionsAsync(ICollection<TradePositionReadModel> tradePositions);
    /// <summary>Insert Trade Positions through the typed trade persistence boundary.</summary>
    /// <param name="tradePositions">The trade positions supplied by the caller.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<long> InsertTradePositionsAsync(IEnumerable<TradePositionReadModel> tradePositions);
    /// <summary>Insert Option Leg through the typed trade persistence boundary.</summary>
    /// <param name="optionLeg">The option leg supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertOptionLegAsync(OptionTradeLegReadModel optionLeg);
    /// <summary>Insert Option Legs through the typed trade persistence boundary.</summary>
    /// <param name="optionLegs">The option legs supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertOptionLegsAsync(ICollection<OptionTradeLegReadModel> optionLegs);
    /// <summary>Insert Option Legs through the typed trade persistence boundary.</summary>
    /// <param name="optionLegs">The option legs supplied by the caller.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<long> InsertOptionLegsAsync(IEnumerable<OptionTradeLegReadModel> optionLegs);
    /// <summary>Insert Option Leg Data through the typed trade persistence boundary.</summary>
    /// <param name="optionLegData">The option leg data supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertOptionLegDataAsync(OptionTradeLegDataReadModel optionLegData);
    /// <summary>Insert Option Leg Data through the typed trade persistence boundary.</summary>
    /// <param name="optionLegData">The option leg data supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertOptionLegDataAsync(ICollection<OptionTradeLegDataReadModel> optionLegData);
    /// <summary>Insert Option Leg Data through the typed trade persistence boundary.</summary>
    /// <param name="optionLegData">The option leg data supplied by the caller.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<long> InsertOptionLegDataAsync(IEnumerable<OptionTradeLegDataReadModel> optionLegData);
    /// <summary>Insert Trade Limit through the typed trade persistence boundary.</summary>
    /// <param name="tradeLimit">The trade limit supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertTradeLimitAsync(TradeLimitReadModel tradeLimit);
    /// <summary>Insert Trade Limits through the typed trade persistence boundary.</summary>
    /// <param name="tradeLimits">The trade limits supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertTradeLimitsAsync(ICollection<TradeLimitReadModel> tradeLimits);
    /// <summary>Insert Trade Limits through the typed trade persistence boundary.</summary>
    /// <param name="tradeLimits">The trade limits supplied by the caller.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<long> InsertTradeLimitsAsync(IEnumerable<TradeLimitReadModel> tradeLimits);
    /// <summary>Insert Trade Type Limit through the typed trade persistence boundary.</summary>
    /// <param name="tradeTypeLimit">The trade type limit supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertTradeTypeLimitAsync(TradeTypeLimitReadModel tradeTypeLimit);
    /// <summary>Insert Trade Type Limits through the typed trade persistence boundary.</summary>
    /// <param name="tradeTypeLimits">The trade type limits supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertTradeTypeLimitsAsync(ICollection<TradeTypeLimitReadModel> tradeTypeLimits);
    /// <summary>Insert Trade Type Limits through the typed trade persistence boundary.</summary>
    /// <param name="tradeTypeLimits">The trade type limits supplied by the caller.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<long> InsertTradeTypeLimitsAsync(IEnumerable<TradeTypeLimitReadModel> tradeTypeLimits);
    /// <summary>Insert Trade Fill through the typed trade persistence boundary.</summary>
    /// <param name="tradeFills">The trade fills supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertTradeFillAsync(ICollection<TradeFillReadModel> tradeFills);
    /// <summary>Insert Trade Fills through the typed trade persistence boundary.</summary>
    /// <param name="tradeFills">The trade fills supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertTradeFillsAsync(ICollection<TradeFillReadModel> tradeFills);
    /// <summary>Insert Trade Fills through the typed trade persistence boundary.</summary>
    /// <param name="tradeFills">The trade fills supplied by the caller.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<long> InsertTradeFillsAsync(IEnumerable<TradeFillReadModel> tradeFills);
    /// <summary>Insert Trade Placement Signal through the typed trade persistence boundary.</summary>
    /// <param name="tradePlacementSignal">The trade placement signal supplied by the caller.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<long> InsertTradePlacementSignalAsync(TradePlacementSignalReadModel tradePlacementSignal);
    /// <summary>Insert Trade Plan through the typed trade persistence boundary.</summary>
    /// <param name="tradePlan">The trade plan supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertTradePlanAsync(TradePlanReadModel tradePlan);
    /// <summary>Insert Trade Plans through the typed trade persistence boundary.</summary>
    /// <param name="tradePlans">The trade plans supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertTradePlansAsync(ICollection<TradePlanByIdReadModel> tradePlans);
    /// <summary>Insert Trade Plans through the typed trade persistence boundary.</summary>
    /// <param name="tradePlans">The trade plans supplied by the caller.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<long> InsertTradePlansAsync(IEnumerable<TradePlanByIdReadModel> tradePlans);
    /// <summary>Insert Trade Plan Forward Loss Limit through the typed trade persistence boundary.</summary>
    /// <param name="tradePlanForwardLossLimit">The trade plan forward loss limit supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitReadModel tradePlanForwardLossLimit);
    /// <summary>Insert Trade Plan Forward Loss Ratio through the typed trade persistence boundary.</summary>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <param name="forwardLosRatio">The forward los ratio supplied by the caller.</param>
    /// <param name="sequenceId">The sequence assigned by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertTradePlanForwardLossRatioAsync(DateOnly valueDate, double forwardLosRatio, long sequenceId);
    /// <summary>Insert Trade Live Feed through the typed trade persistence boundary.</summary>
    /// <param name="tradeLiveFeed">The trade live feed supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertTradeLiveFeedAsync(TradeLiveFeedReadModel tradeLiveFeed);
    /// <summary>Insert Trade Position State through the typed trade persistence boundary.</summary>
    /// <param name="tradePositionState">The trade position state supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertTradePositionStateAsync(TradePositionStateReadModel tradePositionState);
    /// <summary>Insert Option Trade Spread Data through the typed trade persistence boundary.</summary>
    /// <param name="optionTradeSpreadData">The option trade spread data supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertOptionTradeSpreadDataAsync(OptionTradeSpreadsDataModel optionTradeSpreadData);
    /// <summary>Insert Option Trade Spread Data through the typed trade persistence boundary.</summary>
    /// <param name="optionTradeSpreadData">The option trade spread data supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertOptionTradeSpreadDataAsync(ICollection<OptionTradeSpreadsDataModel> optionTradeSpreadData);
    /// <summary>Insert Option Trade Spread Data through the typed trade persistence boundary.</summary>
    /// <param name="optionTradeSpreadData">The option trade spread data supplied by the caller.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<long> InsertOptionTradeSpreadDataAsync(IEnumerable<OptionTradeSpreadsDataModel> optionTradeSpreadData);
    /// <summary>Insert Option Trade Spread Bar Data through the typed trade persistence boundary.</summary>
    /// <param name="optionTradeSpreadBarData">The option trade spread bar data supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertOptionTradeSpreadBarDataAsync(OptionTradeSpreadBarsDataModel optionTradeSpreadBarData);
    /// <summary>Insert Option Trade Spread Bar Data through the typed trade persistence boundary.</summary>
    /// <param name="optionTradeSpreadBarsData">The option trade spread bars data supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertOptionTradeSpreadBarDataAsync(ICollection<OptionTradeSpreadBarsDataModel> optionTradeSpreadBarsData);
    /// <summary>Insert Option Trade Spread Bar Data through the typed trade persistence boundary.</summary>
    /// <param name="optionTradeSpreadBarsData">The option trade spread bars data supplied by the caller.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<long> InsertOptionTradeSpreadBarDataAsync(IEnumerable<OptionTradeSpreadBarsDataModel> optionTradeSpreadBarsData);
    /// <summary>Update Option Trade State through the typed trade persistence boundary.</summary>
    /// <param name="orderId">The order id supplied by the caller.</param>
    /// <param name="tradeId">The trade id supplied by the caller.</param>
    /// <param name="tradeState">The trade state supplied by the caller.</param>
    /// <param name="updatedOn">The updated on supplied by the caller.</param>
    /// <param name="updatedBy">The updated by supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpdateOptionTradeStateAsync(int orderId, int tradeId, TradeState tradeState, DateTime updatedOn, string updatedBy);
    /// <summary>Update Trade Position through the typed trade persistence boundary.</summary>
    /// <param name="key">The key supplied by the caller.</param>
    /// <param name="commission">The commission supplied by the caller.</param>
    /// <param name="deltaHedge">The delta hedge supplied by the caller.</param>
    /// <param name="netSpread">The net spread supplied by the caller.</param>
    /// <param name="tradeValue">The trade value supplied by the caller.</param>
    /// <param name="tradePnl">The trade pnl supplied by the caller.</param>
    /// <param name="assetPrice">The asset price supplied by the caller.</param>
    /// <param name="otmProbability">The otm probability supplied by the caller.</param>
    /// <param name="winRatio">The win ratio supplied by the caller.</param>
    /// <param name="maxPrice">The max price supplied by the caller.</param>
    /// <param name="hedgeProbability">The hedge probability supplied by the caller.</param>
    /// <param name="riskFreeRate">The risk free rate supplied by the caller.</param>
    /// <param name="updatedOn">The updated on supplied by the caller.</param>
    /// <param name="updatedBy">The updated by supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpdateTradePositionAsync(TradePositionEntityId key, decimal commission, int deltaHedge, decimal netSpread, decimal tradeValue, decimal tradePnl, decimal assetPrice, double otmProbability, double winRatio, decimal maxPrice, double hedgeProbability, double riskFreeRate, DateTime updatedOn, string updatedBy);
    /// <summary>Update Trade Position Status through the typed trade persistence boundary.</summary>
    /// <param name="orderId">The order id supplied by the caller.</param>
    /// <param name="tradeId">The trade id supplied by the caller.</param>
    /// <param name="tradeType">The trade type supplied by the caller.</param>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <param name="daysToExpiry">The days to expiry supplied by the caller.</param>
    /// <param name="oldTradeStatus">The old trade status supplied by the caller.</param>
    /// <param name="newTradeStatus">The new trade status supplied by the caller.</param>
    /// <param name="updatedOn">The updated on supplied by the caller.</param>
    /// <param name="updatedBy">The updated by supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpdateTradePositionStatusAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus oldTradeStatus, TradeStatus newTradeStatus, DateTime updatedOn, string updatedBy);
    /// <summary>Update Option Leg Data through the typed trade persistence boundary.</summary>
    /// <param name="optionLegData">The option leg data supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpdateOptionLegDataAsync(OptionTradeLegDataReadModel optionLegData);
    /// <summary>Update Trade Limit Daily Profit Target through the typed trade persistence boundary.</summary>
    /// <param name="tradeId">The trade id supplied by the caller.</param>
    /// <param name="tradeType">The trade type supplied by the caller.</param>
    /// <param name="dailyProfitTarget">The daily profit target supplied by the caller.</param>
    /// <param name="updatedOn">The updated on supplied by the caller.</param>
    /// <param name="updatedBy">The updated by supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpdateTradeLimitDailyProfitTarget(int tradeId, TradeType tradeType, decimal dailyProfitTarget, DateTime updatedOn, string updatedBy);
    /// <summary>Update Trade Live Feed through the typed trade persistence boundary.</summary>
    /// <param name="tradeLiveFeed">The trade live feed supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpdateTradeLiveFeedAsync(TradeLiveFeedReadModel tradeLiveFeed);
}
