using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Events;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ServiceApi;

namespace TomasAI.IFM.Service.PredictiveModel.UseCases
{
    public class GenerateFuturesItiTrendModelUseCase : BaseEventServiceHandler,
         IAsyncEventHandler<FuturesItiTrendModelBuildStartedEvent, PredictiveModelService>,
         IAsyncEventHandler<FuturesItiTrendDeltaModelDataLoadedCompleteEvent, PredictiveModelService>,
         IAsyncEventHandler<FuturesItiTrendDeltaModelTrainedCompleteEvent, PredictiveModelService>,
         IAsyncEventHandler<FuturesItiTrendClassModelDataLoadedCompleteEvent, PredictiveModelService>,
         IAsyncEventHandler<FuturesItiTrendClassModelTrainedCompleteEvent, PredictiveModelService>
    {
        IFuturesItiTrendCommandApi CommandApi { get; }
        ILogger<GenerateFuturesItiTrendModelUseCase> Logger { get; }

        /// <summary>
        /// create futures iti trend events handler
        /// </summary>
        /// <param name="commandApi"></param>
        /// <param name="statusConsoleWriter"></param>
        /// <param name="logger"></param>
        public GenerateFuturesItiTrendModelUseCase(
            IFuturesItiTrendCommandApi commandApi,
            IStatusConsoleWriter statusConsoleWriter,
            ILogger<GenerateFuturesItiTrendModelUseCase> logger) : base(statusConsoleWriter)
        {
            CommandApi = commandApi;
            Logger = logger;
        }

        /// <summary>
        /// futures iti trend mode build started
        /// </summary>
        /// <param name="e">futures iti trend mode build started event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesItiTrendModelBuildStartedEvent e)
        {
            try
            {
                await WriteConsoleAsync(LogSourceType.PredictiveModel, $"futures iti trend model build started - {e.EntityId.Symbol}:{e.EntityId.ValueDate:yyyy-MM-dd} ...");
               await CommandApi.LoadFuturesItiTrendDeltaModelDataAsync(e.EntityId.Symbol, e.EntityId.ValueDate, e.StartDate, e.EndDate);
                await WriteConsoleAsync(LogSourceType.PredictiveModel, $"loading futures iti trend delta model data  - {e.EntityId.Symbol}:{e.EntityId.ValueDate:yyyy-MM-dd} ...");
                await CommandApi.LoadFuturesItiTrendClassModelDataAsync(e.EntityId.Symbol, e.EntityId.ValueDate, e.StartDate, e.EndDate);
                await WriteConsoleAsync(LogSourceType.PredictiveModel, $"loading futures iti trend class model data  - {e.EntityId.Symbol}:{e.EntityId.ValueDate:yyyy-MM-dd} ...");
            }
            catch (Exception ex)
            {
                await WriteConsoleAsync(LogSourceType.PredictiveModel, FuturesItiTrendModelBuildStartedEvent.ErrorCode, ex.GetErrorMessage());
                Logger.LogError(ex.GetErrorMessage(), $"{LogSourceType.PredictiveModel}: futures iti trend model build started event - {e.EntityId.Symbol}:{e.EntityId.ValueDate:yyyy-MM-dd} handler failed");
            }
        }

        /// <summary>
        /// futures iti trend model data loaded
        /// </summary>
        /// <param name="e">futures iti trend model data loaded complete  event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesItiTrendDeltaModelDataLoadedCompleteEvent e)
        {
            try
            {
                await WriteConsoleAsync(LogSourceType.PredictiveModel, $"futures iti trend delta model data loading complete - {e.EntityId.Symbol}:{e.EntityId.ValueDate:yyyy-MM-dd} ...");
                await CommandApi.TrainFuturesItiTrendDeltaModelAsync(e.EntityId.Symbol, e.EntityId.ValueDate, e.StartDate, e.EndDate, e.Statistics);
                await WriteConsoleAsync(LogSourceType.PredictiveModel, $"training futures iti trend delta model - {e.EntityId.Symbol}:{e.EntityId.ValueDate:yyyy-MM-dd} ...");
            }
            catch (Exception ex)
            {
                await WriteConsoleAsync(LogSourceType.PredictiveModel, 9991, ex.GetErrorMessage());
                Logger.LogError(ex.GetErrorMessage(), $"{LogSourceType.PredictiveModel}: futures iti trend model data loaded complete event - {e.EntityId.Symbol}:{e.EntityId.ValueDate:yyyy-MM-dd} handler failed");
            }
        }

        /// <summary>
        /// futures iti trend model data loaded
        /// </summary>
        /// <param name="e">futures iti trend model data loaded complete  event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesItiTrendClassModelDataLoadedCompleteEvent e)
        {
            try
            {
                await WriteConsoleAsync(LogSourceType.PredictiveModel, $"futures iti trend class model data loading complete - {e.EntityId.Symbol}:{e.EntityId.ValueDate:yyyy-MM-dd} ...");
                await CommandApi.TrainFuturesItiTrendClassModelAsync(e.EntityId.Symbol, e.EntityId.ValueDate, e.StartDate, e.EndDate, e.Statistics);
                await WriteConsoleAsync(LogSourceType.PredictiveModel, $"training futures iti trend class model - {e.EntityId.Symbol}:{e.EntityId.ValueDate:yyyy-MM-dd} ...");
            }
            catch (Exception ex)
            {
                await WriteConsoleAsync(LogSourceType.PredictiveModel, 9991, ex.GetErrorMessage());
                Logger.LogError(ex.GetErrorMessage(), $"{LogSourceType.PredictiveModel}: futures iti trend model data loaded complete event - {e.EntityId.Symbol}:{e.EntityId.ValueDate:yyyy-MM-dd} handler failed");
            }
        }

        /// <summary>
        /// futures iti trend model trained
        /// </summary>
        /// <param name="e">futures iti trend delta model trained complete event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesItiTrendDeltaModelTrainedCompleteEvent e)
            => await WriteConsoleAsync(LogSourceType.PredictiveModel, $"futures iti trend delta model training complete - {e.EntityId.Symbol}:{e.EntityId.ValueDate:yyyy-MM-dd} ");

        /// <summary>
        /// futures iti trend class model trained
        /// </summary>
        /// <param name="e">futures iti trend class model trained complete event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesItiTrendClassModelTrainedCompleteEvent e)
            => await WriteConsoleAsync(LogSourceType.PredictiveModel, $"futures iti trend class model training complete - {e.EntityId.Symbol}:{e.EntityId.ValueDate:yyyy-MM-dd} ");

    }

}
