using TomasAI.IFM.UI.Net.Services.Analytics;
using TomasAI.IFM.UI.Net.Services.Application;
using TomasAI.IFM.UI.Net.Services.Fund;
using TomasAI.IFM.UI.Net.Services.MarketData;
using TomasAI.IFM.UI.Net.Services.MarketDataFeed;
using TomasAI.IFM.UI.Net.Services.OptionPricing;
using TomasAI.IFM.UI.Net.Services.Trade;

namespace TomasAI.IFM.UI.Net.Services;

/// <summary>
/// Exposes the UI's explicitly registered domain-service boundary without generic service location.
/// </summary>
public interface IUiServiceCatalog
{
    /// <summary>Gets the shared command-response event service.</summary>
    CommandResponseEventService CommandResponses { get; }

    /// <summary>Gets the application-event service.</summary>
    ApplicationEventService ApplicationEvents { get; }

    /// <summary>Gets the status-console service.</summary>
    StatusConsoleService StatusConsole { get; }

    /// <summary>Gets the Fund command service.</summary>
    FundCommandService FundCommands { get; }

    /// <summary>Gets the Fund query service.</summary>
    FundQueryService FundQueries { get; }

    /// <summary>Gets the Fund event service.</summary>
    FundEventService FundEvents { get; }

    /// <summary>Gets the Fund-order event service.</summary>
    FundOrderEventService FundOrderEvents { get; }

    /// <summary>Gets the Market Data command service.</summary>
    MarketDataCommandService MarketDataCommands { get; }

    /// <summary>Gets the Market Data query service.</summary>
    MarketDataQueryService MarketDataQueries { get; }

    /// <summary>Gets the Market Data event service.</summary>
    MarketDataEventService MarketDataEvents { get; }

    /// <summary>Gets the option-spread bar event service.</summary>
    OptionTradeSpreadBarDataEventService SpreadBarEvents { get; }

    /// <summary>Gets the Market Data Feed command service.</summary>
    MarketDataFeedCommandService FeedCommands { get; }

    /// <summary>Gets the Market Data Feed query service.</summary>
    MarketDataFeedQueryService FeedQueries { get; }

    /// <summary>Gets the analytics command service.</summary>
    MarketDataAnalyticsCommandService AnalyticsCommands { get; }

    /// <summary>Gets the analytics query service.</summary>
    MarketDataAnalyticsQueryService AnalyticsQueries { get; }

    /// <summary>Gets the analytics event service.</summary>
    MarketDataAnalyticsEventService AnalyticsEvents { get; }

    /// <summary>Gets the UI-facing option-pricing service.</summary>
    IOptionPricingService OptionPricing { get; }

    /// <summary>Gets the spread-distribution job service.</summary>
    SpreadDistributionJobService SpreadDistributionJobs { get; }

    /// <summary>Gets the Strategy Operations service.</summary>
    StrategyOperationsService StrategyOperations { get; }

    /// <summary>Gets the Trade command service.</summary>
    TradeCommandService TradeCommands { get; }

    /// <summary>Gets the Trade query service.</summary>
    TradeQueryService TradeQueries { get; }

    /// <summary>Gets the Trade Placement command service.</summary>
    TradePlacementCommandService TradePlacementCommands { get; }

    /// <summary>Gets the Trade Placement event service.</summary>
    TradePlacementEventService TradePlacementEvents { get; }

    /// <summary>Gets the Trade Plan query service.</summary>
    TradePlanQueryService TradePlanQueries { get; }

    /// <summary>Gets the Trade Plan event service.</summary>
    TradePlanEventService TradePlanEvents { get; }

    /// <summary>Gets the Trade Plan action event service.</summary>
    TradePlanActionEventService TradePlanActionEvents { get; }

    /// <summary>Gets the Trade Position event service.</summary>
    TradePositionFeedEventService TradePositionEvents { get; }

    /// <summary>Gets the end-of-day event service.</summary>
    EndOfDayProcessEventService EndOfDayEvents { get; }
}
