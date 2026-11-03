using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;
using TomasAI.IFM.Framework.MarketData.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.Historical;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public static class DatabentoServiceCollectionExtensions
{

    /// <param name="services">
    /// The service collection to register the provider services in.
    /// </param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers DataBento framework services only. The application-level
        /// IMarketDataApi is intentionally not referenced or registered here.
        /// </summary>
        ///
        /// <returns>The add databento market data services result.</returns>
        public IServiceCollection AddDatabentoMarketDataServices(
        )
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddSingleton<IDatabentoFeedFactory, DatabentoFeedFactory>();
            services.TryAddSingleton<ITickLiveEventPublisher, NullTickLiveEventPublisher>();
            return services;
        }

        /// <summary>
        /// Registers the provider-neutral Databento historical adapter. Credentials remain in the
        /// native Databento environment and are never copied into dependency-injection options.
        /// </summary>
        ///
        /// <param name="options">The configuration governing provider or feed operation.</param>
        /// <returns>The add databento historical market data services result.</returns>
        public IServiceCollection AddDatabentoHistoricalMarketDataServices(
            DatabentoHistoricalProviderOptions options)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(options);
            options.Validate();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton(options);
            services.TryAddSingleton<DatabentoHistoricalProvider>();
            services.TryAddSingleton<IMarketDataHistoricalProvider>(provider =>
                provider.GetRequiredService<DatabentoHistoricalProvider>());
            return services;
        }
    }
}
