using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Linq;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.MarketDataAnalytics;

namespace TomasAI.IFM.Service.MarketDataAnalytics.EventHandlers
{
    public class FuturesRsiSignalEventHandlers : BaseEventServiceHandler,
        IAsyncEventHandler<FuturesRsiSignalStartedEvent, MarketDataAnalyticsService>,
        IAsyncEventHandler<FuturesRsiSignalStoppedEvent, MarketDataAnalyticsService>,
        IAsyncEventHandler<FuturesRsiSignalsGeneratedEvent, MarketDataAnalyticsService>
    {
        IMarketDataAnalyticsCommandApi CommandApi { get; }
        IMarketDataFeedQueryApi MarketDataFeedQueryApi { get; }
        IFuturesRsiSignalTimer FuturesRsiSignalTimer { get; }
        ILogger<FuturesRsiSignalEventHandlers> Logger { get; }

        /// <summary>
        /// FuturesBarDataInsertedEventHandler constructor
        /// </summary>
        /// <param name="commandApi"></param>
        /// <param name="marketDataFeedQueryApi"></param>
        /// <param name="statusConsoleWriter"></param>
        /// <param name="logger"></param>
        public FuturesRsiSignalEventHandlers(
            IMarketDataAnalyticsCommandApi commandApi,
            IMarketDataFeedQueryApi marketDataFeedQueryApi,
            IFuturesRsiSignalTimer futuresRsiSignalTimer,
            IStatusConsoleWriter statusConsoleWriter,
            ILogger<FuturesRsiSignalEventHandlers> logger) : base(statusConsoleWriter)
        {
            CommandApi = commandApi;
            MarketDataFeedQueryApi = marketDataFeedQueryApi;
            FuturesRsiSignalTimer = futuresRsiSignalTimer;
            Logger = logger;
        }
    
        /// <summary>
        /// start futures rsi signal timer events to generate rsi signal from current futures eod data
        /// </summary>
        /// <param name="e">futures rsi signal started event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesRsiSignalStartedEvent e)
            => await FuturesRsiSignalTimer.StartAsync(e, async o => {
                try
                {
                    // only generate rsi signals for s&p 500 futures for now...
                    if (e.EntityId.ContractId.StartsWith("ES"))
                    {
                        var serviceResult = await MarketDataFeedQueryApi.GetFuturesEodDataAsync(e.EntityId.ContractId, e.EntityId.ValueDate);
                        if (serviceResult.Success && serviceResult.Value is not null)
                        {
                            var futuresEodData = serviceResult.Value;
                            await CommandApi.GenerateFuturesRsiSignalAsync(futuresEodData);
                        }
                    }
                }
                catch (Exception ex)
                {
                    await WriteConsoleAsync(LogSourceType.MarketDataAnalytics, e.ErrorCode, ex.GetErrorMessage());
                    Logger.LogError(ex.GetErrorMessage(), $"{LogSourceType.MarketDataAnalytics}: futures rsi signal started {e.EntityId.ContractId} event handler failed");
                }
            });

        /// <summary>
        /// stop futures rsi signal timer events 
        /// </summary>
        /// <param name="e">futures rsi signal stopped event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesRsiSignalStoppedEvent e) => await FuturesRsiSignalTimer.StopAsync(e);

        /// <summary>
        /// generate futures tdi signal from rsi signals
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesRsiSignalsGeneratedEvent e)
        {
            try
            {
                var futuresTdiSignalId = new FuturesTdiSignalId(e.FuturesRsiSignalsId.ContractId, e.FuturesRsiSignalsId.ValueDate, e.FuturesRsiSignalsId.Timestamp);
                await CommandApi.GenerateFuturesTdiSignalAsync(futuresTdiSignalId, e.FuturesRsiSignals);
            }
            catch (Exception ex)
            {
                await WriteConsoleAsync(LogSourceType.MarketDataAnalytics, e.ErrorCode, ex.GetErrorMessage());
                Logger.LogError(ex.GetErrorMessage(), $"{LogSourceType.MarketDataAnalytics}: futures rsi signals generated {e.FuturesRsiSignalsId.ContractId} event handler failed");
            }

        }
    }
}
