using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TomasAI.IFM.Application.MarketData.FinancialModelingPrep;

public static class FmpMarketDataServiceCollectionExtensions
{
    public static IServiceCollection AddFmpMarketDataImport(
        this IServiceCollection services,
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
