using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;

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
        var registry = new DatabentoContractRegistrationRegistry(
            runtimeOptions.Contracts,
            runtimeOptions);
        var effectiveRuntimeOptions = runtimeOptions with { Contracts = registry };
        services.TryAddSingleton(registry);
        services.TryAddSingleton<IDatabentoContractRegistrationRegistry>(provider =>
            provider.GetRequiredService<DatabentoContractRegistrationRegistry>());
        services.TryAddSingleton(effectiveRuntimeOptions);
        services.TryAddSingleton(apiOptions ?? new DatabentoMarketDataApiOptions());
        services.TryAddSingleton<IDatabentoMarketDataEpochFactory,
            DatabentoMarketDataEpochFactory>();
        services.TryAddSingleton<IDatabentoCurrentFuturesContractResolver,
            DatabentoCurrentFuturesContractResolver>();
        services.TryAddSingleton<FuturesContractRolloverStartupCheck>();
        services.TryAddSingleton<DatabentoMarketDataApi>();
        services.TryAddSingleton<IMarketDataApi>(provider =>
            provider.GetRequiredService<DatabentoMarketDataApi>());
        return services;
    }

    /// <summary>Registers the provider-neutral historical application API and data load orchestration.</summary>
    public static IServiceCollection AddApplicationMarketDataHistoricalApi(
        this IServiceCollection services,
        DatabentoHistoricalOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(options);
        services.TryAddSingleton<IMarketSessionCalendar, CmeFuturesMarketSessionCalendar>();
        services.TryAddSingleton<IHistoricalSeriesRequestResolver, ConfiguredHistoricalSeriesRequestResolver>();
        services.TryAddSingleton<DatabentoHistoricalApi>();
        services.TryAddSingleton<IMarketDataHistoricalApi>(provider =>
            provider.GetRequiredService<DatabentoHistoricalApi>());
        services.TryAddSingleton<IHistoricalReplayPublisher, NullHistoricalReplayPublisher>();
        services.TryAddSingleton<IHistoricalDailyReplayPublisher, NullHistoricalDailyReplayPublisher>();
        services.TryAddSingleton<IHistoricalAnalyticsSignalReader, NullHistoricalAnalyticsSignalReader>();
        services.TryAddSingleton<HistoricalDataLoader>();
        services.TryAddSingleton<IFuturesEodAnalyticsAssembler, FuturesEodAnalyticsAssembler>();
        return services;
    }
}
