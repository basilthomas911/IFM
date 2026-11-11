using System.Collections.Immutable;
using TomasAI.IFM.Shared.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Reads saved statistics before allocating a chain; never called from a tick callback.</summary>
public interface IOptionChainStatisticsStore
{
    /// <summary>Reads option chain statistics from the committed projection.</summary>
    /// <param name="underlyingContractId">The exact canonical underlying futures contract identity.</param>
    /// <param name="expiryDate">The expiry date identifying the immutable partition.</param>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<ImmutableArray<OptionChainStatisticsSnapshot>> ReadOptionChainStatisticsAsync(
        string underlyingContractId, DateOnly expiryDate, DateOnly valueDate, CancellationToken cancellationToken);
}
