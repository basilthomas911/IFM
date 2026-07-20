using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ServiceApi;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Service.MarketDataAnalytics;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Events;

namespace TomasAI.IFM.Service.MarketDataAnalysis.EventHandlers
{
    public class FuturesItiTrendEventHandlers : BaseEventServiceHandler,
         IAsyncEventHandler<FuturesItiTrendModelBuildStartedEvent, MarketDataAnalyticsService>,
         IAsyncEventHandler<FuturesItiTrendDeltaModelDataLoadedCompleteEvent, MarketDataAnalyticsService>,
         IAsyncEventHandler<FuturesItiTrendModelLoadedCompleteEvent, MarketDataAnalyticsService>
    {
        IFuturesItiTrendCommandApi CommandApi { get; }
        ILogger<FuturesItiTrendEventHandlers> Logger { get; }

        /// <summary>
        /// FuturesBarDataInsertedEventHandler constructor
        /// </summary>
        /// <param name="commandApi"></param>
        /// <param name="statusConsoleWriter"></param>
        /// <param name="logger"></param>
        public FuturesItiTrendEventHandlers(
            IFuturesItiTrendCommandApi commandApi,
            IStatusConsoleWriter statusConsoleWriter,
            ILogger<FuturesItiTrendEventHandlers> logger) : base(statusConsoleWriter)
        {
            CommandApi = commandApi;
            Logger = logger;
        }

        /// <summary>
        /// futures iti trend mode build started, load model data
        /// </summary>
        /// <param name="e">futures iti trend mode build started event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesItiTrendModelBuildStartedEvent e)
        {
            try
            {
                await WriteConsoleAsync(LogSourceType.PredictiveModel, $"trend model build started - {e.EntityId.Symbol}:{e.EntityId.ValueDate:yyyy-MM-dd} ...");
                await CommandApi.LoadFuturesItiTrendDeltaModelDataAsync(e.EntityId.Symbol, e.EntityId.ValueDate, e.StartDate, e.EndDate);
                await WriteConsoleAsync(LogSourceType.PredictiveModel, $"loading trend model Data - {e.EntityId.Symbol}:{e.EntityId.ValueDate:yyyy-MM-dd} ...");
            }
            catch (Exception ex)
            {
                await WriteConsoleAsync(LogSourceType.PredictiveModel, e.ErrorCode, ex.GetErrorMessage());
                Logger.LogError(ex.GetErrorMessage(), $"{LogSourceType.PredictiveModel}: futures iti trend model build started event - {e.EntityId.Symbol}:{e.EntityId.ValueDate:yyyy-MM-dd} handler failed");
            }
        }

        /// <summary>
        /// futures iti trend model data loaded, start trainging model
        /// </summary>
        /// <param name="e">futures iti trend model data loaded complete  event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesItiTrendDeltaModelDataLoadedCompleteEvent e)
        {
            try
            {
                await WriteConsoleAsync(LogSourceType.PredictiveModel, $"trend model data loading complete - {e.EntityId.Symbol}:{e.EntityId.ValueDate:yyyy-MM-dd} ...");
                await CommandApi.TrainFuturesItiTrendDeltaModelAsync(e.EntityId.Symbol, e.EntityId.ValueDate, e.StartDate, e.EndDate, e.Statistics);
                await WriteConsoleAsync(LogSourceType.PredictiveModel, $"training trend model for - {e.EntityId.Symbol}:{e.EntityId.ValueDate:yyyy-MM-dd} ...");
            }
            catch (Exception ex)
            {
                await WriteConsoleAsync(LogSourceType.PredictiveModel, 9991, ex.GetErrorMessage());
                Logger.LogError(ex.GetErrorMessage(), $"{LogSourceType.PredictiveModel}: futures iti trend model data loaded complete event - {e.EntityId.Symbol}:{e.EntityId.ValueDate:yyyy-MM-dd} handler failed");
            }
        }

        /// <summary>
        /// futures iti trend model loaded complete event
        /// </summary>
        /// <param name="e">futures iti trend model loaded complete event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesItiTrendModelLoadedCompleteEvent e)
            => await WriteConsoleAsync(LogSourceType.PredictiveModel, $"trend model training complete - {e.EntityId.Symbol}:{e.EntityId.ValueDate:yyyy-MM-dd} ");
    }

}
