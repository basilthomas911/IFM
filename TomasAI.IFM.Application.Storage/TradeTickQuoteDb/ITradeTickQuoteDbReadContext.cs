using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

/// <summary>Reads persisted futures and option ticks from their raw ScyllaDB owner.</summary>
public interface ITradeTickQuoteDbReadContext
{
    /// <summary>Reads a bounded newest-first raw trade window, preserving source timestamps.</summary>
    Task<IReadOnlyList<BootstrapTradeRow>> GetLatestRawFuturesTradesAsync(string contractId, DateOnly throughDate, CancellationToken token = default);

    /// <summary>Gets the futures tick with the specified persisted identity.</summary>
    /// <param name="id">The persisted tick or futures contract/value-date identity.</param>
    /// <returns>The stored result, or null when the scope contains no matching observations.</returns>
    Task<FuturesTickDataV2ReadModel?> GetFuturesTickDataAsync(FuturesTickDataId id);
    /// <summary>Gets the last persisted futures tick in the contract/value-date scope.</summary>
    /// <param name="contractId">The persisted contract identifier.</param>
    /// <param name="valueDate">The requested partition value date.</param>
    /// <returns>The stored result, or null when the scope contains no matching observations.</returns>
    Task<FuturesTickDataV2ReadModel?> GetLastFuturesTickDataAsync(string contractId, DateOnly valueDate);
    /// <summary>Gets the latest tick at or before the supplied time within one value date.</summary>
    /// <param name="contractId">The persisted contract identifier.</param>
    /// <param name="valueDate">The requested partition value date.</param>
    /// <param name="tickTime">The inclusive time cutoff within the value date.</param>
    /// <param name="token">Cancels the asynchronous query.</param>
    /// <returns>The stored result, or null when the scope contains no matching observations.</returns>
    Task<FuturesTickDataV2ReadModel?> GetFuturesTickAtOrBeforeAsync(string contractId, DateOnly valueDate, TimeOnly tickTime, CancellationToken token = default);
    /// <summary>Gets the last futures tick identity in the contract/value-date scope.</summary>
    /// <param name="contractId">The persisted contract identifier.</param>
    /// <param name="valueDate">The requested partition value date.</param>
    /// <returns>The stored result, or null when the scope contains no matching observations.</returns>
    Task<FuturesTickDataId?> GetLastFuturesTickDataIdAsync(string contractId, DateOnly valueDate);
    /// <summary>Gets the last option tick identity in the contract/value-date scope.</summary>
    /// <param name="contractId">The persisted contract identifier.</param>
    /// <param name="valueDate">The requested partition value date.</param>
    /// <returns>The stored result, or null when the scope contains no matching observations.</returns>
    Task<FuturesOptionTickDataId?> GetLastFuturesOptionTickDataIdAsync(string contractId, DateOnly valueDate);
    /// <summary>Gets the option tick with the specified persisted identity.</summary>
    /// <param name="id">The persisted tick or futures contract/value-date identity.</param>
    /// <returns>The stored result, or null when the scope contains no matching observations.</returns>
    Task<FuturesOptionTickDataV2ReadModel?> GetFuturesOptionTickDataAsync(FuturesOptionTickDataId id);
    /// <summary>Gets the last persisted option tick in the contract/value-date scope.</summary>
    /// <param name="contractId">The persisted contract identifier.</param>
    /// <param name="valueDate">The requested partition value date.</param>
    /// <returns>The stored result, or null when the scope contains no matching observations.</returns>
    Task<FuturesOptionTickDataV2ReadModel?> GetLastFuturesOptionTickDataAsync(string contractId, DateOnly valueDate);
    /// <summary>Reads the high, low and volume aggregate of persisted futures ticks.</summary>
    /// <param name="id">The persisted tick or futures contract/value-date identity.</param>
    /// <returns>The stored result, or null when the scope contains no matching observations.</returns>
    Task<FuturesTickHLVDataReadModel?> GetFuturesTickHLVDataAsync(FuturesDataId id);
}
