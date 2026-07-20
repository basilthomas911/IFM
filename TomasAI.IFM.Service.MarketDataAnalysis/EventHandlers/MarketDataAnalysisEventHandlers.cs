using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Service.MarketDataAnalytics;

namespace TomasAI.IFM.Service.MarketDataAnalysis.EventHandlers
{
    /// <summary>
    /// market data analysis event handlers
    /// </summary>
    public class MarketDataAnalysisEventHandlers : BaseEventServiceHandler,
        IAsyncEventHandler<FuturesItiSignalHoldTradeChangedEvent, MarketDataAnalyticsService>
    {
        IMarketDataAnalyticsCommandApi CommandApi { get; }
        IMarketDataAnalyticsQueryApi QueryApi { get; }
        IMarketDataFeedQueryApi MarketDataFeedQueryApi { get; }
        IBlackboardService BlackBoardService { get; }
        ILogger<MarketDataAnalysisEventHandlers> Logger { get; }

        /// <summary>
        /// MarketDataAnalysisEventHandlers constructor
        /// </summary>
        /// <param name="commandApi"></param>
        /// <param name="marketDataFeedQueryApi"></param>
        /// <param name="blackBoardService"></param>
        /// <param name="statusConsoleWriter"></param>
        /// <param name="logger"></param>
        public MarketDataAnalysisEventHandlers(
            IMarketDataAnalyticsCommandApi commandApi,
            IMarketDataAnalyticsQueryApi queryApi,
            IMarketDataFeedQueryApi marketDataFeedQueryApi,
            IBlackboardService blackBoardService,
            IStatusConsoleWriter statusConsoleWriter,
            ILogger<MarketDataAnalysisEventHandlers> logger) : base(statusConsoleWriter)
        {
            CommandApi = IsArgumentNull.Set(commandApi);
            QueryApi = IsArgumentNull.Set(queryApi);
            MarketDataFeedQueryApi = IsArgumentNull.Set(marketDataFeedQueryApi);
            BlackBoardService = IsArgumentNull.Set(blackBoardService);
            Logger = IsArgumentNull.Set(logger);
        }

        /// <summary>
        /// handle futures iti signal hold trade changed event
        /// </summary>
        /// <param name="e"> futures iti signal hold trade changed event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesItiSignalHoldTradeChangedEvent e)
        {
            try
            {
                if (e.HoldTrade)
                    await CommandApi.SetFuturesItiSignalHoldTradeAsync(e.FuturesItiSignalId);
                else
                    await CommandApi.ClearFuturesItiSignalHoldTradeAsync(e.FuturesItiSignalId);
            }
            catch (Exception ex)
            {
                await WriteConsoleAsync(LogSourceType.MarketDataAnalytics, e.ErrorCode, ex.GetErrorMessage());
                Logger.LogError(ex.GetErrorMessage(), $"{LogSourceType.MarketDataAnalytics}: futures iti signal hold trade changed event - {e.FuturesItiSignalId.ContractId} handler failed");
            }
        }
    }
}
