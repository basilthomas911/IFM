using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Schema;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb.Schema;
using TomasAI.IFM.Application.Storage.PortfolioDb.Schema;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;
using TomasAI.IFM.Application.Storage.SystemAdminDb.Schema;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Application.Storage.TradePlanDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.Storage;
/// <summary>
/// Owns the one isolated infrastructure set used by a complete domain actor integration-test assembly.
/// </summary>
public static class DomainActorIntegrationInfrastructureFixture
{
    static readonly IReadOnlyDictionary<string, string> CqlConnections =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TradeTickQuoteDbConnection"] = "tradetickquote"
        };

    static readonly string[] PostgresConnections =
    [
        "ConfigurationDbConnection",
        "EventSourceActorDbConnection",
        "MarketDataServiceDbConnection",
        "MarketDataDbConnection",
        "ReferenceDbConnection",
        "PortfolioDbConnection",
        "SequenceIdDbConnection",
        "SystemAdminDbConnection",
        "SecuritiesDbConnection",
        "TradeDbConnection",
        "TradePlanDbConnection"
    ];

    static readonly Dictionary<string, string?> previousEnvironment = new(StringComparer.Ordinal);
    static readonly IsolatedIntegrationInfrastructure infrastructure = new("domainactors");
    static int disposed;

    /// <summary>Observes the real application shutdown before its service provider is disposed.</summary>
    sealed class TrackedApiServerLifecycle : TomasAI.IFM.Application.Api.Server.Core.Hosting.Contracts.IApiServerLifecycle
    {
        readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task Completion => completion.Task;

        public async Task RunAsync(Microsoft.AspNetCore.Builder.WebApplication app, string[] args,
            Microsoft.Extensions.Logging.ILogger logger)
        {
            try
            {
                await new TomasAI.IFM.Application.Api.Server.Core.Hosting.ApiServerLifecycle()
                    .RunAsync(app, args, logger).ConfigureAwait(false);
                completion.TrySetResult();
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
                throw;
            }
        }
    }

    /// <summary>Gets the NATS endpoint owned by this assembly fixture.</summary>
    public static string NatsUrl
        => Environment.GetEnvironmentVariable("IFM_TEST_NATS_URL")
            ?? throw new InvalidOperationException("The domain actor integration fixture has not started.");

    internal static async Task InitializeAsync(string testAssemblyName)
    {
        var backtestConnection = Environment.GetEnvironmentVariable("IFM_BACKTEST_POSTGRES_CONNECTION");
        var backtestCredentials = Environment.GetEnvironmentVariable("POSTGRES_DEV_KEY");
        if (backtestConnection is not null)
        {
            var target = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = backtestConnection };
            if (!target.TryGetValue("Host", out var host) || !Equals(host, "127.0.0.1")
                || !target.TryGetValue("Database", out var database)
                || !System.Text.RegularExpressions.Regex.IsMatch(database.ToString()!, "\\Aifm_application_backtest_[a-f0-9]{12}_synthetic_host\\z")
                || string.IsNullOrWhiteSpace(backtestCredentials))
                throw new InvalidOperationException("Backtest requires a run-specific loopback PostgreSQL database and POSTGRES_DEV_KEY.");
        }
        await infrastructure.StartAsync();

        var postgresConnection = backtestConnection ?? infrastructure.PostgresConnectionString;
        if (backtestConnection is not null)
        {
            SetEnvironment("POSTGRES_DEV_KEY", backtestCredentials!);
            SetEnvironment("POSTGRES_TEST_KEY", backtestCredentials!);
            SetEnvironment("MarketDataHost__Enabled", "false");
            KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>
                .ConfigureSharedServices(services =>
                {
                    // Historical replay supplies inputs itself and never owns a live feed.
                    foreach (var descriptor in services.Where(descriptor =>
                        descriptor.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                        && descriptor.ImplementationFactory?.Method.ReturnType == typeof(TomasAI.IFM.Application.MarketData.Databento.Resiliency.DatabentoMarketDataWatchdogService)).ToArray())
                        services.Remove(descriptor);
                });
        }
        var postgresTestConnection = postgresConnection;
        var cqlConnections = CqlConnections.ToDictionary(
            static pair => pair.Key,
            pair => infrastructure.GetCqlConnectionString($"ifm_synthetic_{infrastructure.RunId}_{pair.Value}"),
            StringComparer.Ordinal);
        foreach (var keyspace in CqlConnections.Values.Append("fund").Distinct(StringComparer.Ordinal))
            await infrastructure.EnsureCqlKeyspaceAsync($"ifm_synthetic_{infrastructure.RunId}_{keyspace}");

        SetEnvironment("DOTNET_ENVIRONMENT", "Development");
        SetEnvironment("ASPNETCORE_ENVIRONMENT", "Development");
        if (testAssemblyName.Contains("Trade.IntegratedTests", StringComparison.Ordinal))
        {
            SetEnvironment("DOTNET_ENVIRONMENT", "Test");
            SetEnvironment("ASPNETCORE_ENVIRONMENT", "Test");
            SetEnvironment("IFM_ENGINE_QUALIFICATION_RUN", infrastructure.RunId);
            SetEnvironment("ParameterSets__SingleUserDevelopmentEnabled", "false");
        }
        SetEnvironment("AppSettings__Databento__DataSource", "Synthetic");
        SetEnvironment("AppSettings__Databento__DeploymentProfile", "SyntheticCi");
        if (testAssemblyName.Contains("MarketData.Feed.IntegrationTests", StringComparison.Ordinal))
        {
            SetEnvironment("AppSettings__Databento__Contracts__0__DomainContractId", "ES20261218");
            SetEnvironment("AppSettings__Databento__Contracts__0__ProviderContractName", "ESZ6");
            SetEnvironment("AppSettings__Databento__Contracts__0__AssetTypeId", "Futures");
            SetEnvironment("AppSettings__Databento__Contracts__0__RootSymbol", "ES");
            SetEnvironment("AppSettings__Databento__Contracts__0__OnTheRun", "true");
            SetEnvironment("AppSettings__Databento__Contracts__0__Rollover", "true");
            SetEnvironment("AppSettings__Databento__Contracts__1__DomainContractId", "VX20261216");
            SetEnvironment("AppSettings__Databento__Contracts__1__ProviderContractName", "VXZ6");
            SetEnvironment("AppSettings__Databento__Contracts__1__AssetTypeId", "Futures");
            SetEnvironment("AppSettings__Databento__Contracts__1__RootSymbol", "VX");
            SetEnvironment("AppSettings__Databento__Contracts__1__OnTheRun", "true");
            SetEnvironment("AppSettings__Databento__Contracts__1__Rollover", "true");
            SetEnvironment("AppSettings__Databento__Contracts__2__DomainContractId", "VX20270120");
            SetEnvironment("AppSettings__Databento__Contracts__2__ProviderContractName", "VXF7");
            SetEnvironment("AppSettings__Databento__Contracts__2__AssetTypeId", "Futures");
            SetEnvironment("AppSettings__Databento__Contracts__2__RootSymbol", "VX");
            SetEnvironment("AppSettings__Databento__Contracts__2__OnTheRun", "false");
            SetEnvironment("AppSettings__Databento__Contracts__2__Rollover", "true");
            SetEnvironment("AppSettings__Databento__Contracts__3__DomainContractId", "ES20261218P5400");
            SetEnvironment("AppSettings__Databento__Contracts__3__ProviderContractName", "ESZ6 P5400");
            SetEnvironment("AppSettings__Databento__Contracts__3__AssetTypeId", "FuturesOption");
            SetEnvironment("AppSettings__Databento__Contracts__3__RootSymbol", "ES");
        }
        SetEnvironment("MarketDataRecovery__Stage3__Enabled", "false");
        SetEnvironment("IFM_TEST_NATS_URL", infrastructure.NatsUrl);
        // The remote relay must use the same isolated broker as the actor transport.
        SetEnvironment("MarketDataHost__NatsUrl", infrastructure.NatsUrl);
        SetEnvironment("MarketDataHost__Scope", "qualification-" + infrastructure.RunId);
        SetEnvironment("IFM_CONFIGURED_TEST_NATS_URL", infrastructure.AuxiliaryNatsUrl);
        SetEnvironment("IFM_FINANCIAL_TEST_NATS_URL", infrastructure.NatsUrl);
        SetEnvironment("IFM_NATS_URL", infrastructure.NatsUrl);
        SetEnvironment("IFM_DOWNLOADLOG_TEST_NATS_URL", infrastructure.AuxiliaryNatsUrl);
        SetEnvironment("IFM_QUALIFICATION_NATS_URL", infrastructure.NatsUrl);
        SetEnvironment("IFM_TEST_REDIS_URL", infrastructure.RedisConnectionString);
        SetEnvironment("IFM_QUALIFICATION_REDIS_URL", infrastructure.RedisConnectionString);
        SetEnvironment("IFM_TEST_POSTGRES_CONNECTION", postgresTestConnection);
        SetEnvironment("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION", postgresTestConnection);
        SetEnvironment("IFM_QUALIFICATION_POSTGRES_CONNECTION", postgresTestConnection);
        SetEnvironment("IFM_QUALIFICATION_SCYLLA_PORT", infrastructure.CqlPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
        SetEnvironment("IFM_TEST_MARKET_DATA_CONNECTION", postgresTestConnection);
        SetEnvironment("IFM_TEST_OPTION_PRICER_CONNECTION", postgresTestConnection);
        SetEnvironment("IFM_TEST_REFERENCE_CONNECTION", postgresTestConnection);
        SetEnvironment("IFM_TEST_SECURITIES_CONNECTION", postgresTestConnection);
        SetEnvironment("IFM_TEST_TRADE_PLAN_CONNECTION", postgresTestConnection);
        SetEnvironment("IFM_TEST_TRADE_CONNECTION", postgresTestConnection);
        SetEnvironment("IFM_BOOTSTRAP_TEST_REFERENCE_CONNECTION", postgresTestConnection);
        SetEnvironment("IFM_BOOTSTRAP_TEST_SEQUENCE_CONNECTION", postgresTestConnection);
        var artifactDirectory = Path.Combine(AppContext.BaseDirectory, "qualification", $"acceptance-{infrastructure.RunId}");
        Directory.CreateDirectory(artifactDirectory);
        SetEnvironment("IFM_QUALIFICATION_ARTIFACT_DIRECTORY", artifactDirectory);

        foreach (var name in PostgresConnections)
            SetEnvironment($"ConnectionStrings__{name}", postgresConnection);
        foreach (var (name, connection) in cqlConnections)
            SetEnvironment($"ConnectionStrings__{name}", connection);

        var settings = new DbConnectionSettings();
        foreach (var name in PostgresConnections)
            settings.Add(name, postgresConnection, "System.Data.Postgres");
        foreach (var (name, connection) in cqlConnections)
            settings.Add(name, connection, "System.Data.ScyllaDb");
        var logger = NullLogger<DbProvider>.Instance;
        await new ConfigurationSchemaDb(settings, logger).CreateAllAsync();
        await new EventSourceSchemaDb(settings, logger).CreateAllAsync();
        await new SequenceIdSchemaDb(settings, logger).CreateAllAsync();
        await new MarketDataSchemaDb(settings, logger).CreateAllAsync();
        await new MarketDataServiceSchemaDb(settings, logger).CreateAllAsync();
        await new PortfolioSchemaDb(settings, logger).CreateAllAsync();
        await new ReferenceSchemaDb(settings, logger).CreateAllAsync();
        await new SecuritiesSchemaDb(settings, logger).CreateAllAsync();
        await new SystemAdminSchemaDb(settings, logger).CreateAllAsync();
        await new TradeSchemaDb(settings, logger).CreateAllAsync();
        await new TradePlanSchemaDb(settings, logger).CreateAllAsync();

        // Keep the normal runtime, but let cleanup await its actor/container shutdown.
        // Compose this with feed and trade test configuration below.
        KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>
            .ConfigureSharedServices(services =>
                Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.Replace(
                    services, ServiceDescriptor.Singleton<TomasAI.IFM.Application.Api.Server.Core.Hosting.Contracts.IApiServerLifecycle>(
                        _ => new TrackedApiServerLifecycle())));
        KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>
            .ConfigureHostShutdown(async services =>
            {
                var lifecycle = (TrackedApiServerLifecycle)services.GetRequiredService<
                    TomasAI.IFM.Application.Api.Server.Core.Hosting.Contracts.IApiServerLifecycle>();
                services.GetRequiredService<Microsoft.Extensions.Hosting.IHostApplicationLifetime>().StopApplication();
                await lifecycle.Completion.WaitAsync(TimeSpan.FromMinutes(1)).ConfigureAwait(false);
            });

        if (testAssemblyName.Contains("Portfolio.IntegrationTests", StringComparison.Ordinal)
            || testAssemblyName.Contains("MarketData.Feed.IntegrationTests", StringComparison.Ordinal)
            || testAssemblyName.Contains("Trade.IntegratedTests", StringComparison.Ordinal))
        {
            if (testAssemblyName.Contains("MarketData.Feed.IntegrationTests", StringComparison.Ordinal))
            {
                // This fixture owns the deterministic managed feed, rather than a separate native host.
                SetEnvironment("MarketDataHost__Enabled", "false");
                KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>
                    .ConfigureSharedServices(services =>
                    {
                        var serviceType = Type.GetType(
                            "TomasAI.IFM.Framework.MarketData.DataBento.IDatabentoFeedFactory, TomasAI.IFM.Framework.MarketData.DataBento",
                            throwOnError: true)!;
                        var implementationType = Type.GetType(
                            "TomasAI.IFM.Application.Actor.IntegrationTests.IntegrationDatabentoFeedFactory, TomasAI.IFM.Application.Actor.IntegrationTests",
                            throwOnError: true)!;
                        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.Replace(
                            services,
                            Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton(
                                serviceType, implementationType));
                        var lifecycleServiceType = Type.GetType(
                            "TomasAI.IFM.Application.MarketData.Databento.Resiliency.IDatabentoLifecycleRuntime, TomasAI.IFM.Application.MarketData",
                            throwOnError: true)!;
                        var lifecycleImplementationType = Type.GetType(
                            "TomasAI.IFM.Application.Actor.IntegrationTests.IntegrationDatabentoLifecycleRuntime, TomasAI.IFM.Application.Actor.IntegrationTests",
                            throwOnError: true)!;
                        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.Replace(
                            services,
                            Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton(
                                lifecycleServiceType, lifecycleImplementationType));
                    });
            }
            if (testAssemblyName.Contains("Trade.IntegratedTests", StringComparison.Ordinal))
                KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>
                    .ConfigureSharedServices(services =>
                    {
                        // Trade qualification does not start or stop an external feed worker.
                        TomasAI.IFM.IntegrationTesting.HostedServiceOverrides.Suppress<TomasAI.IFM.Application.MarketData.Databento.Resiliency.DatabentoMarketDataWatchdogService>(services);
                        // Configured Trade hosts deliberately omit Feed actors and supply
                        // frozen market inputs. A remote receiver cannot own a Feed mailbox there.
                        TomasAI.IFM.IntegrationTesting.HostedServiceOverrides.Suppress<TomasAI.IFM.Application.MarketData.Remote.MarketDataClientReceiver>(services);
                    });
            var services = KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>.StartShared();
            var initializer = (TomasAI.IFM.Application.Api.Server.Core.Startup.Schema.ApplicationSchemaInitializer?)services.GetService(
                typeof(TomasAI.IFM.Application.Api.Server.Core.Startup.Schema.ApplicationSchemaInitializer))
                ?? throw new InvalidOperationException("The API host did not register its schema initializer.");
            await initializer.InitializeAsync();

            if (testAssemblyName.Contains("MarketData.Feed.IntegrationTests", StringComparison.Ordinal))
                await SeedFeedContractCatalogAsync(services);

        }
    }


    static Task SeedFeedContractCatalogAsync(IServiceProvider services)
    {
        var securities = (ISecuritiesDbContext?)services.GetService(typeof(ISecuritiesDbContext))
            ?? throw new InvalidOperationException("The API host did not register the Securities context.");
        ICollection<FuturesContractV3ReadModel> contracts =
        [
            new("ES20251010", "E-mini S&P 500 Dec 2025", "ES", "ESZ5", "FUT", "USD", "CME", "50",
                new DateOnly(2025, 12, 19), true, true),
            new("ES20261218", "E-mini S&P 500 Dec 2026", "ES", "ESZ6", "FUT", "USD", "CME", "50",
                new DateOnly(2026, 12, 18), true, true),
            new("VX20261216", "VX Futures Dec 2026", "VX", "VXZ6", "FUT", "USD", "CFE", "1000",
                new DateOnly(2026, 12, 16), true, true),
            new("VX20270120", "VX Futures Jan 2027", "VX", "VXF7", "FUT", "USD", "CFE", "1000",
                new DateOnly(2027, 1, 20), false, true)
        ];
        return securities.InsertFuturesContractsAsync(contracts);
    }
    internal static async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        try
        {
            // The factory owns the same ordered shutdown for shared and configured hosts.
            await KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>.DisposeSharedAsync();
        }
        finally
        {
            await infrastructure.DisposeAsync();
            foreach (var (name, value) in previousEnvironment)
                Environment.SetEnvironmentVariable(name, value);
        }
    }

    static void SetEnvironment(string name, string value)
    {
        previousEnvironment.TryAdd(name, Environment.GetEnvironmentVariable(name));
        Environment.SetEnvironmentVariable(name, value);
    }
}

