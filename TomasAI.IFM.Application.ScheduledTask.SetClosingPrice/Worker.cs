using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ServiceApi;

namespace TomasAI.IFM.Application.ScheduledTask.SetClosingPrice
{
    public class Worker : BackgroundService
    {
        readonly IHost _host;
        readonly ILogger<Worker> _logger;
        readonly IMarketDataFeedCommandApi _marketDataFeedCommandApi;
        readonly ITradePlacementCommandApi _tradePlacementCommandApi;
        readonly IMarketDataFeedQueryApi _marketDataFeedQueryApi;
        readonly IMarketDataQueryApi _marketDataQueryApi;

        public Worker(
            IHost host,
            ILogger<Worker> logger,
            IMarketDataFeedCommandApi marketDataFeedCommandApi,
            ITradePlacementCommandApi tradePlacementCommandApi,
            IMarketDataFeedQueryApi marketDataFeedQueryApi,
            IMarketDataQueryApi marketDataQueryApi)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _marketDataFeedCommandApi = marketDataFeedCommandApi ?? throw new ArgumentNullException(nameof(marketDataFeedCommandApi));
            _tradePlacementCommandApi = tradePlacementCommandApi ?? throw new ArgumentNullException(nameof(tradePlacementCommandApi));
            _marketDataFeedQueryApi = marketDataFeedQueryApi ?? throw new ArgumentNullException(nameof(marketDataFeedQueryApi));
            _marketDataQueryApi = marketDataQueryApi ?? throw new ArgumentNullException(nameof(marketDataQueryApi));
        }

        /// <summary>
        /// set closing price for all currently trade futures contracts
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation($"loading value date...");
                var valueDateResult = await _marketDataQueryApi.GetValueDateAsync();
                if (valueDateResult.Success && valueDateResult.Value is not null)
                {
                    var valueDate = valueDateResult.Value.Value;
                    _logger.LogInformation($"loaded value date for {valueDate:yyyy-MM-dd}");
                    _logger.LogInformation("loading currently traded futures contracts...");
                    var futuresContractsResult = await _marketDataQueryApi.GetCurrentlyTradedFuturesContractsAsync();
                    if (futuresContractsResult.Success)
                    {
                        var futuresContracts = futuresContractsResult.Value;
                        if (futuresContracts is not null && futuresContracts.Length > 0)
                        {
                            // assume normal trading hours closing of 4:00pm...
                            var tickDate = valueDate.Date.AddHours(16);
                            _logger.LogInformation($"loaded {futuresContracts.Length} futures contracts");
                            foreach (var e in futuresContracts)
                            {
                                var futuresTickDataResult = await _marketDataFeedQueryApi.GetLastFuturesTickDataAsync(e.ContractId, tickDate);
                                if (futuresTickDataResult.Success && futuresTickDataResult.Value is not null)
                                {
                                    var closingPrice = futuresTickDataResult.Value.Price;
                                    var futuresClosingPriceId = new FuturesClosingPriceId(e.ContractId, tickDate.Date);
                                    var setClosingPriceCommandResult = await _marketDataFeedCommandApi.InsertFuturesClosingPriceAsync(futuresClosingPriceId, closingPrice);
                                    if (setClosingPriceCommandResult.Success)
                                        _logger.LogInformation($"futures {e.ContractId} @ {closingPrice:F2} closing price inserted");
                                    else
                                        _logger.LogError($"futures {e.ContractId} @ {closingPrice:F2} closing price insert failed due to {setClosingPriceCommandResult.ErrorMessage}");
                                }
                                else
                                    _logger.LogError($"unable to load futures {e.ContractId} closing price due to {futuresTickDataResult.ErrorMessage}");
                                if (e.ContractId.StartsWith("ES"))
                                {
                                    _logger.LogInformation($"stopping trade placement service for {e.ContractId}...");
                                    var tradePlacementId = TradePlacementId.Create(e.ContractId, valueDate);
                                    var tradePlacementCommandResult = await _tradePlacementCommandApi.StopTradePlacementAsync(tradePlacementId);
                                    if (tradePlacementCommandResult.Success)
                                        _logger.LogInformation($"trade placement service stopped");
                                    else
                                        _logger.LogError($"trade placement service stopped failed due to {tradePlacementCommandResult.ErrorMessage}");
                                }

                            }
                        }
                        else
                            _logger.LogError($"unable to load any currently traded futures contracts");
                    }
                    else
                        _logger.LogError($"unable to load any currently traded futures contracts due to {futuresContractsResult.ErrorMessage}");
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