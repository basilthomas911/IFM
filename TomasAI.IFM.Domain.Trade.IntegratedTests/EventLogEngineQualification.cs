using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Http;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests;

// Opt-in routing for the owned synthetic event-log qualification fixture.
internal static class EventLogEngineQualification
{
    internal static string? Validate(Func<string, string?>? read = null)
    {
        read ??= Environment.GetEnvironmentVariable;
        var run = read("IFM_ENGINE_QUALIFICATION_RUN");
        if (run is null) return null;
        if (!System.Text.RegularExpressions.Regex.IsMatch(run, "^[0-9a-f]{12}$")
            || read("DOTNET_ENVIRONMENT") != "Test")
            throw new InvalidOperationException("Engine qualification requires a Test environment and twelve-character run ID.");
        var postgres = $"Host=127.0.0.1;Port=25432;Database=ifm_eventlog_bench_{run}_synthetic_host";
        var trade = $"Contact Points=127.0.0.1;Port=29042;Default Keyspace=ifm_synthetic_{run}_trade";
        foreach (var (name, expected) in new[] {
            ("IFM_TEST_POSTGRES_CONNECTION", postgres),
            ("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION", postgres),
            ("IFM_TEST_TRADE_CONNECTION", trade),
            ("IFM_TEST_REDIS_URL", "127.0.0.1:26379"),
            ("IFM_FINANCIAL_TEST_NATS_URL", "nats://127.0.0.1:24223") })
            if (read(name) != expected)
                throw new InvalidOperationException($"Qualification endpoint mismatch: {name}.");
        return run;
    }

    internal static IWebHostBuilder Configure(IWebHostBuilder builder)
    {
        var run = Validate();
        if (run is null) return builder;
        var postgres = $"Host=127.0.0.1;Port=25432;Database=ifm_eventlog_bench_{run}_synthetic_host";
        return builder.ConfigureAppConfiguration((_, config) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["IFM_TEST_POSTGRES_CONNECTION"] = postgres,
                ["IFM_TEST_REDIS_URL"] = "127.0.0.1:26379",
                ["IFM_TEST_NATS_URL"] = "nats://127.0.0.1:24223",
                ["AppSettings:CommandServerBaseUri"] = "http://127.0.0.1:25443",
                ["AppSettings:QueryServerBaseUri"] = "http://127.0.0.1:25443",
                ["AppSettings:Databento:DeploymentProfile"] = "SyntheticCi",
                ["TradeBroker:Emulator:AccountAlias"] = $"EventLogQualification-{run}"
            };
            foreach (var (name, suffix) in new[] {
                ("Trade", "trade"), ("Fund", "fund"), ("Reference", "reference"),
                ("OptionPricer", "optionpricer"), ("MarketData", "marketdata"), ("Securities", "securities") })
                values[$"ConnectionStrings:{name}DbConnection"] =
                    $"Contact Points=127.0.0.1;Port=29042;Default Keyspace=ifm_synthetic_{run}_{suffix}";
            values["IFM_TEST_MARKET_DATA_CONNECTION"] = values["ConnectionStrings:MarketDataDbConnection"];
            config.AddInMemoryCollection(values);
        }).ConfigureServices(services =>
        {
            services.RemoveAll<IDistributedCache>();
            services.AddDistributedMemoryCache();
            services.AddSingleton<IHttpMessageHandlerBuilderFilter, RejectExternalHttp>();
        });
    }

    private sealed class RejectExternalHttp : IHttpMessageHandlerBuilderFilter
    {
        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
        {
            next(builder);
            builder.AdditionalHandlers.Insert(0, new RejectHandler());
        };
    }
    private sealed class RejectHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            => throw new InvalidOperationException("External HTTP is disabled during synthetic engine qualification.");
    }
}