/// <summary>Runs the owned integration infrastructure around the complete xUnit assembly.</summary>
public sealed class DomainActorIntegrationTestFramework : Xunit.Sdk.XunitTestFramework
{
    /// <summary>Creates the domain integration test framework.</summary>
    public DomainActorIntegrationTestFramework(Xunit.Abstractions.IMessageSink messageSink)
        : base(messageSink)
    {
    }

    /// <inheritdoc />
    protected override Xunit.Abstractions.ITestFrameworkExecutor CreateExecutor(
        System.Reflection.AssemblyName assemblyName)
        => new DomainActorIntegrationTestFrameworkExecutor(
            assemblyName,
            SourceInformationProvider,
            DiagnosticMessageSink);
}

/// <summary>Executes all test cases inside one isolated infrastructure lifetime.</summary>
public sealed class DomainActorIntegrationTestFrameworkExecutor : Xunit.Sdk.XunitTestFrameworkExecutor
{
    /// <summary>Creates the assembly executor.</summary>
    public DomainActorIntegrationTestFrameworkExecutor(
        System.Reflection.AssemblyName assemblyName,
        Xunit.Abstractions.ISourceInformationProvider sourceInformationProvider,
        Xunit.Abstractions.IMessageSink diagnosticMessageSink)
        : base(assemblyName, sourceInformationProvider, diagnosticMessageSink)
    {
    }

