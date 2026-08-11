using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.TickAggregation;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public static class DatabentoServiceCollectionExtensions
{
    /// <summary>
    /// Registers DataBento framework services only. The application-level
    /// IMarketDataApi is intentionally not referenced or registered here.
    /// </summary>
    public static IServiceCollection AddDatabentoMarketDataServices(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IDatabentoFeedFactory, DatabentoFeedFactory>();
        services.TryAddSingleton<ITickLiveEventPublisher, NullTickLiveEventPublisher>();
        return services;
    }
}
