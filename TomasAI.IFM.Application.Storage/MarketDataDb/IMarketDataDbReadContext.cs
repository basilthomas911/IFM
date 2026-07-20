using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb
{
    public interface IMarketDataDbReadContext : IMarketDataDbContext
    {
        Task<FuturesContractViewModel> GetCurrentTradedFuturesContractAsync();
        Task<IReadOnlyList<FuturesContractViewModel>> GetCurrentlyTradedFuturesContractsAsync();
        Task<FuturesContractViewModel> GetFuturesContractAsync(string contractId);
        Task<IReadOnlyList<FuturesContractViewModel>> GetFuturesContractsAsync();
        Task<FuturesOptionContractReadModel> GetFuturesOptionContractAsync(string contractId);
        Task<IReadOnlyList<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync(string contractId);
        Task<IReadOnlyList<NormalCurveDataReadModel>> GetNormalCurveDataAsync();
        Task<NormalCurveTableReadModel> GetNormalCurveTableAsync();
        Task<int> GetTradingDaysAsync(DateTime startDate, DateTime endDate, MarketType marketType, CurrencyType currencyType);
        Task<DateTime[]> GetTradingDatesAsync(DateTime startDate, DateTime endDate, MarketType marketType, CurrencyType currencyType);
        Task<FuturesEodDataViewModel> GetFuturesEodDataAsync(string contractId, DateTime valueDate);
        Task<FuturesEodMovingAverageReadModel> GetFuturesEodMovingAverageAsync(string symbol, DateTime startDate, DateTime endDate);
        Task<FuturesEodDataViewModel> GetCurrentFuturesEodDataAsync(DateTime valueDate);
        Task<IReadOnlyList<VixFuturesEodDataReadModel>> GetVixFuturesEodDataAsync(string contractId, DateTime valueDate);
        Task<IReadOnlyList<VixFuturesEodDataReadModel>> GetVixFuturesEodDataByValueDateAsync(DateTime valueDate);
        Task<VixFuturesEodDataReadModel> GetLastVixFuturesEodDataAsync(DateTime valueDate);
        Task<IReadOnlyList<FuturesEodDataViewModel>> GetFuturesEodDataByDateRangeAsync(string contractId, DateTime startDate, DateTime endDate);
        Task<IReadOnlyList<FuturesEodClosingPriceReadModel>> GetFuturesEodClosingPricesAsync(string symbol, DateTime startDate, DateTime endDate, int maxDays);
        Task<FuturesTickDataViewModel> GetLastFuturesTickDataAsync(string contractId);
        Task<FuturesTickDataViewModel> GetLastFuturesTickDataByTickDateAsync(string contractId, DateTime tickDate);
        Task<FuturesOptionTickDataViewModel> GetLastFuturesOptionTickDataAsync(string contractId);
        Task<YieldCurveRateReadModel> GetLastYieldCurveRateAsync();
        Task<YieldCurveRateReadModel> GetYieldCurveRateAsync(DateTime valueDate);
        Task<IReadOnlyList<YieldCurveRateReadModel>> GetYieldCurveRatesAsync(DateTime startDate, DateTime endDate);
        Task<IReadOnlyList<int>> GetYieldCurveRateYearsAsync();
        Task<RateOfReturnReadModel> GetRateOfReturnAsync(string symbol, DateTime valueDate);
        Task<RateOfReturnReadModel> GetLastRateOfReturnAsync(string symbol);
        Task<bool> GetFuturesContractExistsAsync(string contractId);
        Task<bool> GetFuturesOptionContractExistsAsync(string contractId);
        Task<bool> GetYieldCurveRateExistsAsync(DateTime valueDate);
        Task<IReadOnlyList<MarketExchangeReadModel>> GetMarketExchangesAsync();
        Task<IReadOnlyList<MarketVolatilityStrikePriceOffsetReadModel>> GetMarketVolatilityStrikePriceOffsetsAsync(string symbol);
        Task<IReadOnlyList<FuturesBarDataReadModel>> GetFuturesBarDataAsync(string contractId, string symbol, DateTime valueDate, DateTime startDate, DateTime endDate);
        Task<int> GetFuturesBarDataCountAsync(DateTime valueDate);
        Task<FuturesClosingPriceReadModel> GetYesterdaysFuturesClosingPriceAsync(FuturesDataId id);
        Task<FuturesClosingPriceReadModel> GetFuturesClosingPriceAsync(FuturesDataId e);
        Task<FuturesTradeSignalViewModel> GetLastFuturesTradeSignalAsync(string contractId, DateTime valueDate);
        Task<FuturesTradeSignalViewModel> GetLastFuturesTradeSignalBySymbolAsync(string symbol, DateTime valueDate);
        Task<IReadOnlyList<FuturesTradeSignalId>> GetFuturesTradeSignalIdByValueDateAsync(DateTime valueDate);
        Task<FuturesTrendDirectionReadModel> GetFuturesTrendDirectionFromRSISignalAsync(string contractId, DateTime valueDate, DateTime timestamp, int lookbackInterval, DateTime startTime, DateTime endTime);
        Task<FuturesRsiSignalReadModel> GetLastFuturesRsiSignalAsync(string contractId, DateTime valueDate, FuturesRsiSignalType signalType);
        Task<FuturesTdiSignalReadModel> GetLastFuturesTdiSignalAsync(string contractId, DateTime valueDate);
        Task<IReadOnlyList<FuturesTradeSignalLLMReadModel>> GetFuturesTradeSignalLLMByDateRangeAsync(string contractId, DateTime startDate, DateTime endDate);
        Task<IReadOnlyList<FuturesTradeSignalMetricsLLMReadModel>> GetFuturesTradeSignalMetricsLLMByDateRangeAsync(string contractId, DateTime startDate, DateTime endDate);
        Task<FuturesItiSignalViewModel> GetLastFuturesItiSignalAsync(string contractId, DateTime valueDate);
        Task<IReadOnlyList<FuturesItiSignalViewModel>> GetFuturesItiSignalsAsync(string contractId, DateTime valueDate);
        Task<IReadOnlyList<FuturesItiSignalViewModel>> GetFuturesItiSignalsAsync(string symbol, DateTime startDate, DateTime endDate);
        Task<IReadOnlyList<FuturesItiSignalViewModel>> GetFuturesItiTrendDirectionChangedSignalsAsync(string contractId, DateTime valueDate);
        Task<FuturesItiSignalViewModel> GetLastFuturesItiSignalTrendExtremeChangeAsync(string contractId, DateTime valueDate);
        Task<FuturesItiSignalViewModel> GetLastFuturesItiSignalTrendDirectionChangeAsync(string contractId, DateTime valueDate);
        Task<FuturesItiSignalViewModel> GetLastFuturesItiSignalTrendReversalChangeAsync(string contractId, DateTime valueDate);
        Task<IReadOnlyList<FuturesItiSignalViewModel>> GetFuturesItiSignalTrendDeltaDataAsync(string symbol, DateTime startDate, DateTime endDate);
        Task<IReadOnlyList<FuturesItiSignalViewModel>> GetFuturesItiSignalTrendClassDataAsync(string symbol, DateTime startDate, DateTime endDate);
        Task<IReadOnlyList<FuturesItiTrendDeltaDataReadModel>> GetFuturesItiTrendDeltaDataAsync(string symbol, DateTime startDate, DateTime endDate);
        Task<FuturesItiTrendDeltaModelReadModel> GetFuturesItiTrendDeltaModelAsync(string symbol, DateTime valueDate);
        Task<IReadOnlyList<FuturesItiTrendClassDataReadModel>> GetFuturesItiTrendClassDataAsync(string symbol, DateTime startDate, DateTime endDate);
        Task<FuturesItiTrendClassModelReadModel> GetFuturesItiTrendClassModelAsync(string symbol, DateTime valueDate);
        Task<double> GetFuturesItiSignalAverageTrendDeltaAsync(string contractId, DateTime valueDate);
        Task<FuturesItiSignalAveragePredictedTrendDeltaDataModel> GetFuturesItiSignalAveragePredictedTrendDeltaAsync(string contractId, DateTime valueDate);
        Task<FuturesItiSignalAverageRSIReadModel> GetFuturesItiSignalAverageRSIAsync(string contractId, DateTime valueDate);
        Task<IReadOnlyList<FuturesItiSignalMDIViewModel>> GetFuturesItiSignalMDIAsync(string contractId, DateOnly valueDate);
        Task<IReadOnlyList<FuturesItiSignalMDIViewModel>> GetFuturesItiSignalMDIByTrendAsync(string contractId, DateOnly valueDate, IntrinsicTimeTrendType intrinsicTimeTrend, int intrinsicTimeGroupId);
        Task<FuturesItiMDIDistributionReadModel> GetFuturesItiMDIDistributionAsync(string contractId, DateOnly valueDate);
        Task<FuturesItiMDIDistributionReadModel> GetFuturesItiMDIDistributionByTrendAsync(string contractId, DateOnly valueDate);
        Task<FuturesItiSignalAveragePredictedTrendDeltaRangeReadModel> GetFuturesItiSignalAveragePredictedTrendDeltaRangeAsync(string symbol, DateTime startDate, DateTime endDate);
        Task<FuturesItiSignalAverageTrendDeltaRangeReadModel> GetFuturesItiSignalAverageTrendDeltaRangeAsync(string symbol, DateTime startDate, DateTime endDate);
        Task<(string Symbol, DateTime StartDate, DateTime EndDate, double UpTrendDelta, double DownTrendDelta)>
            GetFuturesItiSignalTrendDeltaByDirectionChangedAsync(string symbol, DateTime startDate, DateTime endDate);
        Task<int> GetStreamingRequestIdAsync();
        Task<IReadOnlyList<FuturesOptionQuoteReadModel>> GetFuturesOptionQuotesAsync(Guid quoteId);
        Task<FuturesOptionQuoteDataReadModel> GetFuturesOptionQuoteDataAsync(FuturesOptionQuoteId quoteId);

    }
}
