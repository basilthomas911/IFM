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
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Service.MarketDataAnalytics.EventHandlers
{
    public class FuturesTradeSignalLLMEventHandlers : BaseEventServiceHandler,
        IAsyncEventHandler<FuturesTradeSignalLLMStartedEvent, MarketDataAnalyticsService>,
        IAsyncEventHandler<FuturesTradeSignalLLMStoppedEvent, MarketDataAnalyticsService>
    {
        IMarketDataAnalyticsCommandApi CommandApi { get; }
        IMarketDataFeedQueryApi MarketDataFeedQueryApi { get; }
        IFuturesTradeSignalLLMTimer FuturesTradeSignalLLMTimer { get; }
        IBlackboardService BlackboardService { get; }
        ILogger<FuturesTradeSignalLLMEventHandlers> Logger { get; }

        /// <summary>
        /// FuturesTradeSignalLLMEventHandlers constructor
        /// </summary>
        /// <param name="commandApi"></param>
        /// <param name="marketDataFeedQueryApi"></param>
        /// <param name="statusConsoleWriter"></param>
        /// <param name="logger"></param>
        public FuturesTradeSignalLLMEventHandlers(
            IMarketDataAnalyticsCommandApi commandApi,
            IMarketDataFeedQueryApi marketDataFeedQueryApi,
            IFuturesTradeSignalLLMTimer futuresTradeSignalLLMTimer,
            IBlackboardService blackboardService,
            IStatusConsoleWriter statusConsoleWriter,
            ILogger<FuturesTradeSignalLLMEventHandlers> logger) : base(statusConsoleWriter)
        {
            CommandApi = commandApi;
            MarketDataFeedQueryApi = marketDataFeedQueryApi;
            FuturesTradeSignalLLMTimer = futuresTradeSignalLLMTimer;
            BlackboardService = blackboardService;
            Logger = logger;
        }
    
        /// <summary>
        /// start futures trade signal LLM timer events to generate rsi signal from current futures eod data
        /// </summary>
        /// <param name="e">futures trade signal LLM started event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesTradeSignalLLMStartedEvent e)
            => await FuturesTradeSignalLLMTimer.StartAsync(e, async o => {
                try
                {
                    // only generate trade signal LLM  for s&p 500 futures for now...
                    if (e.EntityId.ContractId.StartsWith("ES"))
                    {
                        var futuresEodData = await GetFuturesEodDataAsync(e.EntityId.ContractId, e.EntityId.ValueDate);
                        var vixContractId = BlackboardService.VixFuturesContractId.Get(e.EntityId.ValueDate);
                        var vixFuturesEodData = await GetVixFuturesEodDataAsync(vixContractId, e.EntityId.ValueDate);
                        if (futuresEodData is not null && vixFuturesEodData is not null)
                        {
                            var priceVolatility = vixFuturesEodData.Last().ClosePrice;    
                            await CommandApi.GenerateFuturesTradeSignalLLMAsync(futuresEodData, priceVolatility);
                        }
                    }
                }
                catch (Exception ex)
                {
                    await WriteConsoleAsync(LogSourceType.MarketDataAnalytics, e.ErrorCode, ex.GetErrorMessage());
                    Logger.LogError(ex.GetErrorMessage(), $"{LogSourceType.MarketDataAnalytics}: futures trade signal LLM started {e.EntityId.ContractId} event handler failed");
                }
            });

        /// <summary>
        /// stop futures rsi signal timer events 
        /// </summary>
        /// <param name="e">futures rsi signal stopped event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesTradeSignalLLMStoppedEvent e) 
            => await FuturesTradeSignalLLMTimer.StopAsync(e);

       

        /// <summary>
        /// return futures eod data
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        async Task<FuturesEodDataV2ReadModel?> GetFuturesEodDataAsync(string contractId, DateOnly    valueDate)
        {
                var futuresEodData = default(FuturesEodDataV2ReadModel);
                var serviceResult = await MarketDataFeedQueryApi.GetFuturesEodDataAsync(contractId, valueDate);
                if (serviceResult.Success && serviceResult.Value is not null)
                    futuresEodData = serviceResult.Value;
            return futuresEodData;
        }

        /// <summary>
        /// return vix futures eod data
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        async Task<VixFuturesEodDataReadModel[]?> GetVixFuturesEodDataAsync(string contractId, DateOnly valueDate)
        {
            var vixFuturesEodData = default(VixFuturesEodDataReadModel[]);
            var serviceResult = await MarketDataFeedQueryApi.GetVixFuturesEodDataAsync(contractId, valueDate);
            if (serviceResult.Success && serviceResult.Value is not null)
                vixFuturesEodData = serviceResult.Value;
            return vixFuturesEodData;
        }
    }
}
