using TomasAI.IFM.UI.Net.Services.Analytics;
using TomasAI.IFM.UI.Net.Services.Application;
using TomasAI.IFM.UI.Net.Services.MarketData;
using TomasAI.IFM.UI.Net.Services.MarketDataFeed;
using TomasAI.IFM.UI.Net.Services.OptionPricing;
using TomasAI.IFM.UI.Net.Services.Trade;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;

namespace TomasAI.IFM.UI.Net.Services;

/// <summary>Provides immutable, typed access to the registered UI domain services.</summary>
public sealed class UiServiceCatalog(
    IPortfolioCommandApi portfolioCommands,
    IPortfolioFundCommandApi portfolioFundCommands,
    IPortfolioQueryApi portfolioQueries,
    IPortfolioIdentityApi portfolioIdentities,
    IPortfolioFinancialPolicyCommandApi portfolioPolicyCommands,
    IPortfolioOrderCompositionApi portfolioOrderCompositions,
    ITradeOrderLifecycleApi tradeOrderLifecycle,
    IBrokerAccountQueryApi brokerAccounts,
    IBrokerAccountCommandApi brokerAccountCommands,
    IBrokerOrderQueryApi brokerOrders,
    IOrderExecutionQueryApi orderExecutions,
    TomasAI.IFM.Domain.Reference.Shared.ServiceApi.IReferenceQueryApi referenceQueries,
    TomasAI.IFM.Domain.Reference.Shared.ServiceApi.IReferenceCommandApi referenceCommands,
    CommandResponseEventService commandResponses,
    ApplicationEventService applicationEvents,
    ApplicationQueryService applicationQueries,
    StatusConsoleService statusConsole,    MarketDataCommandService marketDataCommands,
    MarketDataQueryService marketDataQueries,
    MarketDataEventService marketDataEvents,
    OptionTradeSpreadBarDataEventService spreadBarEvents,
    MarketDataFeedCommandService feedCommands,
    MarketDataFeedQueryService feedQueries,
    MarketDataAnalyticsCommandService analyticsCommands,
    MarketDataAnalyticsQueryService analyticsQueries,
    MarketDataAnalyticsEventService analyticsEvents,
    IOptionPricingService optionPricing,
    SpreadDistributionJobService spreadDistributionJobs,
    StrategyOperationsService strategyOperations,
    TradeQueryService tradeQueries,
    TradePlacementCommandService tradePlacementCommands,
    TradePlacementEventService tradePlacementEvents,
    TradePlanQueryService tradePlanQueries,
    StrategyTradePlanQueryService strategyTradePlanQueries,
    StrategyPositionService strategyPositions,
    PortfolioTradeOrderService portfolioTradeOrders,
    TradePlanEventService tradePlanEvents,
    TradePlanActionEventService tradePlanActionEvents,
    TradePositionFeedEventService tradePositionEvents,
    TomasAI.IFM.Domain.Portfolio.Shared.Financial.IPortfolioFinancialApi portfolioFinancial, TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement.IRiskQueryApi riskQueries) : IUiServiceCatalog
{
    public IPortfolioCommandApi PortfolioCommands { get; } = portfolioCommands;
    public IPortfolioFundCommandApi PortfolioFundCommands { get; } = portfolioFundCommands;
    public IPortfolioQueryApi PortfolioQueries { get; } = portfolioQueries;
    public TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement.IRiskQueryApi RiskQueries { get; } = riskQueries;
    public TomasAI.IFM.Domain.Portfolio.Shared.Financial.IPortfolioFinancialApi PortfolioFinancial { get; } = portfolioFinancial;
    public IPortfolioIdentityApi PortfolioIdentities { get; } = portfolioIdentities;
    public IPortfolioFinancialPolicyCommandApi PortfolioPolicyCommands { get; } = portfolioPolicyCommands;
    /// <inheritdoc />
    public IPortfolioOrderCompositionApi PortfolioOrderCompositions { get; } = portfolioOrderCompositions;
    /// <inheritdoc />
    public ITradeOrderLifecycleApi TradeOrderLifecycle { get; } = tradeOrderLifecycle;
    /// <inheritdoc />
    public IBrokerAccountQueryApi BrokerAccounts { get; } = brokerAccounts;
    /// <inheritdoc />
    public IBrokerAccountCommandApi BrokerAccountCommands { get; } = brokerAccountCommands;
    /// <inheritdoc />
    public IBrokerOrderQueryApi BrokerOrders { get; } = brokerOrders;
    /// <inheritdoc />
    public IOrderExecutionQueryApi OrderExecutions { get; } = orderExecutions;
    public TomasAI.IFM.Domain.Reference.Shared.ServiceApi.IReferenceQueryApi ReferenceQueries { get; } = referenceQueries;
    public TomasAI.IFM.Domain.Reference.Shared.ServiceApi.IReferenceCommandApi ReferenceCommands { get; } = referenceCommands;
    /// <inheritdoc />
    public CommandResponseEventService CommandResponses { get; } = commandResponses;
    /// <inheritdoc />
    public ApplicationEventService ApplicationEvents { get; } = applicationEvents;
    /// <inheritdoc />
    public ApplicationQueryService ApplicationQueries { get; } = applicationQueries;
    /// <inheritdoc />
    public StatusConsoleService StatusConsole { get; } = statusConsole;
    /// <inheritdoc />
    public MarketDataCommandService MarketDataCommands { get; } = marketDataCommands;
    /// <inheritdoc />
    public MarketDataQueryService MarketDataQueries { get; } = marketDataQueries;
    /// <inheritdoc />
    public MarketDataEventService MarketDataEvents { get; } = marketDataEvents;
    /// <inheritdoc />
    public OptionTradeSpreadBarDataEventService SpreadBarEvents { get; } = spreadBarEvents;
    /// <inheritdoc />
    public MarketDataFeedCommandService FeedCommands { get; } = feedCommands;
    /// <inheritdoc />
    public MarketDataFeedQueryService FeedQueries { get; } = feedQueries;
    /// <inheritdoc />
    public MarketDataAnalyticsCommandService AnalyticsCommands { get; } = analyticsCommands;
    /// <inheritdoc />
    public MarketDataAnalyticsQueryService AnalyticsQueries { get; } = analyticsQueries;
    /// <inheritdoc />
    public MarketDataAnalyticsEventService AnalyticsEvents { get; } = analyticsEvents;
    /// <inheritdoc />
    public IOptionPricingService OptionPricing { get; } = optionPricing;
    /// <inheritdoc />
    public SpreadDistributionJobService SpreadDistributionJobs { get; } = spreadDistributionJobs;
    /// <inheritdoc />
    public StrategyOperationsService StrategyOperations { get; } = strategyOperations;
    /// <inheritdoc />
    public TradeQueryService TradeQueries { get; } = tradeQueries;
    /// <inheritdoc />
    public TradePlacementCommandService TradePlacementCommands { get; } = tradePlacementCommands;
    /// <inheritdoc />
    public TradePlacementEventService TradePlacementEvents { get; } = tradePlacementEvents;
    /// <inheritdoc />
    public TradePlanQueryService TradePlanQueries { get; } = tradePlanQueries;
    /// <inheritdoc />
    public StrategyTradePlanQueryService StrategyTradePlanQueries { get; } = strategyTradePlanQueries;
    /// <inheritdoc />
    public StrategyPositionService StrategyPositions { get; } = strategyPositions;
    /// <inheritdoc />
    public PortfolioTradeOrderService PortfolioTradeOrders { get; } = portfolioTradeOrders;
    /// <inheritdoc />
    public TradePlanEventService TradePlanEvents { get; } = tradePlanEvents;
    /// <inheritdoc />
    public TradePlanActionEventService TradePlanActionEvents { get; } = tradePlanActionEvents;
    /// <inheritdoc />
    public TradePositionFeedEventService TradePositionEvents { get; } = tradePositionEvents;
}
