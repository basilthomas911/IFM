using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

/// <summary>Direct ingestion persistence; these observations do not append business event-log entries.</summary>
public interface ITradeTickQuoteDbWriteContext
{
    Task InsertTickTradeDataAsync(FuturesTickTradeDataInsertedEvent value);
    Task InsertTickQuoteDataAsync(FuturesTickQuoteDataInsertedEvent value);
    Task InsertFuturesTickDataAsync(FuturesTickDataV2ReadModel value);
    Task InsertFuturesTickDataAsync(ICollection<FuturesTickDataV2ReadModel> values);
    Task InsertFuturesOptionTickDataAsync(FuturesOptionTickDataV2ReadModel value);
    Task InsertFuturesOptionTickDataAsync(ICollection<FuturesOptionTickDataV2ReadModel> values);
    Task DeleteFuturesTickDataAsync(string contractId, DateOnly valueDate);
    Task DeleteFuturesOptionTickDataAsync(string contractId, DateOnly valueDate);
}
