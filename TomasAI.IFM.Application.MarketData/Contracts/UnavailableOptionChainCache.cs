using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Explicit cache unavailability for providers that do not support prepared strategy chains.</summary>
public sealed class UnavailableOptionChainCache : IOptionChainCache
{
    /// <summary>Shared stateless unavailable implementation.</summary>
    public static UnavailableOptionChainCache Instance { get; } = new();
    private UnavailableOptionChainCache() { }
    /// <inheritdoc />
    public OptionChainCacheStatus GetStatus(OptionChainCacheScope scope) => new(false, 0, null, null, 0, "CacheNotConfigured");
    /// <inheritdoc />
    public OptionChainSnapshotResult TryGetSnapshot(OptionChainSnapshotRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new(OptionChainSnapshotOutcome.Deferred, "CacheNotConfigured");
    }
}
