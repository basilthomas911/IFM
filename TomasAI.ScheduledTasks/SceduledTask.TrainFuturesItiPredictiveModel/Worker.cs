using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Application.ServiceApi;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ServiceApi;

namespace ScheduledTask.TrainFuturesItiPredictiveModel
{
    public class Worker : BackgroundService
    {
        readonly IHost _host;
        readonly ILogger<Worker> _logger;
        readonly IMarketDataQueryApi _marketDataQueryApi;
        readonly IFuturesItiTrendCommandApi _commandApi;

        public Worker(
            IHost host,
            ILogger<Worker> logger,
            IMarketDataQueryApi marketDataQueryApi,
            IFuturesItiTrendCommandApi commandApi)
        {
            _host = IsArgumentNull.Set(host);
            _logger = IsArgumentNull.Set(logger);
            _marketDataQueryApi = IsArgumentNull.Set(marketDataQueryApi);
            _commandApi = IsArgumentNull.Set(commandApi);
        }

        /// <summary>
        /// start up IFM application services when futures market opens
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var testMode = true;
                _logger.LogInformation("loading value date...");
                var valueDateResult = await _marketDataQueryApi.GetValueDateAsync();
                if (valueDateResult.Success && valueDateResult.Value is not null || testMode)
                {
                    var valueDate = testMode 
                        ? DateTime.Now.Date
                        : valueDateResult.Value.Value;

                    _logger.LogInformation($"starting up Futures ITI Predictive Model Training for {valueDate:yyyy-mm-dd}... ");
                    var startDate = new DateTime(2023, 12, 1);
                    await _commandApi.BuildFuturesItiTrendModelAsync("ES",  valueDate, startDate, valueDate);
                }
                else
                    _logger.LogError($"unable to load value date due to {valueDateResult.ErrorMessage}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"unknown error due to {ex}");
            }
            finally
            {
                await _host.StopAsync();
            }
        }
    }
}