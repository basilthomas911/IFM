using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Persisted ScyllaDB parameter read models. Workflow cache reads never call this boundary.</summary>
public interface IStrategyOptionChainParameterStore
{
    /// <summary>Projects an event-carried parameter version with monotonic aggregate revision fencing.</summary>
    Task ProjectAsync(StrategyOptionChainParameterSet parameters, long revision, bool published, CancellationToken token);
    /// <summary>Reads current published global rows for an exact environment from ScyllaDB.</summary>
    Task<ImmutableArray<StrategyOptionChainParameterSet>> ReadPublishedAsync(string environment, CancellationToken token);
    /// <summary>Reads one immutable stored version without consulting command state.</summary>
    Task<StrategyOptionChainParameterSet?> ReadVersionAsync(Guid setId, int version, CancellationToken token);
}
