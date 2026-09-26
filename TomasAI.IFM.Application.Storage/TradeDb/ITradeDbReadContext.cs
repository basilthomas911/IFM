using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
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

public interface ITradeDbReadContext
{
    Task<OrderCompositionFunctionCompletedEvent?> GetOrderCompositionInvocationAsync(StrategyWorkflowId workflowId, Guid invocationId, CancellationToken token = default);
    Task<QueryPage<OrderCompositionHistoryRow>> GetOrderCompositionHistoryAsync(int portfolioId, int fundId, DateOnly valueDate, int pageSize, byte[]? pagingState = null, CancellationToken token = default);
    Task<MarketConditionAssessmentCompletedEvent?> GetMarketConditionAssessmentAsync(StrategyWorkflowId workflowId, CancellationToken cancellationToken = default);
    Task<ICollection<MarketConditionAssessmentCompletedEvent>> GetMarketConditionAssessmentHistoryAsync(string profile, string root, TimeFrameType horizon, DateTime beforeUtc, int pageSize, CancellationToken cancellationToken = default);
    Task<TradeSelectionFunctionCompletedEvent?> GetTradeSelectionInvocationAsync(StrategyWorkflowId workflowId, Guid invocationId, CancellationToken cancellationToken = default);
    Task<QueryPage<TradeSelectionHistoryRow>> GetTradeSelectionHistoryAsync(int portfolioId, int fundId, DateOnly valueDate, int pageSize, byte[]? pagingState = null, CancellationToken cancellationToken = default);
    Task<WorkflowStrategyStateUpdatedEvent?> GetRiskInvocationAsync(Guid workflow, Guid invocation, CancellationToken token = default);
    Task<QueryPage<RiskHistoryRow>> GetRiskHistoryAsync(int portfolio, int fund, DateOnly date, int size, byte[]? cursor, CancellationToken token = default);
    Task<TradeOrderDefinition?> GetTradeOrderAsync(TradeOrderId id, CancellationToken token = default);
    Task<OrderExecutionDefinition?> GetOrderExecutionAsync(TradeOrderId id, Guid executionAttemptId, CancellationToken token = default);
    Task<EstablishedTradeDefinition?> GetEstablishedTradeAsync(TradeEntityId id, CancellationToken token = default);
    Task<QueryPage<EstablishedTradeDefinition>> GetEstablishedTradesAsync(int portfolioId, int fundId, TradeStrategyKind strategyKind, DateTime fromUtc, DateTime toUtc, int pageSize, byte[]? pagingState = null, CancellationToken token = default);
    Task<StrategyPositionSnapshot?> GetStrategyPositionAsync(StrategyPositionId id, CancellationToken token = default);
    Task<QueryPage<StrategyPositionSnapshot>> GetStrategyPositionHistoryAsync(Guid positionId, DateTime fromUtc, DateTime toUtc, int pageSize, byte[]? pagingState = null, CancellationToken token = default);
    Task<ICollection<OpenPositionRouteReadModel>> GetOpenPositionRoutesAsync(string contractId, CancellationToken token = default);
    Task<ICollection<OpenPositionRouteReadModel>> GetOpenPositionRouteSnapshotAsync(CancellationToken token = default);
    Task<MarketConditionReadModel?> GetMarketConditionAsync(StrategyWorkflowId workflowId);
    Task<MarketConditionReadModel?> GetMarketConditionAsync(StrategyWorkflowId workflowId, CancellationToken cancellationToken);
    Task<ICollection<MarketConditionReadModel>> GetMarketConditionHistoryAsync(int fundId, string instrumentRoot, TimeFrameType targetHorizon, DateTime beforeUtc, int pageSize);
    Task<ICollection<MarketConditionReadModel>> GetMarketConditionHistoryAsync(int fundId, string instrumentRoot, TimeFrameType targetHorizon, DateTime beforeUtc, int pageSize, CancellationToken cancellationToken);
    Task<RegimeDiscoveryReadModel?> GetRegimeDiscoveryAsync(StrategyWorkflowId workflowId);
    Task<RegimeDiscoveryReadModel?> GetRegimeDiscoveryAsync(StrategyWorkflowId workflowId, CancellationToken cancellationToken);
    Task<IntrinsicTimeStrategyWorkflowReadModel?> GetIntrinsicTimeStrategyWorkflowAsync(StrategyWorkflowId workflowId);
    Task<IntrinsicTimeStrategyWorkflowReadModel?> GetIntrinsicTimeStrategyWorkflowAsync(StrategyWorkflowId workflowId, CancellationToken cancellationToken);
    Task<ActiveIntrinsicTimeStrategyWorkflowReadModel?> GetActiveIntrinsicTimeStrategyWorkflowAsync(string workflowEntityId);
    Task<ActiveIntrinsicTimeStrategyWorkflowReadModel?> GetActiveIntrinsicTimeStrategyWorkflowAsync(string workflowEntityId, CancellationToken cancellationToken);
    Task<ICollection<IntrinsicTimeStrategyWorkflowStartAttemptReadModel>> GetIntrinsicTimeStrategyWorkflowStartAttemptsAsync(string workflowEntityId, DateTime beforeUtc, int pageSize);
    Task<ICollection<IntrinsicTimeStrategyWorkflowStartAttemptReadModel>> GetIntrinsicTimeStrategyWorkflowStartAttemptsAsync(string workflowEntityId, DateTime beforeUtc, int pageSize, CancellationToken cancellationToken);
    Task<ICollection<IntrinsicTimeStrategyWorkflowTimelineReadModel>> GetIntrinsicTimeStrategyWorkflowTimelineAsync(StrategyWorkflowId workflowId, long afterEventId, int pageSize);
    Task<ICollection<IntrinsicTimeStrategyWorkflowTimelineReadModel>> GetIntrinsicTimeStrategyWorkflowTimelineAsync(StrategyWorkflowId workflowId, long afterEventId, int pageSize, CancellationToken cancellationToken);
    Task<ICollection<IntrinsicTimeStrategyWorkflowHistoryReadModel>> GetIntrinsicTimeStrategyWorkflowsByEntityAsync(string workflowEntityId, DateTime beforeUtc, int pageSize);
    Task<ICollection<IntrinsicTimeStrategyWorkflowHistoryReadModel>> GetIntrinsicTimeStrategyWorkflowsByEntityAsync(string workflowEntityId, DateTime beforeUtc, int pageSize, CancellationToken cancellationToken);
    Task<ICollection<IntrinsicTimeStrategyWorkflowHistoryReadModel>> GetIntrinsicTimeStrategyWorkflowsByStatusAsync(StrategyWorkflowStatus status, DateOnly startDate, DateOnly endDate, int pageSize);
    Task<ICollection<IntrinsicTimeStrategyWorkflowHistoryReadModel>> GetIntrinsicTimeStrategyWorkflowsByStatusAsync(StrategyWorkflowStatus status, DateOnly startDate, DateOnly endDate, int pageSize, CancellationToken cancellationToken);

