using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;

public interface IMarketDataFeedCommandApi
{
    Task<ServiceResult<Guid>> DeleteStreamingRequestIdAsync(FeedId streamId);
   Task<ServiceResult<Guid>> StartMarketDataFeedAsync(ICollection<FuturesContractV2ReadModel> futuresContracts, DateOnly valueDate);
    Task<ServiceResult<Guid>> StopMarketDataFeedAsync(DateOnly valueDate);
    Task<ServiceResult<Guid>> ResetMarketDataFeedAsync(ICollection<FuturesContractV2ReadModel> futuresContracts, DateOnly valueDate);

    Task<ServiceResult<Guid>> AddTradeLiveFeedAsync(int orderId, int tradeId, DateOnly valueDate);
    Task<ServiceResult<Guid>> RemoveTradeLiveFeedAsync(int orderId, int tradeId, DateOnly valueDate);
    Task<ServiceResult<Guid>> RemoveTradeLiveFeedsAsync(int orderId);
    Task<ServiceResult<Guid>> HaltTradeLiveFeedAsync(int orderId, int tradeId);
    Task<ServiceResult<Guid>> EnableTradeLiveFeedAsync(int orderId, int tradeId);
    Task<ServiceResult<Guid>> DisableTradeLiveFeedAsync(int orderId, int tradeId);

    Task<ServiceResult<Guid>> InsertFuturesTickDataAsync(FuturesContractV2ReadModel futuresContract, FuturesTickDataV2ReadModel futuresTickData);
    Task<ServiceResult<Guid>> InsertFuturesOptionTickDataAsync(FuturesContractV2ReadModel futuresContract, FuturesOptionTickDataV2ReadModel futuresOptionTickData);
    Task<ServiceResult<Guid>> InsertFuturesEodDataAsync(DateOnly valueDate, 
        FuturesTickDataV2ReadModel futuresTickData, 
        FuturesContractV2ReadModel contract,
        FuturesEodDataV2ReadModel eodDataToday, 
        ICollection<FuturesEodDataV2ReadModel> eodDataRange, 
        NormalCurveTableReadModel normCurveData, 
        int windowSize,
        ICollection<VixFuturesEodDataReadModel> vixEodData);
    Task<ServiceResult<Guid>> DeleteFuturesBarDataAsync(FuturesBarDataId id);
    Task<ServiceResult<Guid>> InsertFuturesBarDataAsync(FuturesBarDataReadModel futuresBarData);
    Task<ServiceResult<Guid>> InsertVixFuturesEodDataAsync(FuturesTickDataV2ReadModel vixFuturesTickData);
    Task<ServiceResult<Guid>> InsertFuturesClosingPriceAsync(FuturesDataId id, decimal closingPrice);
    Task<ServiceResult<Guid>> InsertFuturesOptionQuoteDataAsync(int quoteId, string contractId, QuoteData quoteData);

    Task<ServiceResult<Guid>> StartFuturesOptionTickDataStreamingAsync(FuturesOptionTickEntityId entityId, FuturesOptionContractReadModel futuresOptionContract, FuturesContractV2ReadModel baseContract, DateOnly valueDate, DateOnly maturityDate, double riskFreeRate);
    Task<ServiceResult<Guid>> StopFuturesOptionTickDataStreamingAsync(FuturesOptionTickEntityId entityId, string contractId);
    Task<ServiceResult<Guid>> StartFuturesTickDataStreamingAsync(FuturesContractV2ReadModel futuresContract, DateOnly valueDate, bool resetStream);
    Task<ServiceResult<Guid>> StopFuturesTickDataStreamingAsync(string contractId, DateOnly valueDate);
    Task<ServiceResult<Guid>> StartFuturesBarDataStreamingAsync(FuturesContractV2ReadModel[] contracts, DateOnly valueDate);
    Task<ServiceResult<Guid>> StopFuturesBarDataStreamingAsync(DateOnly valueDate);
    Task<ServiceResult<Guid>> StartFuturesOptionQuoteDataStreamingAsync(int quoteId, FuturesOptionQuoteReadModel[] futuresOptionQuotes, FuturesOptionContractReadModel[] futuresOptionContracts);
    Task<ServiceResult<Guid>> StopFuturesOptionQuoteDataStreamingAsync(int quoteId);

}
