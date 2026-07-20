using Microsoft.Extensions.Logging;
using TomasAI.IFM.Service.TradePlacement.Model;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;
using TomasAI.IFM.Shared.Trade.ServiceApi;

namespace TomasAI.IFM.Service.TradePlacement;

/// <summary>
/// Provides functionality for managing trade placement events, including interaction with trade placement commands,
/// market data analytics, and event timing mechanisms.
/// </summary>
/// <remarks>This service integrates with APIs for trade placement commands and market data analytics, as well as
/// a timer for managing trade placement event scheduling. It is designed to handle trade placement-related operations
/// and event processing within the system.</remarks>
public class TradePlacementEventService : 
    BaseEventService, ITradePlacementEventService
{
    public TradePlacementEventService(
        ITradePlacementCommandApi tradePlacementCommand,
        IMarketDataAnalyticsQueryApi marketDataAnalyticsQuery,
        ITradePlacementTimer tradePlacementTimer,
        IEventServiceHandlerResolver eventHandlerResolver,
        ILogger<ITradePlacementEventService> logger)
        :base(eventHandlerResolver, logger)
    {
        TradePlacementCommandApi = IsArgumentNull.Set(tradePlacementCommand);
        MarketDataAnalyticsQueryApi = IsArgumentNull.Set(marketDataAnalyticsQuery);
        TradePlacementTimer = IsArgumentNull.Set(tradePlacementTimer);
        logger.LogInformation("TradePlacementService started");
    }

    protected override string ServiceName => GetType().Name;
    public ITradePlacementCommandApi TradePlacementCommandApi { get; } 
    public IMarketDataAnalyticsQueryApi MarketDataAnalyticsQueryApi { get; }
    public ITradePlacementTimer TradePlacementTimer { get; } 
}

/// <summary>
/// Defines a service for handling events related to trade placement operations.
/// </summary>
/// <remarks>This interface extends <see cref="IEventService"/> to provide functionality specific to trade
/// placement events. Implementations of this service are responsible for managing and processing events triggered
/// during trade placement workflows.</remarks>
public interface ITradePlacementEventService : IEventService
{
}
