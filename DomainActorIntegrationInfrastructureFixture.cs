using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb.Schema;
using TomasAI.IFM.Application.Storage.OptionPricerDb.Schema;
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
            ["MarketDataDbConnection"] = "marketdata",
            ["OptionPricerDbConnection"] = "optionpricer",
            ["ReferenceDbConnection"] = "reference",
            ["SecuritiesDbConnection"] = "securities",
            ["TradeDbConnection"] = "trade",
            ["TradePlanDbConnection"] = "tradeplan"
        };

    static readonly string[] PostgresConnections =
    [
        "ConfigurationDbConnection",
        "EventSourceActorDbConnection",
        "MarketDataServiceDbConnection",
        "PortfolioDbConnection",
        "SequenceIdDbConnection",
        "SystemAdminDbConnection"
    ];

    static readonly Dictionary<string, string?> previousEnvironment = new(StringComparer.Ordinal);
    static readonly IsolatedIntegrationInfrastructure infrastructure = new("domainactors");
    static int disposed;

    /// <summary>Gets the NATS endpoint owned by this assembly fixture.</summary>
    public static string NatsUrl
        => Environment.GetEnvironmentVariable("IFM_TEST_NATS_URL")
            ?? throw new InvalidOperationException("The domain actor integration fixture has not started.");

    internal static async Task InitializeAsync(string testAssemblyName)
    {
        await infrastructure.StartAsync();

        var postgresTestConnection = $"Host=127.0.0.1;Port={infrastructure.PostgresPort};Database={infrastructure.PostgresDatabase}";
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
        SetEnvironment("IFM_TEST_MARKET_DATA_CONNECTION", cqlConnections["MarketDataDbConnection"]);
        SetEnvironment("IFM_TEST_OPTION_PRICER_CONNECTION", cqlConnections["OptionPricerDbConnection"]);
        SetEnvironment("IFM_TEST_REFERENCE_CONNECTION", cqlConnections["ReferenceDbConnection"]);
        SetEnvironment("IFM_TEST_SECURITIES_CONNECTION", cqlConnections["SecuritiesDbConnection"]);
        SetEnvironment("IFM_TEST_TRADE_PLAN_CONNECTION", cqlConnections["TradePlanDbConnection"]);
        SetEnvironment("IFM_TEST_TRADE_CONNECTION", cqlConnections["TradeDbConnection"]);
        SetEnvironment("IFM_BOOTSTRAP_TEST_REFERENCE_CONNECTION", cqlConnections["ReferenceDbConnection"]);
        SetEnvironment("IFM_BOOTSTRAP_TEST_SEQUENCE_CONNECTION", postgresTestConnection);
        var artifactDirectory = Path.Combine(AppContext.BaseDirectory, "qualification", $"acceptance-{infrastructure.RunId}");
        Directory.CreateDirectory(artifactDirectory);
        SetEnvironment("IFM_QUALIFICATION_ARTIFACT_DIRECTORY", artifactDirectory);

        foreach (var name in PostgresConnections)
            SetEnvironment($"ConnectionStrings__{name}", infrastructure.PostgresConnectionString);
        foreach (var (name, connection) in cqlConnections)
            SetEnvironment($"ConnectionStrings__{name}", connection);

        var settings = new DbConnectionSettings();
        foreach (var name in PostgresConnections)
            settings.Add(name, infrastructure.PostgresConnectionString, "System.Data.Postgres");
        foreach (var (name, connection) in cqlConnections)
            settings.Add(name, connection, "System.Data.ScyllaDb");
        var logger = NullLogger<DbProvider>.Instance;
        await new ConfigurationSchemaDb(settings, logger).CreateAllAsync();
        await new EventSourceSchemaDb(settings, logger).CreateAllAsync();
        await new SequenceIdSchemaDb(settings, logger).CreateAllAsync();
        await new MarketDataSchemaDb(settings, logger).CreateAllAsync();
        await new MarketDataServiceSchemaDb(settings, logger).CreateAllAsync();
        await new OptionPricerSchemaDb(settings, logger).CreateAllAsync();
        await new PortfolioSchemaDb(settings, logger).CreateAllAsync();
        await new ReferenceSchemaDb(settings, logger).CreateAllAsync();
        await new SecuritiesSchemaDb(settings, logger).CreateAllAsync();
        await new SystemAdminSchemaDb(settings, logger).CreateAllAsync();
        await new TradeSchemaDb(settings, logger).CreateAllAsync();
        await new TradePlanSchemaDb(settings, logger).CreateAllAsync();

        if (testAssemblyName.Contains("Portfolio.IntegrationTests", StringComparison.Ordinal)
            || testAssemblyName.Contains("MarketData.Feed.IntegrationTests", StringComparison.Ordinal)
            || testAssemblyName.Contains("Trade.IntegratedTests", StringComparison.Ordinal))
        {
            if (testAssemblyName.Contains("MarketData.Feed.IntegrationTests", StringComparison.Ordinal))
            {
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
            var services = KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>.StartShared();
            var initializer = (TomasAI.IFM.Application.Api.Server.ApplicationSchemaInitializer?)services.GetService(
                typeof(TomasAI.IFM.Application.Api.Server.ApplicationSchemaInitializer))
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
        await KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>
            .DisposeSharedAsync();
        await infrastructure.DisposeAsync();
        foreach (var (name, value) in previousEnvironment)
            Environment.SetEnvironmentVariable(name, value);
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
        var requiresSharedInfrastructure = selectedCases.Any(testCase =>
            !testCase.Traits.TryGetValue("Infrastructure", out var values)
            || !values.Contains("SelfContained", StringComparer.Ordinal));
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
        finally
        {
            if (requiresSharedInfrastructure)
                DomainActorIntegrationInfrastructureFixture.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
