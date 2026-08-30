using TomasAI.IFM.UI.Net.Services.Analytics;
using TomasAI.IFM.UI.Net.Services.Application;
using TomasAI.IFM.UI.Net.Services.Fund;
using TomasAI.IFM.UI.Net.Services.MarketData;
using TomasAI.IFM.UI.Net.Services.MarketDataFeed;
using TomasAI.IFM.UI.Net.Services.OptionPricing;
using TomasAI.IFM.UI.Net.Services.Trade;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;

namespace TomasAI.IFM.UI.Net.Services;

/// <summary>Provides immutable, typed access to the registered UI domain services.</summary>
public sealed class UiServiceCatalog(
    IPortfolioCommandApi portfolioCommands,
    IPortfolioFundCommandApi portfolioFundCommands,
    IPortfolioQueryApi portfolioQueries,
    IPortfolioIdentityApi portfolioIdentities,
    CommandResponseEventService commandResponses,
    ApplicationEventService applicationEvents,
    StatusConsoleService statusConsole,
    FundCommandService fundCommands,
    FundQueryService fundQueries,
    FundEventService fundEvents,
    FundOrderEventService fundOrderEvents,
    MarketDataCommandService marketDataCommands,
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
    TradeCommandService tradeCommands,
    TradeQueryService tradeQueries,
    TradePlacementCommandService tradePlacementCommands,
    TradePlacementEventService tradePlacementEvents,
    TradePlanQueryService tradePlanQueries,
    TradePlanEventService tradePlanEvents,
    TradePlanActionEventService tradePlanActionEvents,
    TradePositionFeedEventService tradePositionEvents,
    EndOfDayProcessEventService endOfDayEvents) : IUiServiceCatalog
{
    public IPortfolioCommandApi PortfolioCommands { get; } = portfolioCommands;
    public IPortfolioFundCommandApi PortfolioFundCommands { get; } = portfolioFundCommands;
    public IPortfolioQueryApi PortfolioQueries { get; } = portfolioQueries;
    public IPortfolioIdentityApi PortfolioIdentities { get; } = portfolioIdentities;
    /// <inheritdoc />
    public CommandResponseEventService CommandResponses { get; } = commandResponses;
    /// <inheritdoc />
    public ApplicationEventService ApplicationEvents { get; } = applicationEvents;
    /// <inheritdoc />
    public StatusConsoleService StatusConsole { get; } = statusConsole;
    /// <inheritdoc />
    public FundCommandService FundCommands { get; } = fundCommands;
    /// <inheritdoc />
    public FundQueryService FundQueries { get; } = fundQueries;
    /// <inheritdoc />
    public FundEventService FundEvents { get; } = fundEvents;
    /// <inheritdoc />
    public FundOrderEventService FundOrderEvents { get; } = fundOrderEvents;
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
    public TradeCommandService TradeCommands { get; } = tradeCommands;
    /// <inheritdoc />
    public TradeQueryService TradeQueries { get; } = tradeQueries;
    /// <inheritdoc />
    public TradePlacementCommandService TradePlacementCommands { get; } = tradePlacementCommands;
    /// <inheritdoc />
    public TradePlacementEventService TradePlacementEvents { get; } = tradePlacementEvents;
    /// <inheritdoc />
    public TradePlanQueryService TradePlanQueries { get; } = tradePlanQueries;
    /// <inheritdoc />
    public TradePlanEventService TradePlanEvents { get; } = tradePlanEvents;
    /// <inheritdoc />
    public TradePlanActionEventService TradePlanActionEvents { get; } = tradePlanActionEvents;
    /// <inheritdoc />
    public TradePositionFeedEventService TradePositionEvents { get; } = tradePositionEvents;
    /// <inheritdoc />
    public EndOfDayProcessEventService EndOfDayEvents { get; } = endOfDayEvents;
}
