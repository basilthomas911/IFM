

using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

/// <summary>Direct ingestion persistence; these observations do not append business event-log entries.</summary>
public interface ITradeTickQuoteDbWriteContext
{
    /// <summary>Directly writes one raw Databento trade observation without appending a business event.</summary>
    /// <param name="value">The accepted observation to store.</param>
    /// <returns>A task representing the direct persistence operation.</returns>
    Task InsertTickTradeDataAsync(FuturesTickTradeDataInsertedEvent value);
    /// <summary>Directly writes one bounded quote segment; the Scylla executor releases its owned encoded buffer after success or failure.</summary>
    /// <param name="value">The accepted observation to store.</param>
    /// <returns>A task representing the direct persistence operation.</returns>
    Task InsertTickQuoteDataAsync(FuturesTickQuoteDataInsertedEvent value);
    /// <summary>Writes accepted futures ticks to both identity and time indexes in partition-scoped batches.</summary>
    /// <param name="value">The accepted observation to store.</param>
    /// <returns>A task representing the direct persistence operation.</returns>
    Task InsertFuturesTickDataAsync(FuturesTickDataV2ReadModel value);
    /// <summary>Writes accepted futures ticks to both identity and time indexes in partition-scoped batches.</summary>
    /// <param name="values">The accepted observations; duplicate identities are rejected before writing.</param>
    /// <returns>The stored result, or null when the scope contains no matching observations.</returns>
    Task InsertFuturesTickDataAsync(ICollection<FuturesTickDataV2ReadModel> values);
    /// <summary>Writes accepted option ticks with their supplied prices and Greeks.</summary>
    /// <param name="value">The accepted observation to store.</param>
    /// <returns>A task representing the direct persistence operation.</returns>
    Task InsertFuturesOptionTickDataAsync(FuturesOptionTickDataV2ReadModel value);
    /// <summary>Writes accepted option ticks with their supplied prices and Greeks.</summary>
    /// <param name="values">The accepted observations; duplicate identities are rejected before writing.</param>
    /// <returns>The stored result, or null when the scope contains no matching observations.</returns>
    Task InsertFuturesOptionTickDataAsync(ICollection<FuturesOptionTickDataV2ReadModel> values);
    /// <summary>Deletes the contract/value-date scope from both futures tick indexes.</summary>
    /// <param name="contractId">The persisted contract identifier.</param>
    /// <param name="valueDate">The requested partition value date.</param>
    /// <returns>A task representing the direct persistence operation.</returns>
    Task DeleteFuturesTickDataAsync(string contractId, DateOnly valueDate);
    /// <summary>Deletes option ticks for the contract/value-date scope.</summary>
    /// <param name="contractId">The persisted contract identifier.</param>
    /// <param name="valueDate">The requested partition value date.</param>
    /// <returns>A task representing the direct persistence operation.</returns>
    Task DeleteFuturesOptionTickDataAsync(string contractId, DateOnly valueDate);
}
