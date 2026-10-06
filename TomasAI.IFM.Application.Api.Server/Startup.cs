using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Actor;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Supervisor.Shared.Service.Health;
using TomasAI.IFM.Domain.Supervisor.Shared.Service;
using TomasAI.IFM.Domain.Supervisor.Shared.Service.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Service.Metrics;
using TomasAI.IFM.Domain.Supervisor.Recovery;
using TomasAI.IFM.Domain.Supervisor.Recovery.Event.Projection;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using Hazelcast;
using Hazelcast.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using StackExchange.Redis;
using System.Buffers;
using System.Reflection;
using System.Text.Json.Serialization;
using TomasAI.IFM.Application.Actor.Client;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Application.MarketData.Worker;
using TomasAI.IFM.Application.Storage.EventSourceDb.HistoricalDataLoader;
using TomasAI.IFM.Application.MarketData.FinancialModelingPrep;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Application.Storage.PortfolioDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Application.Storage.OptionPricerDb.Schema;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Application.Storage.TradePlanDb.Schema;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Domain.Application.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Application.Storage.SystemAdminDb.Schema;
using TomasAI.IFM.Domain.Portfolio;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Projection;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.MarketData;
using TomasAI.IFM.Domain.MarketData.Query;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Recovery;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model.Processing;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Query;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Feed;
using TomasAI.IFM.Domain.MarketData.Securities;
using TomasAI.IFM.Domain.Reference;
using TomasAI.IFM.Domain.Reference.Services;
using TomasAI.IFM.Domain.SystemAdmin;
using TomasAI.IFM.Domain.OptionPricer;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;
using TomasAI.IFM.Domain.Trade;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Development;
using DomainApplicationActorAssembly = TomasAI.IFM.Domain.Application.Actor.ApplicationActorAssembly;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Caching.Redis;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;
using TomasAI.IFM.Framework.MarketData.TickAggregation;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Azure;
using TomasAI.IFM.Framework.Telemetry.Metrics;
using TomasAI.IFM.TradePlan;
using TomasAI.IFM.TradePlan.HostedService;
using TomasAI.IFM.Service.TradePosition;
using TomasAI.IFM.Service.TradePosition.HostedService;
using TomasAI.IFM.Domain.Application.Shared.ServiceApi;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProducers;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.Model;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Contracts;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.TradePlan.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.TradePlan.ServiceApi;

namespace TomasAI.IFM.Application.Api.Server;

