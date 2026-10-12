using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Immediate provider-neutral access to prepared strategy option snapshots. No operation acquires data.</summary>
public interface IOptionChainCache
{
    /// <summary>Reads a single current-generation snapshot or returns an explicit unavailable result immediately.</summary>
    OptionChainSnapshotResult TryGetSnapshot(OptionChainSnapshotRequest request);
    /// <summary>Reads resident readiness without refreshing or acquiring a scope.</summary>
    OptionChainCacheStatus GetStatus(OptionChainCacheScope scope);
}
