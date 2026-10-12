using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

/// <summary>Reads persisted futures and option ticks from their raw ScyllaDB owner.</summary>
public interface ITradeTickQuoteDbReadContext
{
    Task<FuturesTickDataV2ReadModel?> GetFuturesTickDataAsync(FuturesTickDataId id);
    Task<FuturesTickDataV2ReadModel?> GetLastFuturesTickDataAsync(string contractId, DateOnly valueDate);
    Task<FuturesTickDataV2ReadModel?> GetFuturesTickAtOrBeforeAsync(string contractId, DateOnly valueDate, TimeOnly tickTime, CancellationToken token = default);
    Task<FuturesTickDataId?> GetLastFuturesTickDataIdAsync(string contractId, DateOnly valueDate);
    Task<FuturesOptionTickDataId?> GetLastFuturesOptionTickDataIdAsync(string contractId, DateOnly valueDate);
    Task<FuturesOptionTickDataV2ReadModel?> GetFuturesOptionTickDataAsync(FuturesOptionTickDataId id);
    Task<FuturesOptionTickDataV2ReadModel?> GetLastFuturesOptionTickDataAsync(string contractId, DateOnly valueDate);
    Task<FuturesTickHLVDataReadModel?> GetFuturesTickHLVDataAsync(FuturesDataId id);
}
