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
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log;

namespace TomasAI.IFM.Service.MarketDataAnalytics.EventHandlers
{
    public class FuturesTdiSignalEventHandlers : BaseEventServiceHandler,
        IAsyncEventHandler<FuturesTdiSignalStartedEvent, MarketDataAnalyticsService>,
        IAsyncEventHandler<FuturesTdiSignalStoppedEvent, MarketDataAnalyticsService>
    {
        IMarketDataAnalyticsCommandApi CommandApi { get; }
        IMarketDataAnalyticsQueryApi MarketDataAnalyticsQueryApi { get; }
        IFuturesTdiSignalTimer FuturesTdiSignalTimer { get; }
        ILogger<FuturesTdiSignalEventHandlers> Logger { get; }

        /// <summary>
        /// FuturesTdiSignalEventHandlers constructor
        /// </summary>
        /// <param name="commandApi"></param>
        /// <param name="marketDataAnalyticsQueryApi"></param>
        /// <param name="statusConsoleWriter"></param>
        /// <param name="futuresTdiSignalTimer"></param>
        /// <param name="logger"></param>
        public FuturesTdiSignalEventHandlers(
            IMarketDataAnalyticsCommandApi commandApi,
            IMarketDataAnalyticsQueryApi marketDataAnalyticsQueryApi,
            IFuturesTdiSignalTimer futuresTdiSignalTimer,
            IStatusConsoleWriter statusConsoleWriter,
            ILogger<FuturesTdiSignalEventHandlers> logger) : base(statusConsoleWriter)
        {
            CommandApi = commandApi;
            MarketDataAnalyticsQueryApi = marketDataAnalyticsQueryApi;
            FuturesTdiSignalTimer = futuresTdiSignalTimer;
            Logger = logger;
        }
    
        /// <summary>
        /// start futures tdi signal timer events to generate tdi signal from current futures trend direction
        /// </summary>
        /// <param name="e">futures tdi signal started event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesTdiSignalStartedEvent e)
            => await FuturesTdiSignalTimer.StartAsync(e, async o => {
                try
                {
                    // only generate tdi signals for s&p 500 futures for now...
                    if (e.EntityId.ContractId.StartsWith("ES"))
                    {
                        var timestamp = DateTime.Now;
                        var loopback = 10;
                        var startTime = timestamp.AddMinutes(-10);
                        var endTime = timestamp;
                        var serviceResult = await MarketDataAnalyticsQueryApi.GetFuturesTrendDirectionFromRSISignalAsync(
                            e.EntityId.ContractId, e.EntityId.ValueDate, timestamp, loopback, startTime, endTime);
                        if (serviceResult.Success && serviceResult.Value is not null)
                        {
                            var futuresTrendDirection = serviceResult.Value;
                            await CommandApi.GenerateFuturesTdiSignalAsync(futuresTrendDirection);
                        }
                    }
                }
                catch (Exception ex)
                {
                    await WriteConsoleAsync(LogSourceType.MarketDataAnalytics, e.ErrorCode, ex.GetErrorMessage());
                    Logger.LogError(ex.GetErrorMessage(), $"{LogSourceType.MarketDataAnalytics}: futures tdi signal started {e.EntityId.ContractId} event handler failed");
                }
            });

        /// <summary>
        /// stop futures tdi signal timer events 
        /// </summary>
        /// <param name="e">futures tdi signal stopped event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesTdiSignalStoppedEvent e) => await FuturesTdiSignalTimer.StopAsync(e);

    }
}
