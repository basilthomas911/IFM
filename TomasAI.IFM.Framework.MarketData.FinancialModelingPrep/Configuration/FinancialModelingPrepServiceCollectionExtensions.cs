using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Framework.MarketData.Contracts;

namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;

public static class FinancialModelingPrepServiceCollectionExtensions
{
    private const string TreasuryClientName = "IFM.FMP.Treasury";
    private const string EconomicCalendarClientName = "IFM.FMP.EconomicCalendar";

    /// <summary>
    /// Registers both provider-neutral FMP market-data contracts. The API key
    /// is read from FMP_API_KEY (or the configured environment-variable name)
    /// and is never added to a URI.
    /// </summary>
    public static IServiceCollection AddFinancialModelingPrepMarketData(
        this IServiceCollection services,
        Action<FinancialModelingPrepOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new FinancialModelingPrepOptions();
        configure?.Invoke(options);
        options.Validate(requireApiKey: options.Enabled);

        services.TryAddSingleton(options);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<FinancialModelingPrepRequestGate>();

        services.AddHttpClient(TreasuryClientName, client => ConfigureClient(client, options))
            .RedactLoggedHeaders(["apikey"]);
        services.AddHttpClient(EconomicCalendarClientName, client => ConfigureClient(client, options))
            .RedactLoggedHeaders(["apikey"]);

        services.TryAddSingleton(serviceProvider =>
            new FinancialModelingPrepTreasuryCurve(
                serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(TreasuryClientName),
                serviceProvider.GetRequiredService<FinancialModelingPrepOptions>(),
                serviceProvider.GetRequiredService<FinancialModelingPrepRequestGate>(),
                serviceProvider.GetRequiredService<TimeProvider>()));

        services.TryAddSingleton(serviceProvider =>
            new FinancialModelingPrepEconomicCalendar(
                serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(EconomicCalendarClientName),
                serviceProvider.GetRequiredService<FinancialModelingPrepOptions>(),
                serviceProvider.GetRequiredService<FinancialModelingPrepRequestGate>(),
                serviceProvider.GetRequiredService<TimeProvider>()));

        services.TryAddSingleton<ITreasuryCurve>(serviceProvider =>
            serviceProvider.GetRequiredService<FinancialModelingPrepTreasuryCurve>());
        services.TryAddSingleton<IEconomicCalendar>(serviceProvider =>
            serviceProvider.GetRequiredService<FinancialModelingPrepEconomicCalendar>());

        return services;
    }

    private static void ConfigureClient(HttpClient client, FinancialModelingPrepOptions options)
    {
        client.BaseAddress = options.BaseAddress;
        client.Timeout = Timeout.InfiniteTimeSpan;
    }
}