public static class Startup
{
    /// <summary>
    /// Configures the specified <see cref="WebApplicationBuilder"/> with essential services, logging, and application
    /// settings.
    /// </summary>
    /// <remarks>This method performs the following configurations: <list type="bullet">
    /// <item><description>Sets up application configuration using JSON files, including environment-specific
    /// settings.</description></item> <item><description>Configures Serilog as the logging provider with console and
    /// asynchronous file and environment-controlled console sinks.</description></item> <item><description>Registers essential services, including controllers, JSON
    /// serialization options, Swagger, and Simple Injector.</description></item> </list> The method also initializes
    /// the <paramref name="logger"/> parameter with the application's logger instance and registers it as a singleton
    /// service.</remarks>
    /// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure.</param>
    /// <param name="logger">When this method returns, contains the configured <see cref="Microsoft.Extensions.Logging.ILogger"/> instance
    /// for the application. This parameter is passed uninitialized.</param>
    /// <returns>The configured <see cref="WebApplicationBuilder"/> instance.</returns>
    public static WebApplicationBuilder ConfigureApiServer(this WebApplicationBuilder builder, out Microsoft.Extensions.Logging.ILogger logger)
    {
        var siContainer = new Container();
        siContainer.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
            .MinimumLevel.Override(
                "Microsoft.Extensions.Diagnostics.HealthChecks.DefaultHealthCheckService",
                LogEventLevel.Fatal)
            .MinimumLevel.Override("System", LogEventLevel.Error)
            .Enrich.FromLogContext();
        if (builder.Environment.IsDevelopment()
            || builder.Configuration.GetValue("Logging:Console:Enabled", false))
        {
            loggerConfiguration.WriteTo.Console();
        }
        if (builder.Configuration.GetValue("Telemetry:Logs:Enabled", false))
            loggerConfiguration.WriteTo.Sink(new TomasAI.IFM.Framework.Telemetry.Logging.OtlpStructuredLogSink(
                builder.Configuration, "TomasAI.IFM.Application.Api.Server"));
        Log.Logger = loggerConfiguration
            .WriteTo.Async(
                sink => sink.File(
                    new Serilog.Formatting.Json.JsonFormatter(renderMessage: true),
                    "Logs/ifm-apiserver-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7),
                bufferSize: 4096,
                blockWhenFull: false,
                monitor: new TomasAI.IFM.Framework.Telemetry.Logging.AsyncLogBufferMonitor())
            .CreateLogger();
        _ = builder.WebHost.UseKestrel();
        _ = builder.Host.UseSerilog();

        logger = new Serilog.Extensions.Logging.SerilogLoggerFactory(Log.Logger, dispose: false)
            .CreateLogger<Program>();
        builder.Services.AddSingleton(logger);

        logger.LogInformationEvent("ApiServer","{Component}.{Method} "+"configure web api server...",nameof(Startup),nameof(ConfigureApiServer));
        builder.Services.ConfigureHttpJsonOptions(options =>
            ApiServerJson.Configure(options.SerializerOptions));
        builder.Services.AddOutputCache(options =>
        {
            options.SizeLimit = 16 * 1024 * 1024;
            options.MaximumBodySize = 2 * 1024 * 1024;
            options.AddPolicy(
                ApiOutputCachePolicies.HealthSnapshot,
                new HealthOutputCachePolicy());
            options.AddPolicy(ApiOutputCachePolicies.OperationalSnapshot, policy =>
                policy.Expire(ApiOutputCachePolicies.OperationalLifetime));
        });
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddSingleton(siContainer);
        builder.Services.AddSimpleInjector(siContainer);

        return builder;
    }

    /// <summary>
    /// Registers application services, including base services, query APIs, storage services, service handlers, event
    /// producers, and hosted services, into the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <remarks>This method organizes service registration into distinct categories, such as base services,
    /// query APIs, storage services, service handlers, event producers, and hosted services. Each category is
    /// registered through dedicated internal methods to ensure modularity and maintainability. <para> The method relies
    /// on configuration values provided by <paramref name="config"/> to initialize certain services, such as database
    /// connections and external API options. </para> <para> Logging is performed at various stages of the registration
    /// process to provide visibility into the services being registered. </para></remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the services will be added.</param>
    /// <param name="config">The <see cref="ConfigurationManager"/> used to retrieve configuration settings for service registration.</param>
    /// <param name="logger">The <see cref="Microsoft.Extensions.Logging.ILogger"/> used to log information during the registration process.</param>
    /// <param name="hostEnvironment">The hosting environment used to enforce Development-only recovery activation.</param>
    /// <returns>The updated <see cref="IServiceCollection"/> with the registered services.</returns>
    public static IServiceCollection RegisterServices(this IServiceCollection services, ConfigurationManager config, Microsoft.Extensions.Logging.ILogger logger, IHostEnvironment? hostEnvironment = null)
    {
        var siContainer = services.GetSimpleInjectorContainer();
        if (EventLogQualification.Active is { } qualification)
        {
            qualification.Validate(config);
            services.AddSingleton<Microsoft.Extensions.Http.IHttpMessageHandlerBuilderFilter, QualificationHttpFilter>();
        }
        var focusedActorIntegration = !string.IsNullOrWhiteSpace(config["IFM_TEST_ACTOR_DOMAIN"]);
        logger.LogInformationEvent("ApiServer","{Component}.{Method} "+"add web app services...",nameof(Startup),nameof(RegisterServices));
        RegisterBaseServices();
        RegisterCommandApiServices();
        RegisterEventApiServices();
        RegisterQueryApiServices();
        RegisterStorageServices();
        RegisterServiceHandlers();
        RegisterEventProducers();
        RegisterHostedServices();
        RegisterGenericTypes(siContainer, config, logger);
        return services;

        void RegisterBaseServices()
        {
            // add web app services...
            logger.LogInformationEvent("ApiServer","{Component}.{Method} "+"register base services...",nameof(Startup),nameof(RegisterBaseServices));
            services.Configure<HostOptions>(options =>
                options.BackgroundServiceExceptionBehavior =
                    BackgroundServiceExceptionBehavior.Ignore);
            services.AddIfmMetrics(config, "TomasAI.IFM.Application.Api.Server");
            var portfolioOperations = config.GetSection(PortfolioOperationalOptions.SectionName)
                .Get<PortfolioOperationalOptions>() ?? new PortfolioOperationalOptions();
            services.AddSingleton(portfolioOperations.Validate());
            services.AddSingleton<IPortfolioOperationalGuard, PortfolioOperationalGuard>();
            var applicationStartup = config.GetSection(ApplicationStartupOptions.SectionName)
                .Get<ApplicationStartupOptions>() ?? new ApplicationStartupOptions();
            services.AddSingleton(applicationStartup.Validate());
            var actorRuntimeStartup = config.GetSection(ActorRuntimeStartupOptions.SectionName)
                .Get<ActorRuntimeStartupOptions>() ?? new ActorRuntimeStartupOptions();
            services.AddSingleton(actorRuntimeStartup.Validate());
            var startupOrchestration = config.GetSection(StartupOrchestrationOptions.SectionName)
                .Get<StartupOrchestrationOptions>() ?? new StartupOrchestrationOptions();
            services.AddSingleton(startupOrchestration.Validate());
            var eventLogPersistence = config
                .GetSection(TomasAI.IFM.Application.Storage.EventSourceDb.Persistence.EventLogPersistenceOptions.SectionName)
                .Get<TomasAI.IFM.Application.Storage.EventSourceDb.Persistence.EventLogPersistenceOptions>()
                ?? new TomasAI.IFM.Application.Storage.EventSourceDb.Persistence.EventLogPersistenceOptions();
            services.AddSingleton(eventLogPersistence.Validate());
            var commandAuditPersistence = config
                .GetSection(TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditPersistenceOptions.SectionName)
                .Get<TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditPersistenceOptions>()
                ?? new TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditPersistenceOptions();
            services.AddSingleton(commandAuditPersistence.Validate());
            var inMemoryEventSourceActor = config
                .GetSection(InMemoryEventSourceActorOptions.SectionName)
                .Get<InMemoryEventSourceActorOptions>() ?? new InMemoryEventSourceActorOptions();
            services.AddSingleton(inMemoryEventSourceActor.Validate());
            services.AddSingleton<IApplicationStartupStatusStore, ApplicationStartupStatusStore>();
            services.AddSingleton<IApplicationStartupHandoffStatusStore, ApplicationStartupHandoffStatusStore>();
            services.AddSingleton<IApplicationStartupActivities, ApiApplicationStartupActivities>();
            services.AddSingleton<IApplicationBootstrapReadiness, ApplicationBootstrapReadiness>();
            services.AddSingleton<ActorRuntimeStartupSignal>();
            services.AddSingleton<IActorRuntimeStartupSignal>(provider =>
                provider.GetRequiredService<ActorRuntimeStartupSignal>());
            var deploymentIdentity = config.GetSection(DeploymentIdentityOptions.SectionName)
                .Get<DeploymentIdentityOptions>() ?? new DeploymentIdentityOptions();
            services.AddSingleton(deploymentIdentity);
            services.AddSingleton<DeploymentIdentityMonitor>();
            if (!focusedActorIntegration)
                services.AddHostedService<DeploymentIdentityEnforcementService>();
            var healthChecks = services.AddHealthChecks()
                .AddCheck<ProcessLivenessHealthCheck>("process_liveness", tags: ["live"])
                .AddCheck<DeploymentIdentityHealthCheck>("deployment_identity", tags: ["bootstrap", "launch", "ready"])
                .AddCheck<PortfolioOperationalHealthCheck>("portfolio_operations", tags: ["bootstrap", "launch", "ready"]);
            healthChecks
                .AddCheck<ActorRuntimeHealthCheck>("actor_runtime", tags: ["actor", "bootstrap", "launch", "ready"])
                .AddCheck<FmpConfigurationHealthCheck>("fmp_configuration", tags: ["application", "ready"])
                .AddCheck<LivePipelineHealthCheck>("live_pipeline", tags: ["application", "ready"])
                .AddCheck<MarketDataRuntimeHealthCheck>("market_data_runtime", tags: ["application", "ready"])
                .AddCheck<ApplicationLifecycleHealthCheck>("application_lifecycle", tags: ["application", "launch", "ready"]);
            var fmpEnabled = config.GetValue("AppSettings:Fmp:Enabled", true);
            services.AddFinancialModelingPrepMarketData(options =>
            {
                options.Enabled = fmpEnabled;
                options.LatestTreasuryLookbackDays = config.GetValue("AppSettings:Fmp:LatestTreasuryLookbackDays", 14);
                options.MaximumProviderWindowDays = config.GetValue("AppSettings:Fmp:MaximumProviderWindowDays", 90);
                options.MaximumRequestRangeDays = config.GetValue("AppSettings:Fmp:MaximumRequestRangeDays", 3_660);
                options.MaximumConcurrentRequests = config.GetValue("AppSettings:Fmp:MaximumConcurrentRequests", 2);
            });
            services.AddFinancialModelingPrepReferenceDataApi();
            // FMP continues to own the economic calendar. Pricing and yield-curve imports use the official feed.
            services.AddHttpClient("USTreasury", client => client.Timeout = TimeSpan.FromSeconds(30));
            services.AddSingleton<TomasAI.IFM.Framework.MarketData.ReferenceData.UsTreasuryCurve>(sp => new(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("USTreasury"), sp.GetRequiredService<TimeProvider>()));
            Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.Replace(services,
                ServiceDescriptor.Singleton<TomasAI.IFM.Framework.MarketData.Contracts.ITreasuryCurve>(
                    sp => sp.GetRequiredService<TomasAI.IFM.Framework.MarketData.ReferenceData.UsTreasuryCurve>()));
            services.AddSingleton(TomasAI.IFM.Framework.MarketData.ReferenceData.UsTreasuryCurve.ConversionPolicy);
            services.AddSingleton(TomasAI.IFM.Application.MarketData.Pricing.UsTreasuryPublicationCalendar.Default2026);
            if (EventLogQualification.Active is null && !focusedActorIntegration)
                services.AddHostedService<UsTreasuryRefreshHostedService>();
            services.AddFmpMarketDataImport(options =>
                options.MaximumRangeDays = config.GetValue("AppSettings:Fmp:MaximumImportRangeDays", 366));
            services.AddSingleton(new MarketDataImportPolicyOptions
            {
                Treasury = ParseImportPolicy(config, "AppSettings:Fmp:TreasuryDuplicatePolicy"),
                EconomicCalendar = ParseImportPolicy(config, "AppSettings:Fmp:EconomicCalendarDuplicatePolicy")
            }.Validate());
            services.AddOpenApiDocument();

            // Register HazelcastCache as the IDistributedCache implementation
            var hazelcastOptions = new HazelcastOptionsBuilder()
            .With(options =>
            {
                options.ClusterName = "ifm-cluster";
                options.Networking.Addresses.Add("localhost:5701");
            })
           .Build();

            // Configure the Hazelcast cache options, specifying a unique identifier for the cache map
            var cacheOptions = new HazelcastCacheOptions
            {
                CacheUniqueIdentifier = "api-server-cache",
            };
            if (EventLogQualification.Active is null)
                services.AddSingleton<IDistributedCache>(new HazelcastCache(hazelcastOptions, cacheOptions));
            else services.AddDistributedMemoryCache();

            services.AddHttpClient();
            var redisUri = config["IFM_TEST_REDIS_URL"] ?? config.GetValue<string>("AppSettings:RedisUri")!;
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisUri));
            services.AddSingleton<IRecoveryInfrastructureProbe, RedisRecoveryInfrastructureProbe>();
            services.AddSingleton<IRecoveryInfrastructureProbe, NatsRecoveryInfrastructureProbe>();
            foreach (var database in new (string Name, string Object, bool Procedure)[]
            {
                ("EventSourceActorDbConnection", "public.event_log", false),
                ("MarketDataServiceDbConnection", "market_data_service.watchdog_status_log", false),
                ("ConfigurationDbConnection", "reference_configuration.lookup_definition", false),
                ("SequenceIdDbConnection", "public.fn_get_next_sequence_id(text)", true)
            })
            {
                services.AddSingleton<IRecoveryInfrastructureProbe>(_ => new PostgreSqlRecoveryInfrastructureProbe(
                    database.Name, config.GetConnectionString(database.Name)
                        ?? throw new InvalidOperationException(database.Name + " is required for recovery qualification."),
                    database.Object, database.Procedure));
            }
            services.AddSingleton<IRecoveryInfrastructureProbe>(_ => new ScyllaRecoveryInfrastructureProbe(
                "MarketDataDbConnection", config.GetConnectionString("MarketDataDbConnection")
                    ?? throw new InvalidOperationException("MarketDataDbConnection is required for recovery qualification.")));
            // Validate the inert recovery policies during composition, before any actor/feed starts.
            // Runtime caller migration is a separate gate; registration does not enable hard reset.
            services.AddSingleton((config.GetSection("MarketDataRecovery:HardRecovery:Episode")
                .Get<DatabentoRecoveryEpisodePolicy>() ?? new DatabentoRecoveryEpisodePolicy()).Validate());
            services.AddSingleton((config.GetSection("MarketDataRecovery:HardRecovery:Attempts")
                .Get<DatabentoHardRecoveryPolicy>() ?? new DatabentoHardRecoveryPolicy()).Validate());
            services.AddSingleton((config.GetSection("MarketDataRecovery:HardRecovery:SoftGate")
                .Get<DatabentoSoftGatePolicy>() ?? new DatabentoSoftGatePolicy()).Validate());
            services.AddSingleton((config.GetSection("MarketDataRecovery:HardRecovery:FatalShutdown")
                .Get<FatalRecoveryShutdownOptions>() ?? new FatalRecoveryShutdownOptions()).Validate());
            // Explicit Development opt-in requires the scoped tick-storage proof/admission adapter.
            // Production remains prohibited until supervised live qualification is recorded.
            ApiDatabentoRecoveryHostActivation.Validate(
                config.GetValue<bool>("MarketDataRecovery:HardRecovery:Pipeline:Enabled"),
                hostEnvironment?.IsDevelopment() == true,
                config.GetValue<bool>("MarketDataRecovery:Stage3:Enabled"),
                downstreamProofAvailable: true);
            services.AddSingleton((config.GetSection("MarketDataRecovery:HardRecovery:Pipeline")
                .Get<ApiDatabentoRecoveryPipelinePolicy>() ?? new ApiDatabentoRecoveryPipelinePolicy()).Validate());
            services.AddSingleton((config.GetSection("MarketDataRecovery:HardRecovery:Runtime")
                .Get<ApiDatabentoHardRuntimePolicy>() ?? new ApiDatabentoHardRuntimePolicy()).Validate());
            services.AddSingleton((config.GetSection("MarketDataRecovery:HardRecovery:CandidateProof")
                .Get<DatabentoCandidateTickStorageProofPolicy>() ?? new DatabentoCandidateTickStorageProofPolicy()).Validate());
            var recoveryPipelineEnabled = config.GetValue<bool>("MarketDataRecovery:HardRecovery:Pipeline:Enabled");
            if (recoveryPipelineEnabled)
            {
                services.AddSingleton<IRecoveryFatalTelemetry, RecoveryFatalOpenTelemetry>();
                services.AddSingleton<IApiFatalRecoveryShutdown, ApiFatalRecoveryShutdown>();
                services.AddSingleton<IDatabentoRecoveryRequester>(ApiDatabentoRecoveryComposition.Create);
            }
            services.AddSingleton<DatabentoSoftRecoveryGate>();
            services.AddSingleton<IRedisCache, RedisCache>();
            services.AddSingleton<IBlackboardService, BlackboardService>();
            services.AddSingleton<IDataCacheService, LocalDataCacheService>();
            services.AddSingleton<IReferenceLookupService, ReferenceLookupActorService>();
            services.AddSingleton<IJsonSerializer, SystemTextJsonSerializer>();
            services.AddSingleton<IBinarySerializer, MessagePackBinarySerializer>();
            services.AddSingleton(provider => new IntrinsicTimeStrategyWorkflowOptions
            {
                Enabled = provider.GetRequiredService<IConfiguration>().GetValue("AppSettings:IntrinsicTimeStrategyWorkflow:Enabled", false),
                MarketConditionAssessmentProfileId = provider.GetRequiredService<IConfiguration>().GetValue("AppSettings:IntrinsicTimeStrategyWorkflow:MarketConditionAssessmentProfileId", "ES.Standard")!,
                ProvisionDevelopmentMarketConditionAssessmentDefaults = provider.GetRequiredService<IConfiguration>().GetValue(
                    "AppSettings:IntrinsicTimeStrategyWorkflow:ProvisionDevelopmentMarketConditionAssessmentDefaults", false),
                FundId = provider.GetRequiredService<IConfiguration>().GetValue("AppSettings:IntrinsicTimeStrategyWorkflow:FundId", 1),
                PortfolioId = provider.GetRequiredService<IConfiguration>().GetValue("AppSettings:IntrinsicTimeStrategyWorkflow:PortfolioId", 1),
                RequireWarmRegimeDiscoverySignals = provider.GetRequiredService<IConfiguration>().GetValue("AppSettings:IntrinsicTimeStrategyWorkflow:RequireWarmRegimeDiscoverySignals", true)
            });
            services.AddSingleton<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.IIntrinsicTimeWorkflowStartPolicy>(
                provider => provider.GetRequiredService<IntrinsicTimeStrategyWorkflowOptions>());
            var developmentPortfolio = config.GetSection(DevelopmentTradingPortfolioOptions.SectionName)
                .Get<DevelopmentTradingPortfolioOptions>() ?? new DevelopmentTradingPortfolioOptions();
            services.AddSingleton(developmentPortfolio.Validate());
            services.AddSingleton<DevelopmentTradingPortfolioProvisioner>();
            services.AddSingleton<DevelopmentTradingPortfolioIdentityRecovery>();
            services.AddSingleton<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Development.MarketConditionAssessmentDefaultProvisioner>();
            services.AddSingleton<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Development.RegimeDiscoveryDefaultProvisioner>();
            var regimeDiscoveryExecutionOptions = new RegimeDiscoveryExecutionOptions
            {
                MaximumExecutionDuration = config.GetValue(
                    $"{RegimeDiscoveryExecutionOptions.SectionName}:MaximumExecutionDuration",
                    RegimeDiscoveryExecutionOptions.DefaultMaximumExecutionDuration)
            };
            regimeDiscoveryExecutionOptions.Validate();
            services.AddSingleton(regimeDiscoveryExecutionOptions);
            services.AddSingleton<IBoundedContextFactoryResolver, BoundedContextFactoryResolver>(_ => new BoundedContextFactoryResolver(e => GetContainerInstance(siContainer, e)!));
            services.AddSingleton<IBoundedContextFactory, BoundedContextFactory>();
            services.AddSingleton<IActorStateFactoryResolver, ActorStateFactoryResolver>(_ => new ActorStateFactoryResolver(e => GetContainerInstance(siContainer, e)!));
            services.AddSingleton<IEventSourceActorStateFactory, EventSourceActorStateFactory>();
            //services.AddSingleton<IAlgorithmBuilder, AlgorithmBuilder>();
            services.AddSingleton<IExceptionDecoratorFactory>(_ => new ExceptionDecoratorFactory(e => GetContainerInstance(siContainer, e)!));
            services.AddSingleton<IValidationDecoratorFactory>(_ => new ValidationDecoratorFactory(e => GetContainerInstance(siContainer, e)!));
            services.AddSingleton<IEventServiceApiResolver>(_ => new EventServiceApiResolver(eventHandlerType => GetContainerInstance(siContainer, eventHandlerType)!));
            services.AddSingleton<IEventServiceHandlerResolver>(_ => new EventServiceHandlerResolver(eventHandlerType => GetContainerInstance(siContainer, eventHandlerType)!));
            services.AddSingleton<IOptionTradeLiveFeedMap, OptionTradeLiveFeedMap>();

            // register Event Model Actor instances...
            var admissionOptions = config
                .GetSection(ActorAdmissionOptions.SectionName)
                .Get<ActorAdmissionOptions>() ?? new ActorAdmissionOptions();
            admissionOptions.Validate();
            var actorInformationLoggingPolicy = (config
                .GetSection(ActorInformationLoggingOptions.SectionName)
                .Get<ActorInformationLoggingOptions>() ?? new ActorInformationLoggingOptions())
                .Compile();
            var natsConsumerOptions = config
                .GetSection(NatsConsumerOptions.SectionName)
                .Get<NatsConsumerOptions>() ?? new NatsConsumerOptions();
            var natsJetStreamConsumerOptions = config
                .GetSection(NatsJetStreamConsumerOptions.SectionName)
                .Get<NatsJetStreamConsumerOptions>() ?? new NatsJetStreamConsumerOptions();
            var brokerUrl = config["IFM_TEST_NATS_URL"];
            if (!string.IsNullOrWhiteSpace(brokerUrl))
            {
                natsConsumerOptions.Url = brokerUrl;
                natsJetStreamConsumerOptions.Url = brokerUrl;
            }
            natsConsumerOptions.Validate(admissionOptions);
            natsJetStreamConsumerOptions.Validate(admissionOptions);

            services.AddSingleton(admissionOptions);
            services.AddSingleton(actorInformationLoggingPolicy);
            services.AddSingleton<ActorAdmissionController>();
            services.AddSingleton<IActorSupervisor, ActorSupervisor>();
            services.AddSingleton<IActorService, ActorService>();
            services.AddSingleton<ISupervisorActorMetricsState, SupervisorActorMetricsState>();
            services.AddSingleton<TomasAI.IFM.Domain.Supervisor.Shared.Service.Health.Evaluation.SupervisorActorThreadHealthEvaluator>();
            services.AddSingleton<ISupervisorManagedActorMetricsSource>(provider =>
                new SupervisorManagedActorMetricsSource(
                    provider.GetRequiredService<IActorSupervisor>().RuntimeContext,
                    provider.GetRequiredService<TomasAI.IFM.Domain.Supervisor.Shared.Service.Health.Evaluation.SupervisorActorThreadHealthEvaluator>(),
                    provider.GetRequiredService<ILogger<SupervisorManagedActorMetricsSource>>()));
            services.AddSingleton<ISupervisorExceptionLog, SupervisorExceptionLog>();
            services.AddSingleton<ISupervisorHealthLlmAdvisorySink, NoOpSupervisorHealthLlmAdvisorySink>();
            services.AddSingleton<ISupervisorActorMetricsPollingService, SupervisorActorMetricsPollingService>();
            services.AddSingleton<ISupervisorBootstrap,
                TomasAI.IFM.Domain.Supervisor.Shared.Service.Lifecycle.SupervisorBootstrap>();
            services.AddSingleton<ISupervisorManagedActorLifecycle,
                TomasAI.IFM.Domain.Supervisor.Shared.Service.Lifecycle.SupervisorManagedActorLifecycle>();
            services.AddSingleton<ISupervisorOperatorAuthorizer>(_ =>
                new TomasAI.IFM.Domain.Supervisor.Shared.Service.Lifecycle.SupervisorOperatorAuthorizer(
                    config.GetSection("Supervisor:AllowedOperators").Get<string[]>() ?? []));
            services.AddSingleton<SupervisorOperationStore>();
            services.AddSingleton<RecoveryCanaryProjectionStore>();
            services.AddSingleton<SupervisorRecoveryCanaryProbe>();
            services.AddSingleton<ISupervisorOperationStore>(provider => provider.GetRequiredService<SupervisorOperationStore>());
            services.AddSingleton<ISupervisorHealthManager, SupervisorHealthManager>();
            services.AddSingleton<IActorRuntimeMetricsSourceProvider>(provider =>
                provider.GetRequiredService<IActorSupervisor>().RuntimeContext);
            services.AddSingleton<ISupervisorHistoryPersistence, SupervisorFileHistoryPersistence>();
            services.AddSingleton(new SupervisorHealthActionOptions
            {
                AutomaticMutationEnabled = config.GetValue<bool>("Supervisor:AutomaticMutationEnabled")
            });
            services.AddSingleton<SupervisorIncidentStore>();
            services.AddSingleton<ISupervisorIncidentStore>(provider => provider.GetRequiredService<SupervisorIncidentStore>());
            services.AddSingleton<SupervisorHistoryStore>();
            services.AddSingleton<ISupervisorHistoryStore>(provider => provider.GetRequiredService<SupervisorHistoryStore>());
            services.AddSingleton<ISupervisorHealthActionCoordinator, SupervisorHealthActionCoordinator>();
            services.AddSingleton<IActorRegistry>(_ =>
            {
                var actorTypes = (
                    from reg in siContainer.GetCurrentRegistrations()
                    where reg.ServiceType.IsClosedTypeOf(typeof(IActor<>))
                    select reg.ServiceType)
                    .Distinct()
                    .ToArray();
                return new ActorRegistry(actorTypes);
            });
            services.AddSingleton<IActorFactory>(_ => new ActorFactory(actorType => GetContainerInstance(siContainer, actorType)!,
                actorType => RealtimeActorReplacementFactory.Create(actorType, type => GetContainerInstance(siContainer, type)!)));
            services.AddSingleton<INatsProducerOptions>(_ => new NatsProducerOptions
            {
                Url = EventLogQualification.Active is null ? brokerUrl ?? new NatsProducerOptions().Url : EventLogQualification.Active.BrokerUrl
            });
            services.AddSingleton<INatsConsumerOptions>(natsConsumerOptions);
            services.AddSingleton<INatsEventListenerOptions>(_ => new NatsEventListenerOptions
            {
                Url = EventLogQualification.Active is null ? brokerUrl ?? new NatsEventListenerOptions().Url : EventLogQualification.Active.BrokerUrl
            });
            services.AddSingleton<NatsConnectionManager>();
            services.AddTransient<IActorProducer, NatsActorProducer>();
            services.AddTransient<IActorConsumer, NatsActorConsumer>();
            services.AddSingleton<INatsJetStreamProducerOptions>(_ => new NatsJetStreamProducerOptions
            {
                Url = EventLogQualification.Active is null ? brokerUrl ?? new NatsJetStreamProducerOptions().Url : EventLogQualification.Active.BrokerUrl
            });
            services.AddSingleton<INatsJetStreamConsumerOptions>(natsJetStreamConsumerOptions);
            services.AddSingleton<IDurableReplayQueue, NatsJSDurableReplayQueue>();
            services.AddTransient<IJSActorProducer, NatsJetStreamActorProducer>();
            services.AddTransient<IJSActorConsumer, NatsJetStreamActorConsumer>();
            services.AddSingleton<IContainerInstance>(provider => new ContainerInstance(type =>
            {
                var instance = provider.GetService(type)!;
                instance ??= GetContainerInstance(siContainer, type)!;
                return instance;
            }));
            services.AddTransient<IActorThreadQueue>(provider =>
                admissionOptions.MailboxImplementation switch
                {
                    ActorMailboxImplementation.Channel => new ActorThreadQueueV2(
                        provider.GetRequiredService<ActorAdmissionController>(),
                        admissionOptions.DefaultMailboxMessageLimit,
                        32,
                        32),
                    ActorMailboxImplementation.MpscRing => new ActorThreadQueueMpscRing(
                        provider.GetRequiredService<ActorAdmissionController>(),
                        admissionOptions.DefaultMailboxMessageLimit),
                    ActorMailboxImplementation.SpscRing => new ActorThreadQueueSpscRing(
                        provider.GetRequiredService<ActorAdmissionController>(),
                        admissionOptions.DefaultMailboxMessageLimit),
                    _ => throw new InvalidOperationException(
                        $"Unknown actor mailbox implementation '{admissionOptions.MailboxImplementation}'.")
                });


        }

