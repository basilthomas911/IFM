using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Application.ServiceApi;

namespace TomasAI.IFM.Application.ScheduledTask.FuturesMarketOpen
{
    public class Worker : BackgroundService
    {
        readonly IHost _host;
        readonly ILogger<Worker> _logger;
        readonly IMarketDataQueryApi _marketDataQueryApi;
        readonly IApplicationCommandApi _appCommandApi;

        public Worker(
            IHost host,
            ILogger<Worker> logger,
            IMarketDataQueryApi marketDataQueryApi,
            IApplicationCommandApi appCommandApi)
        {
            _host = IsArgumentNull.Set(host);
            _logger = IsArgumentNull.Set(logger);
            _marketDataQueryApi = IsArgumentNull.Set(marketDataQueryApi);
            _appCommandApi = IsArgumentNull.Set(appCommandApi);
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
                _logger.LogInformation("loading value date...");
                var valueDateResult = await _marketDataQueryApi.GetValueDateAsync();
                if (valueDateResult.Success && valueDateResult.Value is not null)
                {
                    var valueDate = valueDateResult.Value.Value;
                    _logger.LogInformation($"starting up IFM application services on {valueDate:yyyy-mm-dd}... ");
                    await _appCommandApi.StartApplicationAsync();
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    _logger.LogInformation("IFM application services started ");
                    _logger.LogInformation($"loaded value date for {valueDate:yyyy-MM-dd}");
                    _logger.LogInformation("loading currently traded futures contracts...");
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