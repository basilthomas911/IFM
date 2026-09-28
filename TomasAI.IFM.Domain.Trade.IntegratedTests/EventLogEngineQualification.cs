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
        var postgres = read("IFM_TEST_POSTGRES_CONNECTION") ?? throw new InvalidOperationException("Qualification PostgreSQL endpoint is required.");
        if (read("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION") != postgres)
            throw new InvalidOperationException("Qualification PostgreSQL endpoints must match.");
        var parsed = new Npgsql.NpgsqlConnectionStringBuilder(postgres);
        if (parsed.Host != "127.0.0.1" || parsed.Port <= 0 || parsed.Database != $"ifm_eventlog_bench_{run}_synthetic_host")
            throw new InvalidOperationException("Qualification PostgreSQL must be loopback and run-scoped.");
        var trade = read("IFM_TEST_TRADE_CONNECTION") ?? string.Empty;
        if (!System.Text.RegularExpressions.Regex.IsMatch(trade, $@"\AContact Points=127\.0\.0\.1;Port=\d+;Default Keyspace=ifm_synthetic_{run}_trade\z"))
            throw new InvalidOperationException("Qualification CQL endpoint mismatch.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(read("IFM_TEST_REDIS_URL") ?? string.Empty, @"\A127\.0\.0\.1:\d+(,.*)?\z"))
            throw new InvalidOperationException("Qualification Redis endpoint mismatch.");
        if (!Uri.TryCreate(read("IFM_FINANCIAL_TEST_NATS_URL"), UriKind.Absolute, out var nats)
            || nats.Scheme != "nats" || nats.Host != "127.0.0.1" || nats.Port <= 0)
            throw new InvalidOperationException("Qualification NATS endpoint mismatch.");
        return run;
    }

    internal static IWebHostBuilder Configure(IWebHostBuilder builder)
    {
        var run = Validate();
        if (run is null) return builder;
        var postgres = Environment.GetEnvironmentVariable("IFM_TEST_POSTGRES_CONNECTION")!;
        var scyllaPort = int.Parse(Environment.GetEnvironmentVariable("IFM_QUALIFICATION_SCYLLA_PORT")
            ?? throw new InvalidOperationException("Qualification CQL port is required."));
        return builder.ConfigureAppConfiguration((_, config) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["IFM_TEST_POSTGRES_CONNECTION"] = postgres,
                ["IFM_TEST_REDIS_URL"] = Environment.GetEnvironmentVariable("IFM_TEST_REDIS_URL"),
                ["IFM_TEST_NATS_URL"] = Environment.GetEnvironmentVariable("IFM_FINANCIAL_TEST_NATS_URL"),
                ["AppSettings:Databento:DeploymentProfile"] = "SyntheticCi",
                ["TradeBroker:Emulator:AccountAlias"] = $"EventLogQualification-{run}"
            };
            foreach (var (name, suffix) in new[] {
                ("Trade", "trade"), ("Fund", "fund"), ("Reference", "reference"),
                ("OptionPricer", "optionpricer"), ("MarketData", "marketdata"), ("Securities", "securities") })
                values[$"ConnectionStrings:{name}DbConnection"] =
                    $"Contact Points=127.0.0.1;Port={scyllaPort};Default Keyspace=ifm_synthetic_{run}_{suffix}";
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
