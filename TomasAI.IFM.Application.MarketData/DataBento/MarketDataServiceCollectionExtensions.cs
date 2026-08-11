using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento;

public static class MarketDataServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationMarketDataApi(
        this IServiceCollection services,
        DatabentoMarketDataRuntimeOptions runtimeOptions,
        DatabentoMarketDataApiOptions? apiOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(runtimeOptions);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(runtimeOptions);
        services.TryAddSingleton(apiOptions ?? new DatabentoMarketDataApiOptions());
        services.TryAddSingleton<IDatabentoMarketDataEpochFactory,
            DatabentoMarketDataEpochFactory>();
        services.TryAddSingleton<DatabentoMarketDataApi>();
        services.TryAddSingleton<IMarketDataApi>(provider =>
            provider.GetRequiredService<DatabentoMarketDataApi>());
        return services;
    }
}
