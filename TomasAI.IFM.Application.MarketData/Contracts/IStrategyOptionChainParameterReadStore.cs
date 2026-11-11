using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Reads published option chain policy from its PostgreSQL projection.</summary>
public interface IStrategyOptionChainParameterReadStore
{
    /// <summary>Reads current published global rows for an exact environment from PostgreSQL.</summary>
    /// <param name="environment">The environment supplied by the caller.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<ImmutableArray<StrategyOptionChainParameterSet>> ReadPublishedAsync(string environment, CancellationToken token);
    /// <summary>Reads one immutable stored version without consulting command state.</summary>
    /// <param name="setId">The set id supplied by the caller.</param>
    /// <param name="version">The exact immutable reference revision.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<StrategyOptionChainParameterSet?> ReadVersionAsync(Guid setId, int version, CancellationToken token);
}