        void RegisterCommandApiServices()
        {
            logger.LogInformationEvent("ApiServer","{Component}.{Method} "+"registering command api services...",nameof(Startup),nameof(RegisterCommandApiServices));
            services.AddSingleton<IApplicationCommandApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.ApplicationCommandApi(
                    provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<IMarketDataCommandApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.MarketDataCommandApi(
                    provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<IMarketDataFeedCommandApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.MarketDataFeedCommandApi(
                    provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<IMarketDataAnalyticsCommandApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.MarketDataAnalyticsCommandApi(
                    provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<IOptionPricerCommandApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.OptionPricerCommandApi(
                    provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<IReferenceCommandApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.ReferenceCommandApi(
                    provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<ITradePlanCommandApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.TradePlanCommandApi(
                    provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<ITradePlacementCommandApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.TradePlacementCommandApi(
                    provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.IPortfolioCommandApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.PortfolioCommandApi(provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.IPortfolioFinancialPolicyCommandApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.PortfolioFinancialPolicyCommandApi(provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<IStrategyPositionCommandApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.StrategyPositionCommandApi(
                    provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<ITradeOrderLifecycleApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.TradeOrderLifecycleApi(
                    provider.GetRequiredService<IActorProducer>()));
        }

        void RegisterEventApiServices()
        {
            logger.LogInformationEvent("ApiServer","{Component}.{Method} "+"registering actor event api services...",nameof(Startup),nameof(RegisterEventApiServices));
        }

        void RegisterQueryApiServices()
        {
            logger.LogInformationEvent("ApiServer","{Component}.{Method} "+"register query API services...",nameof(Startup),nameof(RegisterQueryApiServices));
            services.AddSingleton<IApplicationQueryApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.ApplicationQueryApi(
                    provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<IMarketDataAnalyticsQueryApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.MarketDataAnalyticsQueryApi(
                    provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<IMarketDataFeedQueryApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.MarketDataFeedQueryApi(
                    provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<IMarketDataQueryApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.MarketDataQueryApi(
                    provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<IDownloadLogQueryApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.DownloadLogQueryApi(
                    provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.ISelectionConstructionProfileResolver, TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.SelectionConstructionProfileResolver>();
            services.AddSingleton<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.ITradeSelectionQueryApi, TomasAI.IFM.Application.Api.Nats.Client.TradeSelectionQueryApi>();
            services.AddSingleton<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.IMarketConditionAssessmentQueryApi, TomasAI.IFM.Application.Api.Nats.Client.MarketConditionAssessmentQueryApi>();
            services.AddSingleton<IDownloadLogCommandApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.DownloadLogCommandApi(
                    provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<IOptionPricerQueryApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.OptionPricerQueryApi(
                    provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<ITradePlanQueryApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.TradePlanQueryApi(
                    provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IStrategyTradePlanQueryApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.StrategyTradePlanQueryApi(
                    provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<ITradeQueryApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.OptionTradeQueryApi(
                    provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<IReferenceQueryApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.ReferenceQueryApi(
                    provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.IPortfolioQueryApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.PortfolioQueryApi(provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.IPortfolioFundCommandApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.PortfolioFundCommandApi(
                    provider.GetRequiredService<IActorProducer>(),
                    provider.GetRequiredService<TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.IPortfolioQueryApi>()));
            services.AddSingleton<IStrategyPositionQueryApi>(provider =>
                new TomasAI.IFM.Application.Api.Nats.Client.StrategyPositionQueryApi(
                    provider.GetRequiredService<IActorProducer>()));
        }

        void RegisterStorageServices()
        {
            logger.LogInformationEvent("ApiServer","{Component}.{Method} "+"register storage services...",nameof(Startup),nameof(RegisterStorageServices));
            services.AddSingleton(_ =>
            {
                var isolatedPostgres = config["IFM_TEST_POSTGRES_CONNECTION"];
                return new DbConnectionSettings()
                .Add("EventSourceActorDbConnection", isolatedPostgres ?? config.GetConnectionString("EventSourceActorDbConnection")!, "System.Data.Postgres")
                .Add("ConfigurationDbConnection", isolatedPostgres ?? config.GetConnectionString("ConfigurationDbConnection")
                    ?? config.GetConnectionString("EventSourceActorDbConnection")!, "System.Data.Postgres")
                .Add("MarketDataServiceDbConnection", isolatedPostgres ?? config.GetConnectionString("MarketDataServiceDbConnection")
                    ?? config.GetConnectionString("EventSourceActorDbConnection")!, "System.Data.Postgres")
                .Add("SystemAdminDbConnection", isolatedPostgres ?? config.GetConnectionString("SystemAdminDbConnection")
                    ?? config.GetConnectionString("EventSourceActorDbConnection")!, "System.Data.Postgres")
                .Add("SequenceIdDbConnection", isolatedPostgres ?? config.GetConnectionString("SequenceIdDbConnection")!, "System.Data.Postgres")
                .Add("PortfolioDbConnection", isolatedPostgres ?? config.GetConnectionString("PortfolioDbConnection")
                    ?? config.GetConnectionString("EventSourceActorDbConnection")!, "System.Data.Postgres")
                .Add("MarketDataDbConnection", config.GetConnectionString("MarketDataDbConnection")!, "System.Data.ScyllaDb")
                .Add("OptionPricerDbConnection", config.GetConnectionString("OptionPricerDbConnection")!, "System.Data.ScyllaDb")
                .Add("ReferenceDbConnection", config.GetConnectionString("ReferenceDbConnection")!, "System.Data.ScyllaDb")
                .Add("SecuritiesDbConnection", config.GetConnectionString("SecuritiesDbConnection")!, "System.Data.ScyllaDb")
                .Add("TradeDbConnection", config.GetConnectionString("TradeDbConnection")!, "System.Data.ScyllaDb");
            });
            services.AddSingleton<IDbCache, DbCache>();
            services.AddSingleton<IDbContextResolver>(_ => new DbContextResolver(e => GetContainerInstance(siContainer, e)!));
            services.AddSingleton<IDbContextFactory, DbContextFactory>();
            services.AddSingleton<ISequenceIdDbContext, SequenceIdDbContext>();
            services.AddSingleton<ISequenceIdGenerator, PostgresSequenceIdGenerator>();
            services.AddSingleton<IPortfolioBusinessIdAllocator, PortfolioBusinessIdAllocator>();
            services.AddSingleton<TomasAI.IFM.Application.Storage.EventSourceDb.IPostgresEventTransaction,
                TomasAI.IFM.Application.Storage.EventSourceDb.PostgresEventTransaction>();
            services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioDb.OrderComposition.PortfolioOrderCompositionStore>();
            services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioDb.OrderComposition.PortfolioCloseOrderCompositionStore>();
            services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.PortfolioFinancialSchema>();
            services.AddSingleton(provider => new TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialDevelopmentPolicy(
                provider.GetRequiredService<IHostEnvironment>().IsDevelopment()));
            services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.IGeneralLedgerStore,
                TomasAI.IFM.Application.Storage.PortfolioFinancial.GeneralLedgerStore>();
            services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.ICapacityReservationStore,
                TomasAI.IFM.Application.Storage.PortfolioFinancial.CapacityReservationStore>();
            services.AddSingleton<TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model.FinancialIdentityAllocator>();
            services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.FinancialAuthorityPreparationStore>();
            services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.IFinancialHistoryProjection,
                TomasAI.IFM.Application.Storage.PortfolioFinancial.FinancialHistoryProjection>();
            services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.IFinancialQueryStore,
                TomasAI.IFM.Application.Storage.PortfolioFinancial.FinancialQueryStore>();
            services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Shared.Financial.IPortfolioFinancialApi>(provider =>
                    new TomasAI.IFM.Application.Api.Nats.Client.PortfolioFinancialApi(
                        provider.GetRequiredService<IActorProducer>()));
            services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.IPortfolioOrderCompositionApi>(provider =>
                (TomasAI.IFM.Application.Api.Nats.Client.PortfolioFinancialApi)provider.GetRequiredService<
                    TomasAI.IFM.Domain.Portfolio.Shared.Financial.IPortfolioFinancialApi>());
            services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.AccountingExportStore>();
            services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.EmulatorExecutionStore>();
            services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.IPortfolioAuthorityFence,
                TomasAI.IFM.Application.Storage.PortfolioFinancial.PortfolioAuthorityFence>();
            services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.FinancialHistoryJournal>();
            services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.ILedgerConfigurationStore,
                TomasAI.IFM.Application.Storage.PortfolioFinancial.LedgerConfigurationStore>();
            // Financial history recovery, workflow recovery, and capacity expiry polling are
            // temporarily disabled pending review of retry behavior and persisted-data compatibility.
            services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.FinancialWorkflowRecoveryJournal>();
            services.AddSingleton<TomasAI.IFM.Application.Storage.EventSourceDb.RiskHistoryJournal>();
            services.AddSingleton<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime.RiskObservationRecoveryService>();
            services.AddSingleton<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime.IWorkflowRiskProjection>(provider =>
                provider.GetRequiredService<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime.RiskObservationRecoveryService>());
            services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.CapacityExpiryDispatchStore>();
            services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(siContainer, type)!).Resolve<EventSourceActorDbContext>() as IEventSourceActorDbContext)!);
            services.AddSingleton<IPortfolioEventStore>(provider => new PortfolioEventStore(provider.GetRequiredService<IEventSourceActorDbContext>(),
                provider.GetRequiredService<TomasAI.IFM.Application.Storage.PortfolioFinancial.IPortfolioAuthorityFence>()));
            services.AddSingleton<IPortfolioProjectionRebuilder>(provider =>
                new PortfolioProjectionRebuilder(
                    provider.GetRequiredService<IPortfolioEventStore>(),
                    provider.GetRequiredService<IPortfolioDbWriteContext>()));
            services.AddSingleton<ICommandAuditLogger>(provider =>
                (ICommandAuditLogger)provider.GetRequiredService<IEventSourceActorDbContext>());
            services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(siContainer, type)!).Resolve<SequenceIdDbContext>() as ISequenceIdDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(siContainer, type)!).Resolve<PortfolioDbContext>() as PortfolioDbContext)!);
            services.AddSingleton<IPortfolioDbReadContext>(provider => provider.GetRequiredService<PortfolioDbContext>());
            services.AddSingleton<IPortfolioDbWriteContext>(provider => provider.GetRequiredService<PortfolioDbContext>());
            services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(siContainer, type)!).Resolve<MarketDataDbContext>() as IMarketDataDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(siContainer, type)!).Resolve<OptionPricerDbContext>() as IOptionPricerDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(siContainer, type)!).Resolve<ReferenceDbContext>() as IReferenceDbContext)!);
            services.AddSingleton<TradeStrategyFamilyBootstrapper>();
            services.AddSingleton<TomasAI.IFM.Application.MarketData.Contracts.ITradeStrategySymbolStore, TradeStrategySymbolStore>();
            services.AddSingleton<TomasAI.IFM.Application.MarketData.Contracts.IInstrumentDefinitionStore>(provider =>
                provider.GetRequiredService<IDbContextFactory>().ReferenceDb.InstrumentDefinitions);
            services.AddSingleton<TomasAI.IFM.Framework.MarketData.Contracts.Pricing.IOptionPricingConventionStore>(provider =>
                provider.GetRequiredService<IDbContextFactory>().SecuritiesDb);
            services.AddSingleton(provider => provider.GetRequiredService<IDbContextFactory>().ReferenceDb.OptionPricingReferenceBundles);
            services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.EuropeanOptionUniverse>();
            services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.TreasuryPricingProvider>();
            services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.IOptionPricingContextProvider,
                TomasAI.IFM.Application.MarketData.Pricing.OptionPricingContextProvider>();
            services.AddTradeStrategySymbolCatalog();
            services.AddSingleton<ITradeStrategyFamilyCatalogStore, TradeStrategyFamilyCatalogStore>();
            services.AddSingleton<TomasAI.IFM.Domain.Reference.TradeStrategyFamilies.TradeStrategyFamilyCreationService>();
            services.AddSingleton<TomasAI.IFM.Domain.Reference.StrategyCatalog.StrategyCatalogService>();
            services.AddSingleton(provider => new TomasAI.IFM.Application.Api.Server.ParameterSets.RsiHistoricalPilotOptions(
                provider.GetRequiredService<IHostEnvironment>().IsDevelopment() && config.GetValue<bool>("ParameterSets:RsiHistoricalPilotEnabled")));
            services.AddSingleton<TomasAI.IFM.Application.Api.Server.ParameterSets.IRsiHistoricalPilotStartup, TomasAI.IFM.Application.Api.Server.ParameterSets.RsiHistoricalPilotStartup>();
            services.AddSingleton<TomasAI.IFM.Domain.Reference.Shared.ParameterSets.IParameterSetsApi, TomasAI.IFM.Application.Api.Nats.Client.ParameterSetsApi>();
            services.AddSingleton<TomasAI.IFM.Domain.Reference.Shared.ParameterSets.IParameterRuntimeSnapshot>(provider =>
                new TomasAI.IFM.Domain.Reference.ParameterSets.Model.ParameterRuntimeSnapshotModel(
                    provider.GetRequiredService<IHostEnvironment>().IsDevelopment() && config.GetValue<bool>("ParameterSets:SingleUserDevelopmentEnabled")));
            services.AddSingleton<TomasAI.IFM.Domain.Reference.Shared.ParameterSets.IParameterAccessPolicy>(provider =>
                new TomasAI.IFM.Domain.Reference.ParameterSets.Model.SingleUserDevelopmentParameterAccessPolicy(
                    provider.GetRequiredService<IHostEnvironment>().EnvironmentName,
                    config.GetValue<bool>("ParameterSets:SingleUserDevelopmentEnabled")));
            services.AddSingleton<TomasAI.IFM.Domain.Reference.StrategyCatalog.StrategyCatalogMigration>();
            services.AddSingleton<TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.IStrategyCatalogReferences, TomasAI.IFM.Domain.Reference.StrategyCatalog.StrategyCatalogReferenceAdapter>();
            services.AddSingleton<TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.IStrategyCatalogCapabilities>(
                _ => new TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog.StrategyCatalogCapabilityRegistry(TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.TradeSelectionCatalogCapabilities.Create()
                    .Concat(TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.CompositionCatalogCapabilities.Create())
                    .Concat(TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model.RiskCatalogCapabilities.Create())));
            services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(siContainer, type)!).Resolve<SecuritiesDbContext>() as ISecuritiesDbContext)!);
            services.AddSingleton<IFuturesContractRolloverStore>(provider =>
                provider.GetRequiredService<ISecuritiesDbContext>());
            services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(siContainer, type)!).Resolve<TradeDbContext>() as ITradeDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(siContainer, type)!).Resolve<ConfigurationDbContext>() as IConfigurationDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(siContainer, type)!).Resolve<MarketDataServiceDbContext>() as MarketDataServiceDbContext)!);
            services.AddSingleton<IMarketDataServiceStore>(provider => provider.GetRequiredService<MarketDataServiceDbContext>());
            services.AddSingleton<TomasAI.IFM.Application.MarketData.Subscriptions.Persistence.IDurableSubscriptionIntentStore>(provider =>
                provider.GetRequiredService<MarketDataServiceDbContext>());
            services.AddSingleton<TomasAI.IFM.Application.MarketData.Subscriptions.DurableSubscriptionDelivery>();
            services.AddSingleton<TomasAI.IFM.Application.MarketData.Subscriptions.Persistence.ICommittedBusinessEventJournal,
                TomasAI.IFM.Application.Storage.EventSourceDb.PostgresCommittedBusinessEventJournal>();
            services.AddSingleton<TomasAI.IFM.Application.MarketData.Subscriptions.Persistence.ICommittedBusinessSubscriptionSource,
                TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.CommittedCompositionSubscriptionSource>();
            services.AddSingleton<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Realtime.CommittedCompositionSubscriptionProjector>();
            services.AddSingleton<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Realtime.ICommittedCompositionSubscriptionProjector>(provider =>
                provider.GetRequiredService<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Realtime.CommittedCompositionSubscriptionProjector>());
            services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.ICompositionRoutePlanStore>(provider =>
                provider.GetRequiredService<MarketDataServiceDbContext>());
            services.AddSingleton<IHistoricalDataLoaderStore, PostgresHistoricalDataLoaderStore>();
            services.AddSingleton<IHistoricalObservationStore>(provider =>
                provider.GetRequiredService<IMarketDataDbContext>());
            services.AddSingleton<EventSourceSchemaDb>();
            services.AddSingleton<SequenceIdSchemaDb>();
            services.AddSingleton<PortfolioSchemaDb>();
            services.AddSingleton<MarketDataSchemaDb>();
            services.AddSingleton<OptionPricerSchemaDb>();
            services.AddSingleton<ReferenceSchemaDb>();
            services.AddSingleton<SecuritiesSchemaDb>();
            services.AddSingleton<TradeSchemaDb>();
            services.AddSingleton<TradePlanSchemaDb>();
            services.AddSingleton<SystemAdminSchemaDb>();
            services.AddSingleton<ConfigurationSchemaDb>();
            services.AddSingleton<MarketDataServiceSchemaDb>();
            services.AddSingleton<ApplicationSchemaInitializer>();
            services.AddSingleton<RegimeDiscoveryMarketSignalSnapshotProvider>();
            services.AddSingleton<IRegimeDiscoveryMarketSignalSnapshotProvider>(provider =>
                provider.GetRequiredService<RegimeDiscoveryMarketSignalSnapshotProvider>());
            services.AddSingleton<IRegimeDiscoveryMarketSignalCache>(provider =>
                provider.GetRequiredService<RegimeDiscoveryMarketSignalSnapshotProvider>());



            services.AddSingleton<IMarketConditionEventRiskAdapter, MarketConditionEventRiskAdapter>();
            services.AddSingleton<IMarketConditionAssessmentSnapshotProvider, MarketConditionAssessmentSnapshotProvider>();


            services.AddSingleton(_ =>
                   new StorageUrlSettings()
                        .Add("DomainData", config.GetValue<string>("AppSettings:DomainDataStorageBaseUri")!)
                        .Add("QueryData", config.GetValue<string>("AppSettings:QueryDataStorageBaseUri")!)
                   );
        }

        void RegisterServiceHandlers()
        {
            logger.LogInformationEvent("ApiServer","{Component}.{Method} "+"register service handlers...",nameof(Startup),nameof(RegisterServiceHandlers));
            services.AddSingleton<IBoundedContextCommandResolver>(_ => new BoundedContextCommandResolver(cmdType => GetContainerInstance(siContainer, cmdType)!));
        }

        void RegisterEventProducers()
        {
            logger.LogInformationEvent("ApiServer","{Component}.{Method} "+"register event producers...",nameof(Startup),nameof(RegisterEventProducers));
            services.AddSingleton<ITradeEventProducer, TradeEventProducer>();
            services.AddSingleton<ITradePlacementEventProducer, TradePlacementEventProducer>();
            services.AddSingleton<IMarketDataEventProducer, MarketDataEventProducer>();
            services.AddSingleton<IStatusConsoleEventProducer, StatusConsoleEventProducer>();
        }

        void RegisterHostedServices()
        {
            logger.LogInformationEvent("ApiServer","{Component}.{Method} "+"register hosted services...",nameof(Startup),nameof(RegisterHostedServices));
            services.AddSingleton<IStatusConsoleWriter, StatusConsoleWriter>();
            services.AddSingleton<IAzureStorageOptions>(sp => config.GetSection("AzureStorage").Get<AzureStorageOptions>()!);
            services.AddSingleton<IAzureStorage, AzureStorage>();
            var dataset = config.GetValue<string>("AppSettings:Databento:Dataset")
                ?? "GLBX.MDP3";
            var profileName = config.GetValue<string>(
                "AppSettings:Databento:DeploymentProfile");
            var deploymentProfile = Enum.TryParse<FeedDeploymentProfile>(
                profileName, true, out var configuredProfile)
                    ? configuredProfile
                    : FeedDeploymentProfile.Development;
            var contracts = config
                .GetSection("AppSettings:Databento:Contracts")
                .Get<DatabentoContractRegistration[]>() ?? [];
            var feedOptions = DatabentoFeedOptions.ForProfile(
                deploymentProfile, dataset);
            var dataSourceName = config.GetValue<string>(
                "AppSettings:Databento:DataSource");
            if (Enum.TryParse<FeedDataSourceMode>(
                    dataSourceName, true, out var configuredDataSource))
            {
                feedOptions = feedOptions with
                {
                    DataSource = configuredDataSource
                };
            }
            logger.LogInformationEvent(
                "ApiServer",
                $"configure Databento market-data source: configured='{dataSourceName ?? "(default)"}', effective='{feedOptions.DataSource}'.");
            var configuredSynthetic = config
                .GetSection("AppSettings:Databento:Synthetic")
                .Get<SyntheticFeedOptions>();
            if (configuredSynthetic is not null
                && feedOptions.DataSource == FeedDataSourceMode.Synthetic)
            {
                feedOptions = feedOptions with
                {
                    Synthetic = configuredSynthetic
                };
            }
            SyntheticPersistenceIsolationGuard.Validate(
                feedOptions,
                config.GetConnectionString("EventSourceActorDbConnection"),
                config.GetConnectionString("MarketDataDbConnection"));
            var snapshotSource = feedOptions.DataSource == FeedDataSourceMode.Synthetic
                ? MarketOutlookSnapshotSource.Synthetic
                : MarketOutlookSnapshotSource.DatabentoLive;
            services.AddSingleton(new MarketOutlookSnapshotPersistencePolicy(snapshotSource));
            services.AddSingleton(new MarketOutlookSnapshotQueryPolicy(
                RejectSyntheticSnapshots:
                    feedOptions.DataSource == FeedDataSourceMode.DatabentoLive));
            var runtimeOptions = new DatabentoMarketDataRuntimeOptions
            {
                FeedOptions = feedOptions,
                OptionPricingRefresh = config.GetSection("AppSettings:Databento:OptionPricingRefresh")
                    .Get<TomasAI.IFM.Application.MarketData.Pricing.OptionPricingRefreshPolicy>() ?? new(),
                Contracts = contracts,
                FuturesQuoteBatchCapacity = config.GetValue(
                    "AppSettings:Databento:FuturesQuoteBatchCapacity", (ushort)64),
                FuturesOptionQuoteBatchCapacity = config.GetValue(
                    "AppSettings:Databento:FuturesOptionQuoteBatchCapacity", (ushort)64),
                TradeStrategySymbolDatasets = config.GetSection("AppSettings:Databento:TradeStrategySymbolDatasets")
                    .Get<string[]>() ?? []
            };
            services.AddDatabentoMarketDataServices();
            services.AddSingleton<IValueDateProvider, FuturesValueDateProvider>();
            services.AddSingleton<FuturesMarketSessionAuthority>();
            services.AddSingleton<IFuturesMarketSessionAuthority>(provider =>
                provider.GetRequiredService<FuturesMarketSessionAuthority>());
            services.AddHostedService<FuturesMarketSessionAuthorityHostedService>();
            services.AddSingleton<ITickAggregationEventPublisher,
                TickAggregationEventPublisher>();
            services.AddApplicationMarketDataApi(runtimeOptions);
            services.AddSingleton<FourHourDatabentoSeedReplay>();
            services.AddSingleton(new DatabentoWatchdogOptions
            {
                Enabled = config.GetValue("MarketDataRecovery:Enabled", true),
                NativeBackend = config.GetValue("MarketDataRecovery:NativeBackend", "Cpp")!,
                PollInterval = config.GetValue("MarketDataRecovery:PollInterval", TimeSpan.FromSeconds(15)),
                ProbeTimeout = config.GetValue("MarketDataRecovery:ProbeTimeout", TimeSpan.FromSeconds(1)),
                AttemptTwoDelay = config.GetValue("MarketDataRecovery:AttemptTwoDelay", TimeSpan.FromSeconds(5)),
                AttemptThreeDelay = config.GetValue("MarketDataRecovery:AttemptThreeDelay", TimeSpan.FromSeconds(15)),
                PersistenceRetryDelay = config.GetValue("MarketDataRecovery:PersistenceRetryDelay", TimeSpan.FromMilliseconds(100)),
                HardStallTimeout = config.GetValue("MarketDataRecovery:HardStallTimeout", TimeSpan.FromMinutes(5)),
                DatasetTeardownTimeout = config.GetValue("MarketDataRecovery:DatasetTeardownTimeout", TimeSpan.FromSeconds(10)),
                DatasetQualificationTimeout = config.GetValue("MarketDataRecovery:DatasetQualificationTimeout", TimeSpan.FromSeconds(30))
            }.Validate());
            var stage3Options = (config.GetSection("MarketDataRecovery:Stage3")
                .Get<DatabentoStage3Options>() ?? new DatabentoStage3Options()).Validate();
            if (runtimeOptions.FuturesQuoteBatchCapacity is 0 or > FuturesTickQuoteDataSegment.MaximumCount
                || runtimeOptions.FuturesOptionQuoteBatchCapacity is 0 or > FuturesTickQuoteDataSegment.MaximumCount)
                throw new ArgumentOutOfRangeException(
                    nameof(runtimeOptions), "Configured quote batch capacities must be between 1 and 4096.");
            if ((runtimeOptions.FuturesQuoteBatchCapacity > 64
                    || runtimeOptions.FuturesOptionQuoteBatchCapacity > 64)
                && (!stage3Options.Enabled
                    || feedOptions.DataSource != FeedDataSourceMode.Synthetic))
                throw new InvalidOperationException(
                    "Quote batches above 64 are qualified only for isolated Synthetic Development with bounded Stage 3 publishing.");
            services.AddSingleton((config.GetSection("MarketDataRecovery:Stage4")
                .Get<TomasAI.IFM.Application.MarketData.Subscriptions.Stage4SubscriptionOptions>()
                ?? new TomasAI.IFM.Application.MarketData.Subscriptions.Stage4SubscriptionOptions())
                .ValidateForApplicationStartup());
            if (stage3Options.Enabled && feedOptions.DataSource != FeedDataSourceMode.Synthetic
                && !(deploymentProfile == FeedDeploymentProfile.Development
                    && config.GetValue<bool>("MarketDataRecovery:Stage3:AllowDevelopmentLiveQualification")))
                throw new InvalidOperationException(
                    "Stage 3 live-provider workers require an explicit Development live-qualification opt-in.");
            services.AddSingleton(new TomasAI.IFM.Application.MarketData.Pricing.PricingSourceClockPolicy(
                deploymentProfile == FeedDeploymentProfile.Development && stage3Options.Enabled
                    && feedOptions.DataSource == FeedDataSourceMode.DatabentoLive
                    && config.GetValue<bool>("MarketDataRecovery:Stage3:AllowDevelopmentLiveQualification") ? 2000 : 0));
            services.AddSingleton(stage3Options);
            services.AddSingleton<DatasetDesiredSubscriptionRegistry>();
            if (stage3Options.Enabled)
            {
                services.AddSingleton<DatasetWorkerCurrentValues>();
                services.AddSingleton((config.GetSection("MarketDataRecovery:Stage3:RealtimePublisher")
                    .Get<TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation.RealtimeTickPublisherPolicy>()
                    ?? new TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation.RealtimeTickPublisherPolicy()).Validate());
            }
            services.AddSingleton(new DatabentoSupervisedWorkerOptions
            {
                DotNetHostPath = stage3Options.Enabled
                    ? ResolveDotNetHostPath() : Environment.ProcessPath!,
                WorkerAssemblyPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                    config.GetValue<string>("MarketDataRecovery:Stage3:WorkerAssemblyPath")
                    ?? (deploymentProfile == FeedDeploymentProfile.Development
                        ? config.GetValue<string>("MarketDataRecovery:Stage3:DevelopmentWorkerAssemblyPath")
                        : null)
                    ?? typeof(DatasetWorkerAssemblyMarker).Assembly.Location)),
                DeploymentProfile = deploymentProfile,
                DataSource = feedOptions.DataSource,
                OptionPricingRefresh = runtimeOptions.OptionPricingRefresh,
                Synthetic = feedOptions.Synthetic
            });
            services.AddSingleton<IDatabentoWatchdogPublisher, DatabentoWatchdogStatusConsolePublisher>();
            services.AddSingleton<ICurrentFuturesContractCatalog>(provider =>
                provider.GetRequiredService<ISecuritiesDbContext>());
            services.AddSingleton<IDatabentoContractAuthority, DatabentoContractAuthority>();
            if (stage3Options.Enabled)
                services.AddSingleton<IDatabentoLifecycleRuntime, SupervisedDatabentoLifecycleRuntime>();
            else
                services.AddSingleton<IDatabentoLifecycleRuntime, DatabentoLifecycleRuntime>();
            services.AddSingleton<DatabentoMarketDataWatchdogService>();
            services.AddSingleton<IMarketDataLifecycleRequests>(provider =>
                provider.GetRequiredService<DatabentoMarketDataWatchdogService>());
            services.AddHostedService(provider =>
                provider.GetRequiredService<DatabentoMarketDataWatchdogService>());
            if (stage3Options.Enabled)
                services.AddHostedService<RealtimePublicationRecoveryService>();
            var historicalOptions = new DatabentoHistoricalOptions
            {
                StagingRoot = Path.Combine(AppContext.BaseDirectory, "market-data-history"),
                SeriesProfiles = CreateHistoricalSeriesProfiles(dataset)
            };
            services.AddDatabentoHistoricalMarketDataServices(new DatabentoHistoricalProviderOptions
            {
                UseSyntheticProvider = feedOptions.DataSource == FeedDataSourceMode.Synthetic
            });
            services.AddApplicationMarketDataHistoricalApi(historicalOptions);
            services.AddSingleton<IHistoricalDailyReplayPublisher, FuturesEmaBbHistoricalDailyReplayPublisher>();
            services.AddSingleton(_ =>
            {
                var configured = config
                    .GetSection("AppSettings:HistoricalAnalyticsWarmup")
                    .Get<HistoricalAnalyticsWarmupOptions>() ?? new HistoricalAnalyticsWarmupOptions();
                return configured.Validate();
            });
            services.AddSingleton<HistoricalAnalyticsWarmupService>();
            services.AddSingleton<IFuturesTradeSessionBarSeriesResolver>(_ =>
                new PrefixFuturesTradeSessionBarSeriesResolver(
                    new Dictionary<string, MarketSeriesIdentity>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["ES"] = MarketSeriesIdentity.ForFuturesSeries(
                            new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1))
                    }));
            services.AddSingleton<FuturesTradeSessionBarAccumulatorRegistry>();
            services.AddSingleton(MarketOutlookHotCache.Shared);
            services.AddSingleton<IMarketOutlookHotCache>(provider =>
                provider.GetRequiredService<MarketOutlookHotCache>());
            services.AddSingleton<IMarketOutlookHotCacheWriter>(provider =>
                provider.GetRequiredService<MarketOutlookHotCache>());
            services.AddSingleton<MarketOutlookProcessorMetrics>();
            services.AddSingleton<DatabentoWatchdogMetrics>();
            services.AddSingleton<DatasetWorkerAdmissionRegistry>();
            services.AddSingleton<IRealtimeSourceAdmission>(provider =>
                provider.GetRequiredService<DatasetWorkerAdmissionRegistry>());
            services.AddSingleton<DatasetPublicationIngress>();
            services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.IOptionTradeEvidenceWriter>(provider =>
                provider.GetRequiredService<IMarketDataDbContext>());
            services.AddSingleton<DatasetWorkerProcessRecoveryService>();
            services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.ICompositionMarketDataApi>(provider =>
                provider.GetRequiredService<DatasetWorkerProcessRecoveryService>());
            services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.ICompositionPreparationStore>(provider =>
                provider.GetRequiredService<IMarketDataDbContext>());
            services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.CompositionPreparationService>();
            services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.QualifiedCompositionDiscovery>();
            services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.CompositionMarketPreparation>();
            services.AddSingleton<TomasAI.IFM.Application.MarketData.Subscriptions.DurableCompositionRuntime>();
            services.AddSingleton<TomasAI.IFM.Application.MarketData.Subscriptions.IDurableCompositionReconciler>(provider =>
                provider.GetRequiredService<TomasAI.IFM.Application.MarketData.Subscriptions.DurableCompositionRuntime>());
            services.AddSingleton<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.CompositionDiscoveryHandoff>();
            services.AddHostedService(provider => provider.GetRequiredService<TomasAI.IFM.Application.MarketData.Subscriptions.DurableCompositionRuntime>());
            services.AddSingleton<IDatabentoDatasetProcessRecovery>(provider =>
                provider.GetRequiredService<DatasetWorkerProcessRecoveryService>());
            services.AddSingleton<MarketDataOperationsHealthService>();
            services.AddSingleton<IMarketDataOperationsRecorder>(provider =>
                new CompositeMarketDataOperationsRecorder(
                    provider.GetRequiredService<MarketOutlookProcessorMetrics>(),
                    provider.GetRequiredService<DatabentoWatchdogMetrics>(),
                    provider.GetRequiredService<MarketDataOperationsHealthService>()));
            services.AddSingleton<MarketOutlookUpdateChannel>();
            services.AddSingleton<IMarketOutlookUpdateWriter>(provider =>
                provider.GetRequiredService<MarketOutlookUpdateChannel>());
            services.AddSingleton<IMarketOutlookUpdateReader>(provider =>
                provider.GetRequiredService<MarketOutlookUpdateChannel>());
            services.AddSingleton<LatestMarketOutlookSnapshotPublisher>();
            services.AddSingleton<IMarketOutlookSnapshotPublisher>(provider =>
                provider.GetRequiredService<LatestMarketOutlookSnapshotPublisher>());
            services.AddHostedService(provider =>
                provider.GetRequiredService<LatestMarketOutlookSnapshotPublisher>());
            services.AddSingleton<MarketOutlookUpdateProcessor>();
            services.AddSingleton<IMarketOutlookOperations>(provider =>
                provider.GetRequiredService<MarketOutlookUpdateProcessor>());
            services.AddHostedService(provider =>
                provider.GetRequiredService<MarketOutlookUpdateProcessor>());
            services.AddHostedService<MarketDataOperationsHealthObserver>();
            services.AddSingleton<LivePipelineEvidence>();
            services.AddSingleton<FuturesItiSignalRuntimeTelemetry>();
            services.AddSingleton<MarketDataRuntimeHealthCheck>();
            services.AddSingleton<ActorRuntimeHealthCheck>();
            services.AddSingleton((config.GetSection("MarketDataRecovery:LivePipeline")
                .Get<LivePipelineMonitorOptions>() ?? new LivePipelineMonitorOptions()).Validate()); services.AddSingleton<ILivePipelineProbe, LivePipelineProbe>();
            services.AddSingleton<LivePipelineMonitor>();
            services.AddHostedService(provider => provider.GetRequiredService<LivePipelineMonitor>());
            services.AddHostedService<HistoricalDailyAnalyticsInitializationService>();
            services.AddHostedService<FuturesRolloverPreparationHostedService>();
            services.AddSingleton((config.GetSection("AppSettings:Databento:OptionExpiryCalendar")
                .Get<OptionContractExpiryCalendarOptions>() ?? new()).Validate());
            services.AddSingleton<OptionContractExpiryCalendarRefreshService>();
            services.AddHostedService<OptionContractExpiryCalendarStartupService>();
            services.AddHostedService<ApplicationStartupCommandDispatcher>();
            var fmpScheduleOptions = (config
                .GetSection("AppSettings:Fmp:Schedule")
                .Get<FmpImportScheduleOptions>() ?? new FmpImportScheduleOptions()).Validate();
            services.AddSingleton(fmpScheduleOptions);
            services.AddHostedService<FmpMarketDataImportHostedService>();

            //services.AddSingleton<IMarketDataFeedEventConsumer, MarketDataFeedEventConsumer>();
            services.AddSingleton<IFuturesBarDataTimer, FuturesBarDataTimer>();
            //services.AddHostedService<MarketDataFeedHostedService>();

            // trade position hosted service...
            services.AddSingleton<ITradePositionService, TradePositionService>();
            services.AddSingleton<ITradePositionEventConsumer, TradePositionEventConsumer>();
            services.AddHostedService<TradePositionHostedService>();

            // trade plan hosted service...
            services.AddSingleton<ITradePlanService, TradePlanService>();
            services.AddSingleton<ITradePlanEventConsumer, TradePlanEventConsumer>();
            services.AddHostedService<TradePlanHostedService>();

            // trade placement hosted service...
            //services.AddSingleton<ITradePlacementEventService, TradePlacementEventService>();
            //services.AddSingleton<ITradePlacementEventConsumer, TradePlacementEventConsumer>();
            //services.AddSingleton<ITradePlacementTimer, TradePlacementTimer>();
            //services.AddHostedService<TradePlacementHostedService>();

            // market data analytics hosted service...
            //services.AddSingleton<IFuturesRsiSignalTimer, FuturesRsiSignalTimer>();
        }
    }

    /// <summary>
    /// Registers Simple Injector components before the service provider can start hosted services.
    /// </summary>
    /// <remarks>Completing these registrations before the host is built prevents background actor/projector services
    /// from locking the container while registrations are still being added.</remarks>
    /// <param name="config">Application configuration containing projector reliability settings.</param>
    /// <param name="siContainer">The Simple Injector container owned by this application host.</param>
    /// <param name="logger">The <see cref="Microsoft.Extensions.Logging.ILogger"/> used to log configuration events.</param>
    static void RegisterGenericTypes(
        Container siContainer,
        ConfigurationManager config,
        Microsoft.Extensions.Logging.ILogger logger)
    {
        logger.LogInformationEvent("ApiServer","{Component}.{Method} "+"register open generic handlers...",nameof(Startup),nameof(RegisterGenericTypes));
        siContainer.RegisterSingleton<IDataCacheService, DataCacheService>();
        RegisterTradeBrokerEmulator(siContainer, config, logger);
        siContainer.RegisterSingleton<IDatabaseBackupExecutionOutbox, DatabaseBackupExecutionOutbox>();
        var projectorReliabilityOptions = config
            .GetSection(EventProjectorReliabilityOptions.SectionName)
            .Get<EventProjectorReliabilityOptions>() ?? new EventProjectorReliabilityOptions();
        siContainer.RegisterInstance((projectorReliabilityOptions with
        {
            DurableProjectorAllowlist = FinancialJetStreamPolicy.DurableProjectors
        }).Validate());
        var eventLogPersistenceOptions = config
            .GetSection(TomasAI.IFM.Application.Storage.EventSourceDb.Persistence.EventLogPersistenceOptions.SectionName)
            .Get<TomasAI.IFM.Application.Storage.EventSourceDb.Persistence.EventLogPersistenceOptions>()
            ?? new TomasAI.IFM.Application.Storage.EventSourceDb.Persistence.EventLogPersistenceOptions();
        siContainer.RegisterInstance(eventLogPersistenceOptions.Validate());
        var commandAuditPersistenceOptions = config
            .GetSection(TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditPersistenceOptions.SectionName)
            .Get<TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditPersistenceOptions>()
            ?? new TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditPersistenceOptions();
        siContainer.RegisterInstance(commandAuditPersistenceOptions.Validate());
        var inMemoryEventSourceActorOptions = config
            .GetSection(InMemoryEventSourceActorOptions.SectionName)
            .Get<InMemoryEventSourceActorOptions>() ?? new InMemoryEventSourceActorOptions();
        siContainer.RegisterInstance(inMemoryEventSourceActorOptions.Validate());

        var domainAssemblies = new List<Assembly>
        {
            ApplicationActorAssembly.Current,
            DomainApplicationActorAssembly.Current,
            PortfolioActorAssembly.Current,
            MarketDataActorAssembly.Current,
            MarketDataAnalyticsActorAssembly.Current,
            MarketDataFeedActorAssembly.Current,
            OptionPricerActorAssembly.Current,
            ReferenceActorAssembly.Current,
            SecuritiesActorAssembly.Current,
            SystemAdminActorAssembly.Current,
            TradeActorAssembly.Current,
            TomasAI.IFM.Domain.Supervisor.SupervisorActorAssembly.Current,
            TomasAI.IFM.Domain.BrokerAccount.BrokerAccountActorAssembly.Current
        };
        var selectedDomain = config["IFM_TEST_ACTOR_DOMAIN"];
        var actorAssemblies = string.IsNullOrWhiteSpace(selectedDomain)
            ? domainAssemblies
            : domainAssemblies
                .Where(assembly =>
                    assembly == TomasAI.IFM.Domain.Supervisor.SupervisorActorAssembly.Current
                    || selectedDomain
                        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                        .Contains(assembly.GetName().Name, StringComparer.Ordinal))
                .ToList();
        if (actorAssemblies.Count == 0)
            throw new InvalidOperationException($"No actor assembly matched IFM_TEST_ACTOR_DOMAIN '{selectedDomain}'.");

        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => assembly.GetName().Name is { } assemblyName
                                      && !assemblyName.EndsWith("Tests", StringComparison.Ordinal)
                                      && !assemblyName.EndsWith("Benchmarks", StringComparison.Ordinal))
            .ToList();
        assemblies.AddRange(domainAssemblies);
        assemblies = assemblies.Distinct().ToList();
        var repositoryTypes = ObjectRepositoryDiscovery.Discover(assemblies)
            .Where(static type => type != typeof(SystemAdminDbContext)
                                  && type != typeof(EventSourceActorDbContext)
                                  && type != typeof(TomasAI.IFM.Domain.BrokerAccount.Query.Model.BrokerAccountReadStore))
            .ToArray();
        siContainer.Register(typeof(IObjectRepository<>), repositoryTypes, Lifestyle.Transient);
        // The repository contract must use the same singleton as the account reader and projector.
        siContainer.AddRegistration<IObjectRepository<TomasAI.IFM.Domain.BrokerAccount.Query.Model.BrokerAccountReadStore>>(
            siContainer.GetCurrentRegistrations().Single(registration => registration.ServiceType ==
                typeof(TomasAI.IFM.Domain.BrokerAccount.Query.Model.BrokerAccountReadStore)).Registration);
        var eventSourceRegistration = EventLogQualification.Active is null
            ? Lifestyle.Singleton.CreateRegistration<EventSourceActorDbContext>(siContainer)
            : Lifestyle.Singleton.CreateRegistration(() => new EventSourceActorDbContext(
                siContainer.GetInstance<IDbConnectionSettings>(),
                siContainer.GetInstance<IDbContextFactory>(),
                siContainer.GetInstance<IBlackboardService>(),
                siContainer.GetInstance<Microsoft.Extensions.Logging.ILogger<DbProvider>>(),
                eventLogPersistenceOptions, commandAuditPersistenceOptions, true), siContainer);
        siContainer.AddRegistration<IObjectRepository<EventSourceActorDbContext>>(eventSourceRegistration);
        var systemAdminRegistration = Lifestyle.Singleton.CreateRegistration<SystemAdminDbContext>(siContainer);
        siContainer.AddRegistration<ISystemAdminDbContext>(systemAdminRegistration);
        siContainer.AddRegistration<IObjectRepository<SystemAdminDbContext>>(systemAdminRegistration);
        siContainer.Register(typeof(IActor<>), actorAssemblies, Lifestyle.Singleton);
        siContainer.Register(typeof(ICommandActorContext<>), domainAssemblies, Lifestyle.Singleton);
        siContainer.Register(typeof(IFunctionActorContext<>), domainAssemblies, Lifestyle.Singleton);
        // Both context contracts share the same singleton registration.
        siContainer.AddRegistration<TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.Actor.ICapacityReservationFunctionContext>(
            siContainer.GetCurrentRegistrations().Single(registration => registration.ServiceType ==
                typeof(IFunctionActorContext<TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.Actor.CapacityReservationFunctionActor>)).Registration);
        siContainer.AddRegistration<TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.Actor.ICapacityConsumptionFunctionContext>(
            siContainer.GetCurrentRegistrations().Single(registration => registration.ServiceType ==
                typeof(IFunctionActorContext<TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.Actor.CapacityConsumptionFunctionActor>)).Registration);
        siContainer.AddRegistration<TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.Actor.IPortfolioOrderCompositionFunctionContext>(
            siContainer.GetCurrentRegistrations().Single(registration => registration.ServiceType ==
                typeof(IFunctionActorContext<TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.Actor.PortfolioOrderCompositionFunctionActor>)).Registration);
        siContainer.AddRegistration<TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.Actor.IPortfolioCloseOrderCompositionFunctionContext>(
            siContainer.GetCurrentRegistrations().Single(registration => registration.ServiceType ==
                typeof(IFunctionActorContext<TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.Actor.PortfolioCloseOrderCompositionFunctionActor>)).Registration);
        siContainer.AddRegistration<IRegimeDiscoveryFunctionContext>(
            siContainer.GetCurrentRegistrations().Single(registration =>
                registration.ServiceType == typeof(IFunctionActorContext<RegimeDiscoveryFunctionActor>)).Registration);
        siContainer.AddRegistration<IMarketConditionFunctionContext>(
            siContainer.GetCurrentRegistrations().Single(registration =>
                registration.ServiceType == typeof(IFunctionActorContext<MarketConditionFunctionActor>)).Registration);
        siContainer.AddRegistration<ITradeSelectionFunctionContext>(
            siContainer.GetCurrentRegistrations().Single(registration =>
                registration.ServiceType == typeof(IFunctionActorContext<TradeSelectionFunctionActor>)).Registration);
        siContainer.AddRegistration<IOrderCompositionFunctionContext>(
            siContainer.GetCurrentRegistrations().Single(registration =>
                registration.ServiceType == typeof(IFunctionActorContext<OrderCompositionFunctionActor>)).Registration);
        siContainer.AddRegistration<IRiskManagementFunctionContext>(
            siContainer.GetCurrentRegistrations().Single(registration =>
                registration.ServiceType == typeof(IFunctionActorContext<RiskManagementFunctionActor>)).Registration);
        siContainer.AddRegistration<TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.Actor.IIronCondorTradePlanFunctionContext>(
            siContainer.GetCurrentRegistrations().Single(registration => registration.ServiceType ==
                typeof(IFunctionActorContext<TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.Actor.IronCondorTradePlanFunctionActor>)).Registration);
        siContainer.AddRegistration<TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.Actor.IVerticalSpreadTradePlanFunctionContext>(
            siContainer.GetCurrentRegistrations().Single(registration => registration.ServiceType ==
                typeof(IFunctionActorContext<TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.Actor.VerticalSpreadTradePlanFunctionActor>)).Registration);
        siContainer.AddRegistration<TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.Actor.IFuturesTradePlanFunctionContext>(
            siContainer.GetCurrentRegistrations().Single(registration => registration.ServiceType ==
                typeof(IFunctionActorContext<TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.Actor.FuturesTradePlanFunctionActor>)).Registration);
        siContainer.Register(typeof(IEventActorContext<>), domainAssemblies, Lifestyle.Singleton);
        siContainer.Register(typeof(IQueryActorContext<>), domainAssemblies, Lifestyle.Singleton);
        siContainer.Register(typeof(IRealtimeActorContext<>), domainAssemblies, Lifestyle.Singleton);
        siContainer.Register(typeof(IActorStateDenormalizer<>), assemblies, Lifestyle.Singleton);
        siContainer.Register(typeof(IEventSourceActorStateRepository<>), assemblies, Lifestyle.Singleton);
        siContainer.Register(typeof(IResidentEventSourceActorStateRepository<>), assemblies, Lifestyle.Singleton);
        siContainer.Register(typeof(IEventSourceFunctionStateRepository<,>), assemblies, Lifestyle.Singleton);
        if (siContainer.GetRegistration<IEventSourceFunctionStateRepository<
            TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.State.PortfolioOrderCompositionFunctionState,
            TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.EvaluatePortfolioOrderCompositionCommand>>(false) is null)
            siContainer.Register<IEventSourceFunctionStateRepository<
                TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.State.PortfolioOrderCompositionFunctionState,
                TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.EvaluatePortfolioOrderCompositionCommand>,
                TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.State.PortfolioOrderCompositionFunctionStateRepository>(Lifestyle.Singleton);
        siContainer.Register(typeof(IFunctionProjector<>), domainAssemblies, Lifestyle.Singleton);
        siContainer.Register(typeof(IEventProjector<>), domainAssemblies, Lifestyle.Singleton);
        siContainer.Register<TomasAI.IFM.Domain.Portfolio.CapacityReservation.Emulator.Command.EmulatorExecutionCommandServices>(Lifestyle.Singleton);
        siContainer.Register<TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command.GeneralLedgerCommandServices>(Lifestyle.Singleton);
        siContainer.Register<TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command.LedgerConfigurationCommandServices>(Lifestyle.Singleton);
        siContainer.Register<TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query.FinancialBookPreparation>(Lifestyle.Singleton);
        siContainer.Register<TomasAI.IFM.Domain.Portfolio.GeneralLedger.IPortfolioTradeAccountingApi,
            TomasAI.IFM.Domain.Portfolio.GeneralLedger.BrokerExecutionAccountingApi>(Lifestyle.Singleton);
        siContainer.Register<TomasAI.IFM.Domain.Portfolio.GeneralLedger.IPortfolioTradeValuationApi,
            TomasAI.IFM.Domain.Portfolio.GeneralLedger.PortfolioTradeValuationApi>(Lifestyle.Singleton);
        siContainer.Register<TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query.FinancialAuthorityPreparation>(Lifestyle.Singleton);
        siContainer.Register<TomasAI.IFM.Domain.Portfolio.CapacityReservation.Command.CapacityReservationCommandServices>(Lifestyle.Singleton);
        siContainer.Register(
            typeof(TomasAI.IFM.Application.EventProjector.Realtime.Contracts.IRealtimeProjector<>),
            domainAssemblies,
            Lifestyle.Singleton);
        siContainer.Register(typeof(IEventSourceActorState<>), assemblies, Lifestyle.Transient);
        logger.LogInformationEvent("ApiServer","{Component}.{Method} "+"open generic handlers registered",nameof(Startup),nameof(RegisterGenericTypes));
    }

    static void RegisterTradeBrokerEmulator(SimpleInjector.Container container, ConfigurationManager config, Microsoft.Extensions.Logging.ILogger logger)
    {
        var accountAlias = config["TradeBroker:Emulator:AccountAlias"] ?? "IFM-EMULATOR-PAPER";
        var startingCash = config.GetValue<decimal?>("TradeBroker:Emulator:StartingCash") ?? 1_000_000m;
        var scenario = new TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.EmulatorScenario(
            accountAlias, "USD", startingCash, 0.65m, TimeSpan.FromSeconds(2));
        container.RegisterInstance(scenario);
        var ledgerPath = config["TradeBroker:Emulator:LedgerPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "data", "trade-broker", "emulator-ledger.json");
        container.RegisterInstance<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.IEmulatorLedgerStore>(
            new TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.FileEmulatorLedgerStore(ledgerPath));
        container.RegisterSingleton<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.IEmulatorClock,
            TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.SystemEmulatorClock>();
        container.RegisterSingleton<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.EmulatorLedger>(() =>
            new TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.EmulatorLedger(
                container.GetInstance<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.EmulatorScenario>(),
                container.GetInstance<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.IEmulatorClock>(),
                container.GetInstance<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.IEmulatorLedgerStore>()));
        container.RegisterSingleton<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.OfflineFillSimulation>(() =>
            new(container.GetInstance<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.EmulatorLedger>(),
                new() {
                    Enabled = config.GetValue("TradeBroker:Emulator:OfflineSimulation:Enabled", true),
                    CompletionTime = TimeSpan.FromSeconds(config.GetValue("TradeBroker:Emulator:OfflineSimulation:CompletionSeconds", 30)),
                    MaximumUnitsPerFill = config.GetValue("TradeBroker:Emulator:OfflineSimulation:MaximumUnitsPerFill", 3),
                    RandomSeed = config.GetValue("TradeBroker:Emulator:OfflineSimulation:RandomSeed", 1)
                }, () => !container.GetInstance<IFuturesMarketSessionAuthority>().Current.IsMarketOpen, logger));
        container.RegisterSingleton<TomasAI.IFM.Framework.TradeBroker.Contracts.IFrameworkOrderExecutionBroker>(() =>
            new FrozenEmulatorOrderExecutionBroker(
                new TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.OrderExecution.EmulatedOrderExecutionBroker(
                    container.GetInstance<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.EmulatorLedger>()),
                container.GetInstance<IFuturesMarketSessionAuthority>(),
                container.GetInstance<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.IEmulatorClock>(),
                container.GetInstance<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.OfflineFillSimulation>()));
        container.RegisterSingleton<TomasAI.IFM.Framework.TradeBroker.Contracts.IFrameworkBrokerAccount,
            TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.BrokerAccount.EmulatedBrokerAccount>();
        container.RegisterSingleton<TomasAI.IFM.Application.TradeBroker.Contracts.ITradeBroker>(() =>
            new TomasAI.IFM.Application.TradeBroker.InteractiveBrokersEmulatorTradeBroker(
                container.GetInstance<TomasAI.IFM.Framework.TradeBroker.Contracts.IFrameworkOrderExecutionBroker>(),
                container.GetInstance<TomasAI.IFM.Framework.TradeBroker.Contracts.IFrameworkBrokerAccount>(), logger));
        container.RegisterSingleton<TomasAI.IFM.Domain.Trade.Order.Broker.Realtime.BrokerOrderObservationBridge>();
        container.RegisterSingleton<TomasAI.IFM.Domain.Trade.Order.Broker.Query.Model.IBrokerOrderReadStore,
            TomasAI.IFM.Domain.Trade.Order.Broker.Query.Model.BrokerOrderReadStore>();
        container.RegisterSingleton<TomasAI.IFM.Domain.BrokerAccount.Query.Model.BrokerAccountReadStore>();
        container.RegisterSingleton<TomasAI.IFM.Domain.BrokerAccount.Query.Model.IBrokerAccountProjectionWriter>(
            () => container.GetInstance<TomasAI.IFM.Domain.BrokerAccount.Query.Model.BrokerAccountReadStore>());
        container.RegisterSingleton<TomasAI.IFM.Domain.BrokerAccount.Query.Model.IBrokerAccountReadStore>(
            () => container.GetInstance<TomasAI.IFM.Domain.BrokerAccount.Query.Model.BrokerAccountReadStore>());
        container.RegisterSingleton<TomasAI.IFM.Domain.BrokerAccount.Realtime.BrokerAccountObservationBridge>();
    }

    /// <summary>Configures middleware and verifies the completed dependency-injection container.</summary>
    public static WebApplication ConfigureRequestPipeline(this WebApplication app, Microsoft.Extensions.Logging.ILogger logger)
    {
        var siContainer = app.Services.GetRequiredService<Container>();
        // configure the HTTP request pipeline...
        siContainer.RegisterInstance(
                app.Services.GetRequiredService<IFuturesMarketSessionAuthority>());
        app.Services.UseSimpleInjector(siContainer);
        siContainer.Verify();
        logger.LogInformationEvent("ApiServer","{Component}.{Method} "+"configure HTTP request pipeline...",nameof(Startup),nameof(ConfigureRequestPipeline));
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
                options.RoutePrefix = string.Empty;
            });
        }
        else if (EventLogQualification.Active is null)
        {
            app.UseHttpsRedirection();
        }
        app.UseOutputCache();
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live"),
            ResponseWriter = WriteHealthResponseAsync
        });
        app.MapHealthChecks("/health/bootstrap", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("bootstrap"),
            ResponseWriter = WriteHealthResponseAsync
        }).CacheOutput(ApiOutputCachePolicies.HealthSnapshot);
        app.MapHealthChecks("/health/launch-ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("launch"),
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            },
            ResponseWriter = WriteHealthResponseAsync
        }).CacheOutput(ApiOutputCachePolicies.HealthSnapshot);
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            },
            ResponseWriter = WriteHealthResponseAsync
        }).CacheOutput(ApiOutputCachePolicies.HealthSnapshot);
        app.MapHealthChecks("/health/actors", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("actor"),
            ResponseWriter = WriteHealthResponseAsync
        }).CacheOutput(ApiOutputCachePolicies.HealthSnapshot);
        logger.LogInformationEvent("ApiServer","{Component}.{Method} "+"web app configuration completed",nameof(Startup),nameof(ConfigureRequestPipeline));
        return app;

        static async Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                status = report.Status.ToString(),
                totalDurationMilliseconds = report.TotalDuration.TotalMilliseconds,
                entries = report.Entries.ToDictionary(
                    entry => entry.Key,
                    entry => new
                    {
                        status = entry.Value.Status.ToString(),
                        entry.Value.Description,
                        durationMilliseconds = entry.Value.Duration.TotalMilliseconds,
                        entry.Value.Data
                    })
            });
        }
    }

    static IReadOnlyList<DatabentoHistoricalSeriesProfile> CreateHistoricalSeriesProfiles(string dataset)
    {
        var es = MarketSeriesIdentity.ForFuturesSeries(
            new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1));
        var vxFront = MarketSeriesIdentity.ForFuturesSeries(
            new FuturesSeriesId("VX", "calendar-front", "unadjusted", 1));
        var vxSecond = MarketSeriesIdentity.ForFuturesSeries(
            new FuturesSeriesId("VX", "calendar-second", "unadjusted", 1));
        return
        [
            new DatabentoHistoricalSeriesProfile
            {
                MarketSeriesIdentity = es.Format(), Dataset = dataset,
                Symbols = ["ES.c.0"], Symbology = HistoricalSymbology.Continuous
            },
            new DatabentoHistoricalSeriesProfile
            {
                MarketSeriesIdentity = vxFront.Format(), Dataset = dataset,
                Symbols = ["VX.c.0"], Symbology = HistoricalSymbology.Continuous
            },
            new DatabentoHistoricalSeriesProfile
            {
                MarketSeriesIdentity = vxSecond.Format(), Dataset = dataset,
                Symbols = ["VX.c.1"], Symbology = HistoricalSymbology.Continuous
            }
        ];
    }

    static string ResolveDotNetHostPath()
    {
        var configured = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return Path.GetFullPath(configured);
        var process = Environment.ProcessPath;
        if (process is not null && string.Equals(Path.GetFileNameWithoutExtension(process),
                "dotnet", StringComparison.OrdinalIgnoreCase))
            return process;
        var runtimeDirectory = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        var installedHost = Path.GetFullPath(Path.Combine(runtimeDirectory, "..", "..", "..",
            OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
        if (File.Exists(installedHost)) return installedHost;
        throw new InvalidOperationException(
            "Stage 3 could not locate the dotnet host; set DOTNET_HOST_PATH explicitly.");
    }

    static ImportDuplicatePolicy ParseImportPolicy(IConfiguration config, string configurationKey)
    {
        var value = config.GetValue<string>(configurationKey);
        if (string.IsNullOrWhiteSpace(value))
            return ImportDuplicatePolicy.Overwrite;
        if (Enum.TryParse<ImportDuplicatePolicy>(value, true, out var policy)
            && Enum.IsDefined(policy))
            return policy;
        throw new InvalidOperationException(
            $"Configuration '{configurationKey}' must be Overwrite or Reject.");
    }

    /// <summary>
    /// Retrieves an instance of the specified type from the service container.
    /// </summary>
    /// <remarks>If the container does not contain an instance of the specified type, or if an error occurs
    /// during retrieval,  the method returns <see langword="null"/> instead of throwing an exception.</remarks>
    /// <param name="siContainer">The Simple Injector container owned by this application host.</param>
    /// <param name="commandType">The <see cref="Type"/> of the object to retrieve from the container.</param>
    /// <returns>An instance of the specified type if it exists in the container; otherwise, <see langword="null"/>.</returns>
    static object? GetContainerInstance(Container siContainer, Type commandType)
    {
        object? commandInstance;
        try
        {
            commandInstance = siContainer.GetInstance(commandType);
        }
        catch
        {
            commandInstance = null;
        }
        return commandInstance;
    }


    static Container GetSimpleInjectorContainer(this IServiceCollection services)
    {
        return services
                   .Select(static descriptor => descriptor.ImplementationInstance)
                   .OfType<Container>()
                   .SingleOrDefault()
               ?? throw new InvalidOperationException("Simple Injector has not been configured for this host.");
    }
}
