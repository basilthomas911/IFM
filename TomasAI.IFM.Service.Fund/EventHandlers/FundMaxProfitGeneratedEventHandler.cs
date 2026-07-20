using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.Fund.Events;
using TomasAI.IFM.Shared.Fund.ServiceApi;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Service.Fund.EventHandlers
{
    /// <summary>
    /// FundMaxProfitGeneratedEventHandler constructor
    /// </summary>
    /// <param name="marketDataAnalyticsQueryApi"></param>
    /// <param name="fundEventProducer"></param>
    /// <param name="fundQueryApi"></param>
    /// <param name="dataCacheService"></param>
    /// <param name="statusConsoleWriter"></param>
    /// <param name="logger"></param>
    public class FundMaxProfitGeneratedEventHandler(
        IMarketDataAnalyticsQueryApi marketDataAnalyticsQueryApi,
        IFundEventProducer fundEventProducer,
        IFundQueryApi fundQueryApi,
        IDataCacheService dataCacheService,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<FundMaxProfitGeneratedEventHandler> logger) : BaseEventServiceHandler(statusConsoleWriter),
        IAsyncEventHandler<FundMaxProfitGeneratedEvent, FundEventService>
    {
        readonly IMarketDataAnalyticsQueryApi _marketDataAnalyticsQueryApi = marketDataAnalyticsQueryApi;
        readonly IFundEventProducer _fundEventProducer = fundEventProducer;
        readonly IFundQueryApi _fundQueryApi = fundQueryApi;
        readonly IDataCacheService _dataCacheService = dataCacheService;
        readonly ILogger<FundMaxProfitGeneratedEventHandler> _logger = logger;

        /// <summary>
        /// generate fund max profit
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(FundMaxProfitGeneratedEvent e)
        {
            try
            {
                // get fund balance...
                var fundBalance = await GetFundBalanceAsync(e.FundOrder.FundId);

                // get futures trade signal...
                var tradeSignal = await GetFuturesTradeSignalAsync(e.FundOrder.BaseContractId, e.FundOrder.TradeDate);
                
                // set default fund risk from futures trade signal...
                var fundRiskPercent = 0.1;
                if (tradeSignal is not null)
                    fundRiskPercent = tradeSignal.FundRiskPercent;

                // get fund win loss ratio for current month...
                var fundWinLossRatio = 0.0;
                var kellyCriteria = 0.0;
                var fundWinLossRatioData = await GetFundWinLossRatioAsync(
                    fundId: e.FundOrder.FundId,
                    startDate: new DateOnly(e.FundOrder.TradeDate.Year, e.FundOrder.TradeDate.Month, 1),
                    endDate: new DateOnly(e.FundOrder.TradeDate.Year, e.FundOrder.TradeDate.Month, e.FundOrder.TradeDate.Day));
                if (fundWinLossRatioData is not null)
                {
                    fundWinLossRatio = fundWinLossRatioData.WinLossRatio;
                    kellyCriteria = fundWinLossRatioData.KellyCriteria;
                }

                // calculate fund drawdown percent...
                var fundDrawdownPercent = 0.0;
                var fundDrawdownBalances = await GetFundDrawdownBalancesAsync(e.FundOrder.FundId, e.FundOrder.TradeDate);
                if (fundDrawdownBalances is not null)
                    fundDrawdownPercent = (Convert.ToDouble(fundDrawdownBalances.EndBalance) - Convert.ToDouble(fundDrawdownBalances.StartBalance)) / Convert.ToDouble(fundDrawdownBalances.StartBalance);

                // if fund drawdown percent is < -5%, reduce fund risk percent by 50%...
                if (fundDrawdownPercent < -0.05)
                    fundRiskPercent = fundRiskPercent * 0.50;

                // if fund win loss ratio is less than one, reduce fund risk percent by 25%...
                else if (fundWinLossRatio > 0.0 && fundWinLossRatio < 1.0)
                    fundRiskPercent = fundRiskPercent * 0.75;

                // return minimum fund risk percent...
                fundRiskPercent = Math.Max(0.05, fundRiskPercent);

                // raise fund max profit generated complete event...
                var fundMaxProfit = (decimal )((double )fundBalance * fundRiskPercent);
                EventInitHelper.SetProperty(e, nameof(FundMaxProfitGeneratedEvent.FundMaxProfit), new FundMaxProfitReadModel(
                    fundOrderId: e.FundOrder.Id,
                    fundMaxProfit: fundMaxProfit,
                    fundRiskPercent: fundRiskPercent));
                //await _fundEventProducer.PostEventAsync(e.ToCompleteEvent());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"FundEventService.FundMaxProfitGeneratedEventHandler: {ex.Message}");
            }
        }

        /// <summary>
        /// return futures trade signal
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="tradeDate"></param>
        /// <returns></returns>
        async Task<FuturesTradeSignalV2ReadModel?> GetFuturesTradeSignalAsync(string contractId, DateOnly tradeDate)
        {
            FuturesTradeSignalV2ReadModel? tradeSignal = default;
            var srFuturesTradeSignal = await _marketDataAnalyticsQueryApi.GetFuturesTradeSignalAsync(contractId, tradeDate);
            if (srFuturesTradeSignal.Success)
                tradeSignal = srFuturesTradeSignal.Value;
            return tradeSignal;
        }

        /// <summary>
        /// return fund win loss ratio by date range
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name=""></param>
        /// <returns></returns>
        async Task<FundWinLossRatioReadModel?> GetFundWinLossRatioAsync(int fundId, DateOnly startDate, DateOnly endDate)
        {
            FundWinLossRatioReadModel? fundWinLossRatio = default;
            var srFundWinLossRatio = await _fundQueryApi.GetFundWinLossRatioAsync(
                   fundId: fundId,
                   startDate: startDate,
                   endDate: endDate);
            if (srFundWinLossRatio.Success && srFundWinLossRatio.Value is not null)
                fundWinLossRatio = srFundWinLossRatio.Value;
            return fundWinLossRatio;

        }

        /// <summary>
        /// return cached fund balance
        /// </summary>
        /// <param name="fundId"></param>
        /// <returns></returns>
        async Task<decimal> GetFundBalanceAsync(int fundId)
        {
            FundBalanceReadModel fundBalance;
            if (!_dataCacheService.Exists(DataCacheName.FundBalance, fundId))
            {
                var serviceResult = await _fundQueryApi.GetFundBalanceAsync(fundId);
                if (serviceResult.Success)
                {
                    fundBalance = serviceResult.Value;
                    _dataCacheService.Add(DataCacheName.FundBalance, fundId, fundBalance);
                }
            }
            fundBalance = _dataCacheService.Get<int, FundBalanceReadModel>(DataCacheName.FundBalance, fundId);
            return fundBalance?.Value ?? 0m;
        }

        /// <summary>
        /// return cached fund balance
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="tradeDate"></param>
        /// <returns></returns>
        async Task<FundDrawdownBalancesReadModel?> GetFundDrawdownBalancesAsync(int fundId, DateOnly tradeDate)
        {
            FundDrawdownBalancesReadModel? fundDrawdownBalances = default;
            var startDate = new DateOnly(tradeDate.Year, 1, 1);
            var endDate = new DateOnly(tradeDate.Year, 12, 31);
            var serviceResult = await _fundQueryApi.GetFundDrawdownBalancesAsync(fundId, startDate, endDate);
            if (serviceResult.Success)
                fundDrawdownBalances = serviceResult.Value;
            return fundDrawdownBalances;
        }

        /// <summary>
        /// return fund high risk percent 
        /// </summary>
        /// <param name="fundOrder"></param>
        /// <returns></returns>
        double GetFundHighRiskPercent(FundOrderReadModel fundOrder)
            => GetTradeTypeFromFundOrder(fundOrder) switch {
                TradeType.ShortIronCondor => 0.75,
                TradeType.LongIronCondor => 0.40,
                _ => 0
            };

        /// <summary>
        /// return fund medium risk percent 
        /// </summary>
        /// <param name="fundOrder"></param>
        /// <returns></returns>
        double GetFundMediumRiskPercent(FundOrderReadModel fundOrder)
            => GetTradeTypeFromFundOrder(fundOrder) switch {
                TradeType.ShortIronCondor => 0.50,
                TradeType.LongIronCondor => 0.275,
                _ => 0
            };

        /// <summary>
        /// return fund low risk percent 
        /// </summary>
        /// <param name="fundOrder"></param>
        /// <returns></returns>
        double GetFundLowRiskPercent(FundOrderReadModel fundOrder)
            => GetTradeTypeFromFundOrder(fundOrder) switch {
                TradeType.ShortIronCondor => 0.25,
                TradeType.LongIronCondor => 0.15,
                _ => 0
            };

        /// <summary>
        /// return trade type from primary trade in fund order
        /// </summary>
        /// <param name="fundOrder"></param>
        /// <returns></returns>
        TradeType GetTradeTypeFromFundOrder(FundOrderReadModel fundOrder)
            => fundOrder.Trades.Length > 0
                ? fundOrder.Trades[0].TradeType
                : TradeType.Unknown;

    }
}
