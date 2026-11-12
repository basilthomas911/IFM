using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

/// <summary>Reads persisted futures and option ticks from their raw ScyllaDB owner.</summary>
public interface ITradeTickQuoteDbReadContext : IStreamingRawFuturesTradeReader
{
    /// <summary>Reads original trades within one bounded UTC time segment of a trading-date partition for historical indicator initialization.</summary>
    /// <param name="contractId">The exact futures contract.</param>
    /// <param name="valueDate">The trading-date partition.</param>
    /// <param name="fromUtc">The inclusive lower UTC time.</param>
    /// <param name="throughUtc">The inclusive upper UTC time.</param>
    /// <param name="token">Cancels the read.</param>
    /// <returns>The original trades within the requested window in provider order.</returns>
    Task<IReadOnlyList<BootstrapTradeRow>> GetRawFuturesTradeWindowAsync(string contractId, DateOnly valueDate,
        TimeOnly fromUtc, TimeOnly throughUtc, CancellationToken token = default);
    /// <summary>Reads a bounded newest-first raw trade window, preserving source timestamps.</summary>
    /// <param name="contractId">The exact futures contract.</param>
    /// <param name="throughDate">The inclusive trading-date bound.</param>
    /// <param name="token">Cancels the read.</param>
    /// <returns>The selected newest-first raw trade rows.</returns>
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

/// <summary>Streams raw futures trades while the enumerator owns the active provider page.</summary>
public interface IStreamingRawFuturesTradeReader
{
    /// <summary>Reports whether the provider supplies a native streaming read.</summary>
    bool SupportsRawFuturesTradeStreaming => false;

    /// <summary>Reads the original trades in a bounded UTC time segment.</summary>
    /// <param name="contractId">The exact contract identity.</param>
    /// <param name="valueDate">The trading-date partition.</param>
    /// <param name="fromUtc">The inclusive lower UTC time.</param>
    /// <param name="throughUtc">The inclusive upper UTC time.</param>
    /// <param name="token">Cancels the stream.</param>
    /// <returns>The ordered original rows from the active provider page.</returns>
    IAsyncEnumerable<BootstrapTradeRow> StreamRawFuturesTradeWindowAsync(string contractId, DateOnly valueDate,
        TimeOnly fromUtc, TimeOnly throughUtc, CancellationToken token = default);
}
