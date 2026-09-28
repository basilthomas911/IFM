using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace TomasAI.IFM.IntegrationTesting;

/// <summary>
/// Starts the application through its normal entry point on a real Kestrel listener
/// bound to an operating-system-assigned loopback port.
/// </summary>
/// <typeparam name="TEntryPoint">The application entry-point type.</typeparam>
public class KestrelWebApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    const string ActorRuntimeStartupSignalTypeName =
        "TomasAI.IFM.Application.Api.Server.IActorRuntimeStartupSignal";

    static readonly Lazy<KestrelWebApplicationFactory<TEntryPoint>> Shared =
        new(() => new KestrelWebApplicationFactory<TEntryPoint>(useOwnHost: true));

    static Action<IServiceCollection>? configureSharedServices;

    readonly Action<IWebHostBuilder>? configureWebHost;
    readonly bool useOwnHost;
    IServiceProvider? readyServices;

    /// <summary>Creates a production-shaped integration host on an isolated port.</summary>
    public KestrelWebApplicationFactory()
        : this(useOwnHost: false)
    {
    }

    KestrelWebApplicationFactory(bool useOwnHost)
    {
        this.useOwnHost = useOwnHost;
        UseKestrel(0);
    }

    KestrelWebApplicationFactory(Action<IWebHostBuilder> configureWebHost)
        : this(useOwnHost: true)
    {
        this.configureWebHost = configureWebHost;
    }

    /// <summary>
    /// Gets application services after the domain actor runtime has finished starting.
    /// </summary>
    public new IServiceProvider Services
    {
        get
        {
            if (!useOwnHost)
                return Shared.Value.Services;

            if (readyServices is not null)
                return readyServices;

            var services = base.Services;
            var startupSignalType = typeof(TEntryPoint).Assembly.GetType(ActorRuntimeStartupSignalTypeName);
            if (startupSignalType is null)
                return readyServices = services;

            var startupSignal = services.GetService(startupSignalType);
            var waitAsync = startupSignalType.GetMethod("WaitAsync", [typeof(CancellationToken)]);
            if (startupSignal is null || waitAsync is null)
                return readyServices = services;

            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var readiness = (Task?)waitAsync.Invoke(startupSignal, [timeout.Token]);
            readiness?.GetAwaiter().GetResult();

            return readyServices = services;
        }
    }

    /// <summary>Starts the assembly-shared host and waits for the actor runtime to become ready.</summary>
    /// <returns>The ready shared host service provider.</returns>
    public static IServiceProvider StartShared()
        => Shared.Value.Services;

    /// <summary>Configures services on the assembly-shared host before it is created.</summary>
    /// <param name="configuration">The service configuration to apply.</param>
    public static void ConfigureSharedServices(Action<IServiceCollection> configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (Shared.IsValueCreated)
            throw new InvalidOperationException("The shared integration host has already been created.");
        configureSharedServices = configuration;
    }
    /// <summary>Creates an HTTP client for the assembly-shared default host.</summary>
    /// <returns>A client bound to the shared host's isolated Kestrel port.</returns>
    public new HttpClient CreateClient()
        => useOwnHost ? base.CreateClient() : Shared.Value.CreateClient();

    /// <summary>Creates a configured HTTP client for the assembly-shared default host.</summary>
    /// <param name="options">Client configuration.</param>
    /// <returns>A client bound to the shared host's isolated Kestrel port.</returns>
    public new HttpClient CreateClient(WebApplicationFactoryClientOptions options)
        => useOwnHost ? base.CreateClient(options) : Shared.Value.CreateClient(options);

    /// <summary>Stops and disposes the assembly-shared default host when it was created.</summary>
    public static async ValueTask DisposeSharedAsync()
    {
        if (Shared.IsValueCreated)
            await Shared.Value.DisposeAsync();
    }

    /// <summary>
    /// Creates another isolated Kestrel host with additional web-host configuration.
    /// </summary>
    /// <param name="configuration">Configuration applied before the application starts.</param>
    /// <returns>A separately owned Kestrel application factory.</returns>
    public new KestrelWebApplicationFactory<TEntryPoint> WithWebHostBuilder(
        Action<IWebHostBuilder> configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new KestrelWebApplicationFactory<TEntryPoint>(builder =>
        {
            configureWebHost?.Invoke(builder);
            configuration(builder);
            var configuredBroker = Environment.GetEnvironmentVariable("IFM_CONFIGURED_TEST_NATS_URL");
            if (!string.IsNullOrWhiteSpace(configuredBroker))
                builder.UseSetting("IFM_TEST_NATS_URL", configuredBroker);
        });
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .UseSetting("AppSettings:Fmp:Enabled", "false")
            .UseSetting("AppSettings:HistoricalAnalyticsWarmup:Enabled", "false")
            .UseSetting("AppSettings:IntrinsicTimeStrategyWorkflow:Enabled", "false")
            .UseSetting("AppSettings:IntrinsicTimeStrategyWorkflow:ProvisionDevelopmentMarketConditionAssessmentDefaults", "false")
            .UseSetting("AppSettings:IntrinsicTimeStrategyWorkflow:DevelopmentPortfolio:Enabled", "false")
            .UseSetting("ApplicationStartup:AutoStartAfterBootstrap", "false")
            .UseSetting("MarketDataRecovery:Enabled", "false");
        if (useOwnHost && configureWebHost is null && configureSharedServices is not null)
            builder.ConfigureServices(configureSharedServices);
        configureWebHost?.Invoke(builder);
    }
}
