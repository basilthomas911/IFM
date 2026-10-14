using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;


using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Keep the published context interface and named argument contract; inherited capability members intentionally describe the same database operation.")]
public interface ITradeDbReadContext
{
    /// <summary>Get Order Composition Invocation through the typed trade persistence boundary.</summary>
    Task<OrderCompositionFunctionCompletedEvent?> GetOrderCompositionInvocationAsync(StrategyWorkflowId workflowId, Guid invocationId, CancellationToken token = default);
    /// <summary>Get Order Composition History through the typed trade persistence boundary.</summary>
    Task<QueryPage<OrderCompositionHistoryRow>> GetOrderCompositionHistoryAsync(int portfolioId, int fundId, DateOnly valueDate, int pageSize, byte[]? pagingState = null, CancellationToken token = default);
    /// <summary>Get Market Condition Assessment through the typed trade persistence boundary.</summary>
    Task<MarketConditionAssessmentCompletedEvent?> GetMarketConditionAssessmentAsync(StrategyWorkflowId workflowId, CancellationToken cancellationToken = default);
    /// <summary>Get Market Condition Assessment History through the typed trade persistence boundary.</summary>
    Task<ICollection<MarketConditionAssessmentCompletedEvent>> GetMarketConditionAssessmentHistoryAsync(string profile, string root, TimeFrameType horizon, DateTime beforeUtc, int pageSize, CancellationToken cancellationToken = default);
    /// <summary>Get Trade Selection Invocation through the typed trade persistence boundary.</summary>
    Task<TradeSelectionFunctionCompletedEvent?> GetTradeSelectionInvocationAsync(StrategyWorkflowId workflowId, Guid invocationId, CancellationToken cancellationToken = default);
    /// <summary>Get Trade Selection History through the typed trade persistence boundary.</summary>
    Task<QueryPage<TradeSelectionHistoryRow>> GetTradeSelectionHistoryAsync(int portfolioId, int fundId, DateOnly valueDate, int pageSize, byte[]? pagingState = null, CancellationToken cancellationToken = default);
    /// <summary>Get Risk Invocation through the typed trade persistence boundary.</summary>
    Task<WorkflowStrategyStateUpdatedEvent?> GetRiskInvocationAsync(Guid workflow, Guid invocation, CancellationToken token = default);
    /// <summary>Get Risk History through the typed trade persistence boundary.</summary>
    Task<QueryPage<RiskHistoryRow>> GetRiskHistoryAsync(int portfolio, int fund, DateOnly date, int size, byte[]? cursor, CancellationToken token = default);
    /// <summary>Get Trade Order through the typed trade persistence boundary.</summary>
    Task<TradeOrderDefinition?> GetTradeOrderAsync(TradeOrderId id, CancellationToken token = default);
    /// <summary>Get Order Execution through the typed trade persistence boundary.</summary>
    Task<OrderExecutionDefinition?> GetOrderExecutionAsync(TradeOrderId id, Guid executionAttemptId, CancellationToken token = default);
    /// <summary>Get Established Trade through the typed trade persistence boundary.</summary>
    Task<EstablishedTradeDefinition?> GetEstablishedTradeAsync(TradeEntityId id, CancellationToken token = default);
    /// <summary>Get Established Trades through the typed trade persistence boundary.</summary>
    Task<QueryPage<EstablishedTradeDefinition>> GetEstablishedTradesAsync(int portfolioId, int fundId, TradeStrategyKind strategyKind, DateTime fromUtc, DateTime toUtc, int pageSize, byte[]? pagingState = null, CancellationToken token = default);
    /// <summary>Get Strategy Position through the typed trade persistence boundary.</summary>
    Task<StrategyPositionSnapshot?> GetStrategyPositionAsync(StrategyPositionId id, CancellationToken token = default);
    /// <summary>Get Strategy Position History through the typed trade persistence boundary.</summary>
    Task<QueryPage<StrategyPositionSnapshot>> GetStrategyPositionHistoryAsync(Guid positionId, DateTime fromUtc, DateTime toUtc, int pageSize, byte[]? pagingState = null, CancellationToken token = default);
    /// <summary>Get Open Position Routes through the typed trade persistence boundary.</summary>
    Task<ICollection<OpenPositionRouteReadModel>> GetOpenPositionRoutesAsync(string contractId, CancellationToken token = default);
    /// <summary>Get Open Position Route Snapshot through the typed trade persistence boundary.</summary>
    Task<ICollection<OpenPositionRouteReadModel>> GetOpenPositionRouteSnapshotAsync(CancellationToken token = default);
    /// <summary>Reads one bounded recovery-route page for dated session finalization.</summary>
    Task<QueryPage<OpenPositionRouteReadModel>> GetOpenPositionRoutePageAsync(int pageSize, byte[]? pagingState = null, CancellationToken token = default);
    /// <summary>Get Market Condition through the typed trade persistence boundary.</summary>
    Task<MarketConditionReadModel?> GetMarketConditionAsync(StrategyWorkflowId workflowId);
    /// <summary>Get Market Condition through the typed trade persistence boundary.</summary>
    Task<MarketConditionReadModel?> GetMarketConditionAsync(StrategyWorkflowId workflowId, CancellationToken cancellationToken);
    /// <summary>Get Market Condition History through the typed trade persistence boundary.</summary>
    Task<ICollection<MarketConditionReadModel>> GetMarketConditionHistoryAsync(int fundId, string instrumentRoot, TimeFrameType targetHorizon, DateTime beforeUtc, int pageSize);
    /// <summary>Get Market Condition History through the typed trade persistence boundary.</summary>
    Task<ICollection<MarketConditionReadModel>> GetMarketConditionHistoryAsync(int fundId, string instrumentRoot, TimeFrameType targetHorizon, DateTime beforeUtc, int pageSize, CancellationToken cancellationToken);
    /// <summary>Get Regime Discovery through the typed trade persistence boundary.</summary>
    Task<RegimeDiscoveryReadModel?> GetRegimeDiscoveryAsync(StrategyWorkflowId workflowId);
    /// <summary>Get Regime Discovery through the typed trade persistence boundary.</summary>
    Task<RegimeDiscoveryReadModel?> GetRegimeDiscoveryAsync(StrategyWorkflowId workflowId, CancellationToken cancellationToken);
    /// <summary>Get Intrinsic Time Strategy Workflow through the typed trade persistence boundary.</summary>
    Task<IntrinsicTimeStrategyWorkflowReadModel?> GetIntrinsicTimeStrategyWorkflowAsync(StrategyWorkflowId workflowId);
    /// <summary>Get Intrinsic Time Strategy Workflow through the typed trade persistence boundary.</summary>
    Task<IntrinsicTimeStrategyWorkflowReadModel?> GetIntrinsicTimeStrategyWorkflowAsync(StrategyWorkflowId workflowId, CancellationToken cancellationToken);
    /// <summary>Get Active Intrinsic Time Strategy Workflow through the typed trade persistence boundary.</summary>
    Task<ActiveIntrinsicTimeStrategyWorkflowReadModel?> GetActiveIntrinsicTimeStrategyWorkflowAsync(string workflowEntityId);
    /// <summary>Get Active Intrinsic Time Strategy Workflow through the typed trade persistence boundary.</summary>
    Task<ActiveIntrinsicTimeStrategyWorkflowReadModel?> GetActiveIntrinsicTimeStrategyWorkflowAsync(string workflowEntityId, CancellationToken cancellationToken);
    /// <summary>Get Intrinsic Time Strategy Workflow Start Attempts through the typed trade persistence boundary.</summary>
    Task<ICollection<IntrinsicTimeStrategyWorkflowStartAttemptReadModel>> GetIntrinsicTimeStrategyWorkflowStartAttemptsAsync(string workflowEntityId, DateTime beforeUtc, int pageSize);
    /// <summary>Get Intrinsic Time Strategy Workflow Start Attempts through the typed trade persistence boundary.</summary>
    Task<ICollection<IntrinsicTimeStrategyWorkflowStartAttemptReadModel>> GetIntrinsicTimeStrategyWorkflowStartAttemptsAsync(string workflowEntityId, DateTime beforeUtc, int pageSize, CancellationToken cancellationToken);
    /// <summary>Get Intrinsic Time Strategy Workflow Timeline through the typed trade persistence boundary.</summary>
    Task<ICollection<IntrinsicTimeStrategyWorkflowTimelineReadModel>> GetIntrinsicTimeStrategyWorkflowTimelineAsync(StrategyWorkflowId workflowId, long afterEventId, int pageSize);
    /// <summary>Get Intrinsic Time Strategy Workflow Timeline through the typed trade persistence boundary.</summary>
    Task<ICollection<IntrinsicTimeStrategyWorkflowTimelineReadModel>> GetIntrinsicTimeStrategyWorkflowTimelineAsync(StrategyWorkflowId workflowId, long afterEventId, int pageSize, CancellationToken cancellationToken);
    /// <summary>Get Intrinsic Time Strategy Workflows By Entity through the typed trade persistence boundary.</summary>
    Task<ICollection<IntrinsicTimeStrategyWorkflowHistoryReadModel>> GetIntrinsicTimeStrategyWorkflowsByEntityAsync(string workflowEntityId, DateTime beforeUtc, int pageSize);
    /// <summary>Get Intrinsic Time Strategy Workflows By Entity through the typed trade persistence boundary.</summary>
    Task<ICollection<IntrinsicTimeStrategyWorkflowHistoryReadModel>> GetIntrinsicTimeStrategyWorkflowsByEntityAsync(string workflowEntityId, DateTime beforeUtc, int pageSize, CancellationToken cancellationToken);
    /// <summary>Get Intrinsic Time Strategy Workflows By Status through the typed trade persistence boundary.</summary>
    Task<ICollection<IntrinsicTimeStrategyWorkflowHistoryReadModel>> GetIntrinsicTimeStrategyWorkflowsByStatusAsync(StrategyWorkflowStatus status, DateOnly startDate, DateOnly endDate, int pageSize);
    /// <summary>Get Intrinsic Time Strategy Workflows By Status through the typed trade persistence boundary.</summary>
    Task<ICollection<IntrinsicTimeStrategyWorkflowHistoryReadModel>> GetIntrinsicTimeStrategyWorkflowsByStatusAsync(StrategyWorkflowStatus status, DateOnly startDate, DateOnly endDate, int pageSize, CancellationToken cancellationToken);
    /// <summary>Get Option Trade through the typed trade persistence boundary.</summary>
    Task<OptionTradeReadModel?> GetOptionTradeAsync(int orderId, int tradeId);
    /// <summary>Get Option Trade through the typed trade persistence boundary.</summary>
    Task<OptionTradeReadModel?> GetOptionTradeAsync(int orderId, int tradeId, CancellationToken cancellationToken);
    /// <summary>Get Option Trade Spread Data through the typed trade persistence boundary.</summary>
    Task<OptionTradeSpreadsDataModel?> GetOptionTradeSpreadDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType);
    /// <summary>Get Option Trade Spread Data through the typed trade persistence boundary.</summary>
    Task<OptionTradeSpreadsDataModel?> GetOptionTradeSpreadDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType, CancellationToken cancellationToken);
    /// <summary>Get Option Trade Spread Data through the typed trade persistence boundary.</summary>
    Task<ICollection<OptionTradeSpreadsDataModel>> GetOptionTradeSpreadDataAsync();
    /// <summary>Get Option Trade Spread Bar Data through the typed trade persistence boundary.</summary>
    Task<ICollection<OptionTradeSpreadBarsDataModel>> GetOptionTradeSpreadBarDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType, DateTime startDate, DateTime endDate);
    /// <summary>Get Option Trade Spread Bar Data through the typed trade persistence boundary.</summary>
    Task<ICollection<OptionTradeSpreadBarsDataModel>> GetOptionTradeSpreadBarDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType, DateTime startDate, DateTime endDate, CancellationToken cancellationToken);
    /// <summary>Get Option Trade Spread Bar Data through the typed trade persistence boundary.</summary>
    Task<ICollection<OptionTradeSpreadBarsDataModel>> GetOptionTradeSpreadBarDataAsync();
    /// <summary>Get Iron Condor Trade Price through the typed trade persistence boundary.</summary>
    Task<TradePriceReadModel?> GetIronCondorTradePriceAsync(int tradeId, DateOnly valueDate);
    /// <summary>Get Iron Condor Trade Price through the typed trade persistence boundary.</summary>
    Task<TradePriceReadModel?> GetIronCondorTradePriceAsync(int tradeId, DateOnly valueDate, CancellationToken cancellationToken);
    /// <summary>Get Option Trades through the typed trade persistence boundary.</summary>
    Task<ICollection<OptionTradeReadModel>> GetOptionTradesAsync(int orderId);
    /// <summary>Get Option Trades through the typed trade persistence boundary.</summary>
    Task<ICollection<OptionTradeReadModel>> GetOptionTradesAsync(int orderId, CancellationToken cancellationToken);
    /// <summary>Get Option Trades through the typed trade persistence boundary.</summary>
    Task<ICollection<OptionTradeReadModel>> GetOptionTradesAsync();
    /// <summary>Get Option Legs through the typed trade persistence boundary.</summary>
    Task<ICollection<OptionTradeLegReadModel>> GetOptionLegsAsync();
    /// <summary>Get Option Leg Data through the typed trade persistence boundary.</summary>
    Task<ICollection<OptionTradeLegDataReadModel>> GetOptionLegDataAsync();
    /// <summary>Get Trade Position through the typed trade persistence boundary.</summary>
    Task<TradePositionReadModel?> GetTradePositionAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus);
    /// <summary>Get Trade Position through the typed trade persistence boundary.</summary>
    Task<TradePositionReadModel?> GetTradePositionAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus, CancellationToken cancellationToken);
    /// <summary>Get Trade Positions through the typed trade persistence boundary.</summary>
    Task<ICollection<TradePositionReadModel>> GetTradePositionsAsync(int orderId, int tradeId);
    /// <summary>Get Trade Positions through the typed trade persistence boundary.</summary>
    Task<ICollection<TradePositionReadModel>> GetTradePositionsAsync(int orderId, int tradeId, CancellationToken cancellationToken);
    /// <summary>Get Trade Positions through the typed trade persistence boundary.</summary>
    Task<ICollection<TradePositionReadModel>> GetTradePositionsAsync();
    /// <summary>Get Trade History through the typed trade persistence boundary.</summary>
    Task<ICollection<TradeHistoryReadModel>> GetTradeHistoryAsync(int orderId);
    /// <summary>Get Trade History through the typed trade persistence boundary.</summary>
    Task<ICollection<TradeHistoryReadModel>> GetTradeHistoryAsync(int orderId, CancellationToken cancellationToken);
    /// <summary>Get Option Leg Contract Ids through the typed trade persistence boundary.</summary>
    Task<ICollection<string>> GetOptionLegContractIdsAsync(int tradeId);
    /// <summary>Get Option Leg Contract Ids through the typed trade persistence boundary.</summary>
    Task<ICollection<string>> GetOptionLegContractIdsAsync(int tradeId, CancellationToken cancellationToken);
    /// <summary>Get Trade Quantity through the typed trade persistence boundary.</summary>
    Task<int> GetTradeQuantityAsync(int tradeId);
    /// <summary>Get Trade Quantity through the typed trade persistence boundary.</summary>
    Task<int> GetTradeQuantityAsync(int tradeId, CancellationToken cancellationToken);
    /// <summary>Get Trade Limit through the typed trade persistence boundary.</summary>
    Task<TradeLimitReadModel?> GetTradeLimitAsync(int tradeId);
    /// <summary>Get Trade Limit through the typed trade persistence boundary.</summary>
    Task<TradeLimitReadModel?> GetTradeLimitAsync(int tradeId, CancellationToken cancellationToken);
    /// <summary>Get Trade Limits through the typed trade persistence boundary.</summary>
    Task<ICollection<TradeLimitReadModel>> GetTradeLimitsAsync();
    /// <summary>Get Trade Plan Stop Loss Limit through the typed trade persistence boundary.</summary>
    Task<TradePlanStopLossLimitReadModel?> GetTradePlanStopLossLimitAsync(int orderId, int tradeId);
    /// <summary>Get Trade Type Limit through the typed trade persistence boundary.</summary>
    Task<TradeTypeLimitReadModel?> GetTradeTypeLimitAsync(int tradeId, TradeType tradeType);
    /// <summary>Get Trade Type Limit through the typed trade persistence boundary.</summary>
    Task<TradeTypeLimitReadModel?> GetTradeTypeLimitAsync(int tradeId, TradeType tradeType, CancellationToken cancellationToken);
    /// <summary>Get Trade Type Limits through the typed trade persistence boundary.</summary>
    Task<ICollection<TradeTypeLimitReadModel>> GetTradeTypeLimitsAsync(int tradeId);
    /// <summary>Get Trade Type Limits through the typed trade persistence boundary.</summary>
    Task<ICollection<TradeTypeLimitReadModel>> GetTradeTypeLimitsAsync();
    /// <summary>Get Trade Fills through the typed trade persistence boundary.</summary>
    Task<ICollection<TradeFillReadModel>> GetTradeFillsAsync(int orderId, int tradeId);
    /// <summary>Get Trade Fills through the typed trade persistence boundary.</summary>
    Task<ICollection<TradeFillReadModel>> GetTradeFillsAsync(int orderId, int tradeId, CancellationToken cancellationToken);
    /// <summary>Get Trade Fills through the typed trade persistence boundary.</summary>
    Task<ICollection<TradeFillReadModel>> GetTradeFillsAsync();
    /// <summary>Get Trade Plans through the typed trade persistence boundary.</summary>
    Task<ICollection<TradePlanReadModel>> GetTradePlansAsync();
    /// <summary>Get Trade Plans through the typed trade persistence boundary.</summary>
    Task<ICollection<TradePlanReadModel>> GetTradePlansAsync(int orderId);
    /// <summary>Get Trade Plans through the typed trade persistence boundary.</summary>
    Task<ICollection<TradePlanReadModel>> GetTradePlansAsync(int orderId, int tradeId, DateOnly valueDate);
    /// <summary>Get Trade Plans through the typed trade persistence boundary.</summary>
    Task<ICollection<TradePlanReadModel>> GetTradePlansAsync(int orderId, int tradeId, DateOnly startDate, DateOnly endDate);
    /// <summary>Get Last Trade Plans through the typed trade persistence boundary.</summary>
    Task<ICollection<TradePlanReadModel>> GetLastTradePlansAsync(int orderId, int tradeId);
    /// <summary>Get Trade Plan Forward Loss Ratios through the typed trade persistence boundary.</summary>
    Task<ICollection<TradePlanForwardLossRatioReadModel>> GetTradePlanForwardLossRatiosAsync(DateOnly startDate, DateOnly endDate);
    /// <summary>Get Trade Plan Forward Loss Ratio through the typed trade persistence boundary.</summary>
    Task<TradePlanForwardLossRatioReadModel?> GetTradePlanForwardLossRatioAsync(DateOnly valueDate);
    /// <summary>Get Trade Fill Data through the typed trade persistence boundary.</summary>
    Task<ICollection<TradeFillDataReadModel>> GetTradeFillDataAsync(int tradeId);
    /// <summary>Get Trade Live Feed through the typed trade persistence boundary.</summary>
    Task<ICollection<TradeLiveFeedReadModel>> GetTradeLiveFeedAsync(int orderId, int tradeId);
    /// <summary>Get Trade Position Trade Types through the typed trade persistence boundary.</summary>
    Task<ICollection<string>> GetTradePositionTradeTypesAsync(int orderId, int tradeId, DateOnly valueDate, TradeStatus tradeStatus, int daysToExpiry);
    /// <summary>Get Trade Position Trade Types through the typed trade persistence boundary.</summary>
    Task<ICollection<string>> GetTradePositionTradeTypesAsync(int orderId, int tradeId, DateOnly valueDate, TradeStatus tradeStatus, int daysToExpiry, CancellationToken cancellationToken);
    /// <summary>Get Trade Plan Forward Loss Limit through the typed trade persistence boundary.</summary>
    Task<TradePlanForwardLossLimitReadModel?> GetTradePlanForwardLossLimitAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType);
    /// <summary>Get Trade Placement Signal through the typed trade persistence boundary.</summary>
    Task<TradePlacementSignalReadModel?> GetTradePlacementSignalAsync(string contractId, DateOnly valueDate);
}