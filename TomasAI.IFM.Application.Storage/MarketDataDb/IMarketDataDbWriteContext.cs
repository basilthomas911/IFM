using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Application.Storage.MarketDataDb
{
    public interface IMarketDataDbWriteContext : IMarketDataDbContext
    {
        Task DeleteFuturesBarDataAsync(FuturesBarDataId id);
        Task DeleteFuturesContractAsync(string contractId);
        Task DeleteFuturesOptionContractAsync(string contractId);
        Task DeleteFuturesOptionQuotesAsync(int quoteId);
        Task DeleteYieldCurveRateAsync(DateTime valueDate);
        Task DeleteTradeLiveFeedAsync(int orderId, int tradeId);
        Task DeleteStreamingRequestIdAsync(int requestId);
        Task InsertFuturesRsiSignalAsync(FuturesRsiSignalReadModel futuresRsiSignal);
        Task InsertFuturesTdiSignalAsync(FuturesTdiSignalReadModel futuresTdiSignal);
        Task InsertFuturesTradeSignalAsync(FuturesTradeSignalViewModel futuresTradeSignalViewModel);
        Task InsertFuturesClosingPriceAsync(FuturesClosingPriceReadModel futuresClosingPrice);
        Task InsertFuturesContractAsync(FuturesContractViewModel futuresContract);
        Task InsertFuturesOptionContractAsync(FuturesOptionContractReadModel futuresOptionContract);
        Task InsertFuturesOptionContractsAsync(FuturesOptionContractReadModel[] futuresOptionContracts);
        Task InsertFuturesBarDataAsync(FuturesBarDataReadModel barData);
        Task InsertFuturesEodDataAsync(FuturesEodDataViewModel eodData);
        Task InsertVixFuturesEodDataAsync(FuturesTickDataViewModel vixTickData);
        Task InsertFuturesTickDataAsync(FuturesTickDataViewModel tickData);
        Task InsertFuturesTickDataAsync(ICollection<FuturesTickDataViewModel> tickData);
        Task InsertFuturesOptionTickDataAsync(FuturesOptionTickDataViewModel tickData);
        Task InsertFuturesOptionTickDataAsync(ICollection<FuturesOptionTickDataViewModel> tickData);
        Task InsertFuturesOptionQuoteAsync(ICollection<FuturesOptionQuoteReadModel> quotes, ICollection<FuturesOptionQuoteDataReadModel> quoteData);
        Task InsertFuturesOptionQuoteDataAsync(FuturesOptionQuoteDataReadModel quoteData);
        Task InsertStreamingDataLogAsync(DateTime valueDate, int errorCode, string errorMessage);
        Task InsertYieldCurveRateAsync(YieldCurveRateReadModel yieldCurveRate);
        Task InsertYieldCurveRatesAsync(ICollection<YieldCurveRateReadModel> yieldCurveRates);
        Task InsertTradeLiveFeedAsync(TradeLiveFeedReadModel tradeLiveFeed);
        Task InsertRateOfReturnAsync(RateOfReturnReadModel rateOfReturn);
        Task InsertFuturesTradeSignalLLMAsync(FuturesTradeSignalLLMReadModel e);
        Task InsertFuturesTradeSignalMetricsLLMAsync(FuturesTradeSignalMetricsLLMReadModel e);
        Task InsertFuturesItiSignalAsync(FuturesItiSignalViewModel e);
        Task InsertFuturesItiTrendDeltaModelAsync(FuturesItiTrendDeltaModelReadModel e);
        Task InsertFuturesItiTrendClassModelAsync(FuturesItiTrendClassModelReadModel e);
        Task<FuturesItiTrendModelDataStatistics> LoadFuturesItiTrendDeltaDataAsync(string symbol, DateTime startDate, DateTime endDate);
        Task<FuturesItiTrendModelDataStatistics> LoadFuturesItiTrendClassDataAsync(string symbol, DateTime startDate, DateTime endDate);
        Task ReplaceFuturesContractAsync(string contract, FuturesContractViewModel futuresContract);
        Task ReplaceFuturesOptionContractAsync(string contractId, FuturesOptionContractReadModel e);
        Task UpdateFuturesEodDataNearestStrikesAsync(string contractId, DateTime valueDate, int nearestPutStrike, int nearestCallStrike);
        Task UpdateFuturesEodDataAsync(string contractId, DateTime valueDate, double openPrice, double highPrice, double lowPrice, double closePrice, int volume);
        Task UpdateFuturesContractAsync(string originalContractId, FuturesContractViewModel futuresContract);
        Task UpdateFuturesOptionContractAsync(string originalContractId, FuturesOptionContractReadModel futuresOptionContract);
        Task UpdateYieldCurveRateAsync(YieldCurveRateReadModel yieldCurveRate);
        new Task BackupDatabaseAsync(DatabaseBackupType backupType, int commandTimeout, Action<string> onInfoMessage);
    }
}
