using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;

public interface IMarketDataFeedQueryApi
{
    Task<ServiceResult<FuturesTickDataV2ReadModel>> GetLastFuturesTickDataAsync(string contractId, DateOnly valueDate);
    Task<ServiceResult<FuturesTickDataV2ReadModel>> GetLastFuturesTickDataAsync(string contractId, DateTime tickDate);
    Task<ServiceResult<FuturesOptionTickDataV2ReadModel>> GetLastFuturesOptionTickDataAsync(string contractId, DateOnly valueDate);
    Task<ServiceResult<FuturesEodDataV2ReadModel>> GetFuturesEodDataAsync(string contractId, DateOnly valueDate);
    Task<ServiceResult<FuturesEodDataV2ReadModel>> GetLastFuturesEodDataAsync(string contractId, DateOnly valueDate);
    Task<ServiceResult<FuturesBarDataReadModel>> GetLastFuturesBarDataAsync(string contractId, string symbol, DateOnly valueDate);
    Task<ServiceResult<FuturesEodDataMovingAveragesReadModel>> GetFuturesEodMovingAveragesAsync(string contractId, string symbol, DateOnly valueDate);
    Task<ServiceResult<VixFuturesEodDataReadModel[]>> GetVixFuturesEodDataAsync(string contractId, DateOnly valueDate);
    Task<ServiceResult<VixFuturesEodDataReadModel>> GetLastVixFuturesEodDataAsync(string contractId, DateOnly valueDate);
    Task<ServiceResult<FuturesEodDataV2ReadModel[]>> GetFuturesEodDataAsync(string contractId, DateOnly startDate, DateOnly endDate);
    Task<ServiceResult<FuturesBarDataReadModel[]>> GetFuturesBarDataAsync(string contractId, string symbol, DateOnly valueDate, DateTime startDate, DateTime endDate);
    Task<ServiceResult<IronCondorMarketDataFeedReadModel>> GetIronCondorMarketDataFeedAsync(
           string underlyingContractId,
           string shortPutOptionContractId,
           string longPutOptionContractId,
           string shortCallOptionContractId,
           string longCallOptionContractId,
           DateOnly valueDate);
    Task<ServiceResult<FuturesEodDataParametersReadModel>> GetFuturesEodDataParametersAsync(string contractId, DateOnly valueDate);
    Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(string contractId, FuturesOptionContractReadModel queryForContract);
    Task<ServiceResult<FuturesOptionSpreadDataReadModel>> GetFuturesOptionSpreadDataAsync(DateOnly valueDate, DateOnly maturityDate, double assetPrice, 
        double riskFreeRate, double timeValue, FuturesOptionContractReadModel qfShortOptionContract, FuturesOptionContractReadModel qfLongOptionContract);
    Task<ServiceResult<NormalCurveTableReadModel>> GetNormalCurveTableAsync();
    Task<ServiceResult<RiskPositionTypeReadModel>> GetFuturesRiskPositionTypeAsync(DateOnly valueDate, TradeType tradeType);
    Task<ServiceResult<ScalarValue<int>>> GetStreamingRequestIdAsync();
    Task<ServiceResult<ScalarValue<int>>> GetOptionQuoteIdAsync();
}
