using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.OptionChainCache;

/// <summary>Explicit activation requires a persisted global universe source; no invented live defaults.</summary>
public static class OptionChainCacheServiceCollectionExtensions
{
    /// <summary>Registers one stable facade and a host-owned producer with independent strategy leases.</summary>
    public static IServiceCollection AddStrategyOptionChainCache<TUniverseSource>(this IServiceCollection services)
        where TUniverseSource : class, IOptionUniverseSource
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IOptionUniversePlanner, OptionUniversePlanner>();
        services.TryAddSingleton<OptionChainCoverageObservations>();
        services.TryAddSingleton<OptionChainCache>();
        services.TryAddSingleton<IOptionChainCache>(p => p.GetRequiredService<OptionChainCache>());
        services.TryAddSingleton<IOptionChainSnapshotPublisher>(p => p.GetRequiredService<OptionChainCache>());
        services.TryAddSingleton<IOptionUniverseSource, TUniverseSource>();
        services.AddHostedService<OptionChainBackgroundUpdater>();
        return services;
    }
}
