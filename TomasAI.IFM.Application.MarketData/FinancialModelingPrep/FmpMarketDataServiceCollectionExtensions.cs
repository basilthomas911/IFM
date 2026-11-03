using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.FinancialModelingPrep;

public static class FmpMarketDataServiceCollectionExtensions
{

    extension(IServiceCollection services)
    {
        public IServiceCollection AddFinancialModelingPrepReferenceDataApi(
        )
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddSingleton<IReferenceDataApi, FinancialModelingPrepReferenceDataApi>();
            return services;
        }

        public IServiceCollection AddFmpMarketDataImport(
            Action<FmpMarketDataImportOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var options = new FmpMarketDataImportOptions();
            configure?.Invoke(options);
            options.Validate();

            services.TryAddSingleton(options);
            services.TryAddSingleton<IFmpMarketDataImportCoordinator, FmpMarketDataImportCoordinator>();
            return services;
        }
    }
}
