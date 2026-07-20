using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Application.ServiceApi;
using TomasAI.IFM.Shared.Fund.ServiceApi;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Shared.EventQueue;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.Application.ScheduledTask.MarketClose
{
    public class Worker : BackgroundService
    {
        readonly IHost _host;
        readonly ILogger<Worker> _logger;
        readonly IFundQueryApi _fundQueryApi;
        readonly IMarketDataQueryApi _marketDataQueryApi;
        readonly IMarketDataFeedQueryApi _marketDataFeedQueryApi;
        readonly ITradeCommandApi _tradeCommandApi;
        readonly IApplicationCommandApi _applicationCommandApi;
        readonly ISystemAdminCommandApi _systemAdminCommandApi;
        readonly ISystemAdminQueryApi _systemAdminQueryApi;
        readonly ISystemAdminUIEventConsumer _systemAdminEventConsumer;

        public Worker(
            IHost host,
            ILogger<Worker> logger,
            IFundQueryApi fundQueryApi,
            IMarketDataQueryApi marketDataQueryApi,
            IMarketDataFeedQueryApi marketDataFeedQueryApi,
            ITradeCommandApi tradeCommandApi,
            IApplicationCommandApi applicationCommandApi,
            ISystemAdminCommandApi systemAdminCommandApi,
            ISystemAdminQueryApi systemAdminQueryApi,
            ISystemAdminUIEventConsumer systemAdminEventConsumer)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fundQueryApi = fundQueryApi ?? throw new ArgumentNullException(nameof(fundQueryApi));
            _marketDataQueryApi = marketDataQueryApi ?? throw new ArgumentNullException(nameof(marketDataQueryApi));
            _marketDataFeedQueryApi = marketDataFeedQueryApi ?? throw new ArgumentNullException(nameof(marketDataFeedQueryApi));
            _tradeCommandApi = tradeCommandApi ?? throw new ArgumentNullException(nameof(tradeCommandApi));
            _applicationCommandApi = applicationCommandApi ?? throw new ArgumentNullException(nameof(applicationCommandApi));
            _systemAdminCommandApi = systemAdminCommandApi ?? throw new ArgumentNullException(nameof(systemAdminCommandApi));
            _systemAdminQueryApi = systemAdminQueryApi ?? throw new ArgumentNullException(nameof(systemAdminQueryApi));
            _systemAdminEventConsumer = systemAdminEventConsumer ?? throw new ArgumentNullException(nameof(systemAdminEventConsumer));
        }

        /// <summary>
        /// process end of day scheduled task
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await ProcessOpenTradesAsync();
                await ShutdownApplicationAsync();
                await BackupDatabasesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"end of day scheduled task failed due to {ex}");
            }
            finally
            {
                await _host.StopAsync();
            }
            return;

            async Task ProcessOpenTradesAsync()
            {
                var valueDate = await GetValueDateAsync();
                if (valueDate.HasValue)
                {
                    var baseContracts = await GetBaseContractsAsync();
                    if (baseContracts is not null)
                    {
                        var productionFunds = await GetProductionFundsAsync();
                        if (productionFunds is not null)
                        {
                            foreach (var productionFund in productionFunds)
                            {
                                var openFundOrders = await GetOpenFundOrdersAsync(productionFund);
                                if (openFundOrders is null)
                                    continue;
                                foreach (var openFundOrder in openFundOrders)
                                {
                                    var fundOrderTrade = await GetOpenFundOrderTradeAsync(openFundOrder);
                                    if (fundOrderTrade is null)
                                        continue;
                                    var baseContract = baseContracts.Where(e => e.Symbol == fundOrderTrade.BaseContractSymbol).FirstOrDefault();
                                    var futuresEodData = await GetFuturesEodDataAsync(baseContract?.ContractId, valueDate.Value);
                                    if (futuresEodData is null)
                                        continue;
                                    var serviceResult = await _tradeCommandApi.ProcessEndOfDayAsync(
                                        orderId: fundOrderTrade.OrderId,
                                        tradeId: fundOrderTrade.TradeId,
                                        tradeType: fundOrderTrade.TradeType,
                                        valueDate: valueDate.Value,
                                        tradeStatus: TradeStatus.EndOfDay,
                                        openPrice: futuresEodData.OpenPrice,
                                        highPrice: futuresEodData.HighPrice,
                                        lowPrice: futuresEodData.LowPrice,
                                        closePrice: futuresEodData.ClosePrice,
                                        volume: futuresEodData.Volume,
                                        reference: fundOrderTrade.Reference ?? String.Empty);
                                    if (serviceResult.Success)
                                        _logger.LogInformation($"successfully processed end of day for trade {fundOrderTrade.OrderId}:{fundOrderTrade.TradeId}");
                                    else
                                        _logger.LogError($"failed to process end of day for {fundOrderTrade.OrderId}:{fundOrderTrade.TradeId} due to {serviceResult.ErrorMessage}");
                                }
                            }
                        }
                    }
                }

            }

            async Task BackupDatabasesAsync()
            {
                _logger.LogInformation("loading database backup names...");
                var servicerResultDatabaseNames = await _systemAdminQueryApi.GetDatabaseNamesAsync();
                if (servicerResultDatabaseNames.Success)
                {
                    var infoMsgQueue = new ConcurrentEventQueue<string>(infoMsg => _logger.LogInformation(infoMsg));
                    infoMsgQueue.Start();
                    var databaseNames = servicerResultDatabaseNames.Value.Names;
                    var completedNames = new List<string>(databaseNames);
                    await _systemAdminEventConsumer.StartAsync(
                        backupAction: e => infoMsgQueue.EnqueueAndSignal($"{e.DatabaseName}: executing database backup"),
                        infoMsgAction: e => infoMsgQueue.EnqueueAndSignal($"{e.DatabaseName}: {e.InfoMessage}"),
                        completedAction: e => completedNames.Remove(e.DatabaseName),
                        failedAction: e => {
                            completedNames.Remove(e.DatabaseName);
                            infoMsgQueue.EnqueueAndSignal($"{e.DatabaseName}: database backup failed due to {e.ErrorMessage}");
                        });
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    foreach (var databaseName in databaseNames)
                    {
                        var backupType = DateTime.Now.DayOfWeek == DayOfWeek.Friday ? DatabaseBackupType.Full : DatabaseBackupType.Diff;
                        var commandTimeout = DateTime.Now.DayOfWeek == DayOfWeek.Friday ? TimeSpan.FromMinutes(60).Seconds : TimeSpan.FromMinutes(15).Seconds;
                        var serviceResult = await _systemAdminCommandApi.BackupDatabaseAsync(databaseName, backupType, commandTimeout);
                        if (!serviceResult.Success)
                            _logger.LogInformation($"{databaseName}: database backup failed due to {serviceResult.ErrorMessage}");
                    }
                    while (completedNames.Count > 0)
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    await _systemAdminEventConsumer.StopAsync();
                    infoMsgQueue.Stop();
                }
                else
                    _logger.LogError($"unable to load database backup names due to {servicerResultDatabaseNames.ErrorMessage}");
            }

            async Task ShutdownApplicationAsync()
            {
                var shutdownAppResult = await _applicationCommandApi.ShutdownApplicationAsync();
                if (shutdownAppResult.Success)
                    _logger.LogInformation("successfully executed application shutdown command");
                else
                    _logger.LogError($"failed to execute application shutdown command due to {shutdownAppResult.ErrorMessage}");
            }

        }

        private async Task<DateTime?> GetValueDateAsync()
        {
            var valueDate = default(DateTime?);
            _logger.LogInformation($"loading value date...");
            var valueDateResult = await _marketDataQueryApi.GetValueDateAsync();
            if (valueDateResult.Success && valueDateResult.Value is not null)
                valueDate = valueDateResult.Value.Value;
            else
                _logger.LogError($"end of day scheduled task runs only within trading hours...");
            return valueDate;
        }

        private async Task<List<FuturesContractViewModel>> GetBaseContractsAsync()
        {
            var futuresContracts = default(List<FuturesContractViewModel>);
            _logger.LogInformation($"loading base contracts...");
            var futuresContractsResult = await _marketDataQueryApi.GetCurrentlyTradedFuturesContractsAsync();
            if (futuresContractsResult.Success)
                futuresContracts = futuresContractsResult.Value.ToList();
            else
                _logger.LogError($"zero base contracts found");
            return futuresContracts;
        }

        private async Task<List<FundReadModel>> GetProductionFundsAsync()
        {
            var productionFunds = default(List<FundReadModel>);
            _logger.LogInformation($"loading production funds...");
            var fundsResult = await _fundQueryApi.GetFundsAsync();
            if (fundsResult.Success && fundsResult.Value?.Length > 0)
                productionFunds = fundsResult.Value.Where(e => e.IsProduction).ToList();
            else
                _logger.LogInformation($"zero production funds found");
            return productionFunds;
        }

        private async Task<List<FundOrderReadModel>> GetOpenFundOrdersAsync(FundReadModel productionFund)
        {
            var fundOrders = default(List<FundOrderReadModel>);
            _logger.LogInformation($"loading open fund orders for fund {productionFund.Name}...");
            var fundOrdersResult = await _fundQueryApi.GetFundOrdersAsync(productionFund.FundId);
            if (fundOrdersResult.Success && fundOrdersResult.Value?.Length > 0)
                fundOrders = fundOrdersResult.Value.Where(e => e.OrderStatus == Shared.Fund.OrderStatus.Open).ToList();
            else
                _logger.LogInformation($"zero open fund order found for fund {productionFund.Name}");
            return fundOrders;
        }

        private async Task<FundOrderTradeReadModel> GetOpenFundOrderTradeAsync(FundOrderReadModel openFundOrder)
        {
            var fundOrderTrade = default(FundOrderTradeReadModel);
            _logger.LogInformation($"loading fund order trades for open fund order {openFundOrder.Reference ?? String.Empty}...");
            var fundOrderTradesResult = await _fundQueryApi.GetFundOrderTradesAsync(openFundOrder.OrderId);
            if (fundOrderTradesResult.Success && fundOrderTradesResult.Value?.Length > 0)
                fundOrderTrade = fundOrderTradesResult.Value.Where(e => e.TradeState == TradeState.TradeToOpen).SingleOrDefault();
            else
                _logger.LogInformation($"zero fund order trades found found for open fund order {openFundOrder.Reference ?? String.Empty}");
            return fundOrderTrade;
        }

        private async Task<FuturesEodDataViewModel> GetFuturesEodDataAsync(string contractId, DateTime valueDate)
        {
            var futuresEodData = default(FuturesEodDataViewModel);
            if (contractId is not null)
            {
                _logger.LogInformation($"loading futures eod data for {contractId}...");
                var futuresEodDataResult = await _marketDataFeedQueryApi.GetFuturesEodDataAsync(contractId, valueDate);
                if (futuresEodDataResult.Success && futuresEodDataResult.Value is not null)
                    futuresEodData = futuresEodDataResult.Value;
                else
                    _logger.LogInformation($"zero futures eod data loaded for {contractId}");
            }
            else
                _logger.LogError($"empty base contract id");
            return futuresEodData;
        }

    }
}