    /// <inheritdoc />
    protected override void RunTestCases(
        IEnumerable<Xunit.Sdk.IXunitTestCase> testCases,
        Xunit.Abstractions.IMessageSink executionMessageSink,
        Xunit.Abstractions.ITestFrameworkExecutionOptions executionOptions)
    {
        var selectedCases = testCases.ToArray();
        if (Environment.GetEnvironmentVariable("IFM_BACKTEST_POSTGRES_CONNECTION") is not null
            && selectedCases.Any(testCase => !testCase.Traits.TryGetValue("Purpose", out var purposes)
                || !purposes.Contains("Backtest", StringComparer.Ordinal)))
            throw new InvalidOperationException("Persistent backtest databases may only run tests explicitly marked Purpose=Backtest.");
        var requiresSharedInfrastructure = selectedCases.Any(testCase =>
            !testCase.Traits.TryGetValue("Infrastructure", out var values)
            || !values.Contains("SelfContained", StringComparer.Ordinal));
        Exception? primaryFailure = null;
        try
        {
            if (requiresSharedInfrastructure)
                DomainActorIntegrationInfrastructureFixture.InitializeAsync(TestAssembly.Assembly.Name).GetAwaiter().GetResult();
            using var runner = new Xunit.Sdk.XunitTestAssemblyRunner(
                TestAssembly,
                selectedCases,
                DiagnosticMessageSink,
                executionMessageSink,
                executionOptions);
            runner.RunAsync().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
            DiagnosticMessageSink.OnMessage(new Xunit.Sdk.DiagnosticMessage("Integration initialization/execution failed: " + exception));
            throw;
        }
        finally
        {
            try
            {
                if (requiresSharedInfrastructure)
                    DomainActorIntegrationInfrastructureFixture.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception cleanupFailure) when (primaryFailure is not null)
            {
                DiagnosticMessageSink.OnMessage(new Xunit.Sdk.DiagnosticMessage("Secondary integration cleanup failure: " + cleanupFailure));
            }
        }
    }
}