    Task<OptionTradeReadModel?> GetOptionTradeAsync(int orderId, int tradeId);
    Task<OptionTradeReadModel?> GetOptionTradeAsync(int orderId, int tradeId, CancellationToken cancellationToken);
    Task<OptionTradeSpreadsDataModel?> GetOptionTradeSpreadDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType);
    Task<OptionTradeSpreadsDataModel?> GetOptionTradeSpreadDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType, CancellationToken cancellationToken);
    Task<ICollection<OptionTradeSpreadsDataModel>> GetOptionTradeSpreadDataAsync();
    Task<ICollection<OptionTradeSpreadBarsDataModel>> GetOptionTradeSpreadBarDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType, DateTime startDate, DateTime endDate);
    Task<ICollection<OptionTradeSpreadBarsDataModel>> GetOptionTradeSpreadBarDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType, DateTime startDate, DateTime endDate, CancellationToken cancellationToken);
    Task<ICollection<OptionTradeSpreadBarsDataModel>> GetOptionTradeSpreadBarDataAsync();
    Task<TradePriceReadModel?> GetIronCondorTradePriceAsync(int tradeId, DateOnly valueDate);
    Task<TradePriceReadModel?> GetIronCondorTradePriceAsync(int tradeId, DateOnly valueDate, CancellationToken cancellationToken);
    Task<ICollection<OptionTradeReadModel>> GetOptionTradesAsync(int orderId);
    Task<ICollection<OptionTradeReadModel>> GetOptionTradesAsync(int orderId, CancellationToken cancellationToken);
    Task<ICollection<OptionTradeReadModel>> GetOptionTradesAsync();
    Task<ICollection<OptionTradeLegReadModel>> GetOptionLegsAsync();
    Task<ICollection<OptionTradeLegDataReadModel>> GetOptionLegDataAsync();
    Task<TradePositionReadModel?> GetTradePositionAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus);
    Task<TradePositionReadModel?> GetTradePositionAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus, CancellationToken cancellationToken);
    Task<ICollection<TradePositionReadModel>> GetTradePositionsAsync(int orderId, int tradeId);
    Task<ICollection<TradePositionReadModel>> GetTradePositionsAsync(int orderId, int tradeId, CancellationToken cancellationToken);
    Task<ICollection<TradePositionReadModel>> GetTradePositionsAsync();
    Task<ICollection<TradeHistoryReadModel>> GetTradeHistoryAsync(int orderId);
    Task<ICollection<TradeHistoryReadModel>> GetTradeHistoryAsync(int orderId, CancellationToken cancellationToken);
    Task<ICollection<string>> GetOptionLegContractIdsAsync(int tradeId);
    Task<ICollection<string>> GetOptionLegContractIdsAsync(int tradeId, CancellationToken cancellationToken);
    Task<int> GetTradeQuantityAsync(int tradeId);
    Task<int> GetTradeQuantityAsync(int tradeId, CancellationToken cancellationToken);
    Task<TradeLimitReadModel?> GetTradeLimitAsync(int tradeId);
    Task<TradeLimitReadModel?> GetTradeLimitAsync(int tradeId, CancellationToken cancellationToken);
    Task<ICollection<TradeLimitReadModel>> GetTradeLimitsAsync();

    Task<TradePlanStopLossLimitReadModel?> GetTradePlanStopLossLimitAsync(int orderId, int tradeId);
    Task<TradeTypeLimitReadModel?> GetTradeTypeLimitAsync(int tradeId, TradeType tradeType);
    Task<TradeTypeLimitReadModel?> GetTradeTypeLimitAsync(int tradeId, TradeType tradeType, CancellationToken cancellationToken);
    Task<ICollection<TradeTypeLimitReadModel>> GetTradeTypeLimitsAsync(int tradeId);
    Task<ICollection<TradeTypeLimitReadModel>> GetTradeTypeLimitsAsync();
    Task<ICollection<TradeFillReadModel>> GetTradeFillsAsync(int orderId, int tradeId);
    Task<ICollection<TradeFillReadModel>> GetTradeFillsAsync(int orderId, int tradeId, CancellationToken cancellationToken);
    Task<ICollection<TradeFillReadModel>> GetTradeFillsAsync();
    Task<ICollection<TradePlanReadModel>> GetTradePlansAsync();
    Task<ICollection<TradePlanReadModel>> GetTradePlansAsync(int orderId);
    Task<ICollection<TradePlanReadModel>> GetLastTradePlansAsync(int orderId, int tradeId);
    Task<ICollection<TradePlanReadModel>> GetTradePlansAsync(int orderId, int tradeId, DateOnly valueDate);
    Task<ICollection<TradePlanReadModel>> GetTradePlansAsync(int orderId, int tradeId, DateOnly startDate, DateOnly endDate);
    Task<ICollection<TradePlanForwardLossRatioReadModel>> GetTradePlanForwardLossRatiosAsync(DateOnly startDate, DateOnly endDate);
    Task<TradePlanForwardLossRatioReadModel?> GetTradePlanForwardLossRatioAsync(DateOnly valueDate);
    Task<ICollection<TradeFillDataReadModel>> GetTradeFillDataAsync(int tradeId);
    Task<ICollection<TradeLiveFeedReadModel>> GetTradeLiveFeedAsync(int orderId, int tradeId);
    Task<ICollection<string>> GetTradePositionTradeTypesAsync(
       int orderId,
       int tradeId,
       DateOnly valueDate,
        TradeStatus tradeStatus,
        int daysToExpiry);
    Task<ICollection<string>> GetTradePositionTradeTypesAsync(
        int orderId,
        int tradeId,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        int daysToExpiry,
        CancellationToken cancellationToken);
    Task<TradePlanForwardLossLimitReadModel?> GetTradePlanForwardLossLimitAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType);
    Task<TradePlacementSignalReadModel?> GetTradePlacementSignalAsync(string contractId, DateOnly valueDate);
}
