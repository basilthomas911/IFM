using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Persists accepted option chain parameter projections.</summary>
public interface IStrategyOptionChainParameterWriteStore
{
    /// <summary>Projects an event-carried parameter version with monotonic aggregate revision fencing.</summary>
    /// <param name="parameters">The parameters supplied by the caller.</param>
    /// <param name="revision">The revision supplied by the caller.</param>
    /// <param name="published">The published supplied by the caller.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task ProjectAsync(StrategyOptionChainParameterSet parameters, long revision, bool published, CancellationToken token);
}
