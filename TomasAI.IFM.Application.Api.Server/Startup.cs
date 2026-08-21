using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using Hazelcast;
using Hazelcast.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using StackExchange.Redis;
using System.Buffers;
using System.Reflection;
using System.Text.Json.Serialization;
using TomasAI.IFM.Application.Actor.Client;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.FinancialModelingPrep;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.LogDb;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.FundDb.Schema;
using TomasAI.IFM.Application.Storage.LogDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Application.Storage.OptionPricerDb.Schema;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Application.Storage.SystemAdminDb.Schema;
using TomasAI.IFM.Domain.Fund;
using TomasAI.IFM.Domain.MarketData;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics;
using TomasAI.IFM.Domain.MarketData.Feed;
using TomasAI.IFM.Domain.MarketData.Securities;
using TomasAI.IFM.Domain.Reference;
using TomasAI.IFM.Domain.Reference.Services;
using TomasAI.IFM.Domain.SystemAdmin;
using TomasAI.IFM.Domain.OptionPricer;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;
using TomasAI.IFM.Domain.Trade;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Caching.Redis;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.DataProtection;
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
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
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
    readonly static Container _siContainer = new();

    /// <summary>
    /// Configures the specified <see cref="WebApplicationBuilder"/> with essential services, logging, and application
    /// settings.
    /// </summary>
    /// <remarks>This method performs the following configurations: <list type="bullet">
    /// <item><description>Sets up application configuration using JSON files, including environment-specific
    /// settings.</description></item> <item><description>Configures Serilog as the logging provider with console and
    /// HTTP sinks.</description></item> <item><description>Registers essential services, including controllers, JSON
    /// serialization options, Swagger, and Simple Injector.</description></item> </list> The method also initializes
    /// the <paramref name="logger"/> parameter with the application's logger instance and registers it as a singleton
    /// service.</remarks>
    /// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure.</param>
    /// <param name="logger">When this method returns, contains the configured <see cref="Microsoft.Extensions.Logging.ILogger"/> instance
    /// for the application. This parameter is passed uninitialized.</param>
    /// <returns>The configured <see cref="WebApplicationBuilder"/> instance.</returns>
    public static WebApplicationBuilder ConfigureApiServer(this WebApplicationBuilder builder, out Microsoft.Extensions.Logging.ILogger logger)
    {
        _siContainer.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        _ = builder.WebHost
                       .ConfigureAppConfiguration((ctx, configBuilder) => {
                           configBuilder.SetBasePath(Directory.GetCurrentDirectory())
                               .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                               .AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                               .AddEnvironmentVariables();

                           var config = configBuilder.Build();
                           //var telemetryServerBaseUri = config.GetValue<string>("AppSettings:TelemetryServerBaseUri")!;

                           Log.Logger = new LoggerConfiguration()
                               .MinimumLevel.Information()
                               .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                               .MinimumLevel.Override("System", LogEventLevel.Error)
                               .Enrich.FromLogContext()
                               .WriteTo.Console()
                               .WriteTo.File("Logs/ifm-apiserver-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
                               .CreateLogger();
                       })
                       .UseKestrel();
        _ = builder.Host.UseSerilog();

        // configure api server...
        var serviceProvider = builder.Services.BuildServiceProvider();
        logger = serviceProvider.GetRequiredService<ILogger<Program>>() as Microsoft.Extensions.Logging.ILogger;
        builder.Services.AddSingleton(logger);

        logger.LogInformationEvent("ApiServer", "configure web api server...");
        builder.Services.AddControllers()
            .AddNewtonsoftJson()
            .AddJsonOptions(options => {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddSimpleInjector(_siContainer);
        
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
    /// <returns>The updated <see cref="IServiceCollection"/> with the registered services.</returns>
    public static IServiceCollection RegisterServices(this IServiceCollection services, ConfigurationManager config, Microsoft.Extensions.Logging.ILogger logger)
    {
        logger.LogInformationEvent("ApiServer", "add web app services...");
        RegisterBaseServices();
        RegisterCommandApiServices();
        RegisterEventApiServices();
        RegisterQueryApiServices();
        RegisterStorageServices();
        RegisterServiceHandlers();
        RegisterEventProducers();
        RegisterHostedServices();
        RegisterGenericTypes(config, logger);
        return services;

        void RegisterBaseServices()
        {
            // add web app services...
            logger.LogInformationEvent("ApiServer", "register base services...");
            services.AddIfmMetrics(config, "TomasAI.IFM.Application.Api.Server");
            services.AddHealthChecks()
                .AddCheck<ActorRuntimeHealthCheck>("actor_runtime", tags: ["ready"])
                .AddCheck<FmpConfigurationHealthCheck>("fmp_configuration", tags: ["ready"])
                .AddCheck<MarketDataRuntimeHealthCheck>("market_data_runtime", tags: ["ready"]);
            var dataProtectionKeyPath = config.GetValue<string>("DataProtection:KeyPath");
            if (!string.IsNullOrWhiteSpace(dataProtectionKeyPath))
            {
                Directory.CreateDirectory(dataProtectionKeyPath);
                services.AddDataProtection()
                    .SetApplicationName("TomasAI.IFM.Application.Api.Server")
                    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));
            }
            services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
            services.AddAuthorization();

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
            services.AddFmpMarketDataImport(options =>
                options.MaximumRangeDays = config.GetValue("AppSettings:Fmp:MaximumImportRangeDays", 366));
            services.AddSingleton(new ExternalMarketDataCompatibilityOptions
            {
                TreasuryLookbackDays = config.GetValue("AppSettings:Fmp:CompatibilityTreasuryLookbackDays", 14)
            }.Validate());
            services.AddSingleton(new MarketDataImportPolicyOptions
            {
                Treasury = ParseImportPolicy(config, "AppSettings:Fmp:TreasuryDuplicatePolicy"),
                EconomicCalendar = ParseImportPolicy(config, "AppSettings:Fmp:EconomicCalendarDuplicatePolicy")
            }.Validate());
            services.AddOpenApiDocument();

            // Register HazelcastCache as the IDistributedCache implementation
            var hazelcastOptions = new HazelcastOptionsBuilder()
            .With(options => {
                options.ClusterName = "ifm-cluster";
                options.Networking.Addresses.Add("localhost:5701");
            })
           .Build();

            // Configure the Hazelcast cache options, specifying a unique identifier for the cache map
            var cacheOptions = new HazelcastCacheOptions
            {
                CacheUniqueIdentifier = "api-server-cache",
            };
            services.AddSingleton<IDistributedCache>(new HazelcastCache(hazelcastOptions, cacheOptions));

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddHttpClient();
              var redisUri = config.GetValue<string>("AppSettings:RedisUri")!;
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisUri));
            services.AddSingleton<IRedisCache, RedisCache>();
            services.AddSingleton<IBlackboardService, BlackboardService>();
            services.AddSingleton<IDataCacheService, LocalDataCacheService>();
            services.AddSingleton<IReferenceLookupService, ReferenceLookupActorService>();
            services.AddSingleton<IJsonSerializer, NewtonSoftJsonSerializer>();
            services.AddSingleton<IBinarySerializer, MessagePackBinarySerializer>();
            services.AddSingleton<IBoundedContextFactoryResolver, BoundedContextFactoryResolver>(_ => new BoundedContextFactoryResolver(e => GetContainerInstance(e)!));
            services.AddSingleton<IBoundedContextFactory, BoundedContextFactory>();
            services.AddSingleton<IActorStateFactoryResolver, ActorStateFactoryResolver>(_ => new ActorStateFactoryResolver(e => GetContainerInstance(e)!));
            services.AddSingleton<IEventSourceActorStateFactory, EventSourceActorStateFactory>();
            //services.AddSingleton<IAlgorithmBuilder, AlgorithmBuilder>();
            services.AddSingleton<IExceptionDecoratorFactory>(_ => new ExceptionDecoratorFactory(e => GetContainerInstance(e)!));
            services.AddSingleton<IValidationDecoratorFactory>(_ => new ValidationDecoratorFactory(e => GetContainerInstance(e)!));
            services.AddSingleton<IEventServiceApiResolver>(_ => new EventServiceApiResolver(eventHandlerType => GetContainerInstance(eventHandlerType)!));
            services.AddSingleton<IEventServiceHandlerResolver>(_ => new EventServiceHandlerResolver(eventHandlerType => GetContainerInstance(eventHandlerType)!));
            services.AddSingleton<IOptionTradeLiveFeedMap, OptionTradeLiveFeedMap>();

            // register Event Model Actor instances...
            var admissionOptions = config
                .GetSection(ActorAdmissionOptions.SectionName)
                .Get<ActorAdmissionOptions>() ?? new ActorAdmissionOptions();
            admissionOptions.Validate();
            var natsConsumerOptions = config
                .GetSection(NatsConsumerOptions.SectionName)
                .Get<NatsConsumerOptions>() ?? new NatsConsumerOptions();
            natsConsumerOptions.Validate(admissionOptions);
            var natsJetStreamConsumerOptions = config
                .GetSection(NatsJetStreamConsumerOptions.SectionName)
                .Get<NatsJetStreamConsumerOptions>() ?? new NatsJetStreamConsumerOptions();
            natsJetStreamConsumerOptions.Validate(admissionOptions);

            services.AddSingleton(admissionOptions);
            services.AddSingleton<ActorAdmissionController>();
            services.AddSingleton<IActorSupervisor, ActorSupervisor>();
            services.AddSingleton<IActorService, ActorService>();
            services.AddSingleton<IActorRegistry>(_ => {
                var actorTypes = (
                    from reg in _siContainer.GetCurrentRegistrations()
                    where reg.ServiceType.IsClosedTypeOf(typeof(IActor<>))
                    select reg.ServiceType)
                    .Distinct()
                    .ToArray();
                return new ActorRegistry(actorTypes);
            });
            services.AddSingleton<IActorFactory>( _ => new ActorFactory(actorType => GetContainerInstance(actorType)!));
            services.AddSingleton<INatsProducerOptions, NatsProducerOptions>();
            services.AddSingleton<INatsConsumerOptions>(natsConsumerOptions);
            services.AddSingleton<INatsEventListenerOptions, NatsEventListenerOptions>();
            services.AddSingleton<NatsConnectionManager>();
            services.AddTransient<IActorProducer, NatsActorProducer>();
            services.AddTransient<IActorConsumer, NatsActorConsumer>();
            services.AddSingleton<INatsJetStreamProducerOptions, NatsJetStreamProducerOptions>();
            services.AddSingleton<INatsJetStreamConsumerOptions>(natsJetStreamConsumerOptions);
            services.AddSingleton<IDurableReplayQueue, NatsJSDurableReplayQueue>();
            services.AddTransient<IJSActorProducer, NatsJetStreamActorProducer>();
            services.AddTransient<IJSActorConsumer, NatsJetStreamActorConsumer>();
            services.AddSingleton<IContainerInstance>(provider => new ContainerInstance(type => {
                var instance = provider.GetService(type)!;
                instance ??= GetContainerInstance(type)!;
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
            logger.LogInformationEvent("ApiServer", "registering command api services...");
            services.AddSingleton<ICommandServiceApiOptions>(_ => new CommandServiceApiOptions(config.GetValue<string>("AppSettings:CommandServerBaseUri")!));
            services.AddSingleton<ICommandServiceApi, CommandServiceApiClient>();
            services.AddSingleton<IApplicationCommandApi, ApplicationCommandApi>();
            services.AddSingleton<IFundCommandApi, FundCommandApi>();
            services.AddSingleton<IMarketDataCommandApi, MarketDataCommandApi>();
            services.AddSingleton<IMarketDataFeedCommandApi, MarketDataFeedCommandApi>();
            services.AddSingleton<IMarketDataAnalyticsCommandApi, MarketDataAnalyticsCommandApi>();
            services.AddSingleton<IOptionPricerCommandApi, OptionPricerCommandApi>();
            services.AddSingleton<IReferenceCommandApi, ReferenceCommandApi>();
            services.AddSingleton<ITradeCommandApi, OptionTradeCommandApi>();
            services.AddSingleton<ITradePlanCommandApi, TradePlanCommandApi>();
            services.AddSingleton<ITradePlacementCommandApi, TradePlacementCommandApi>();
            services.AddSingleton<IActorMarketDataAnalyticsCommandApiFactory, TomasAI.IFM.Domain.MarketData.Analytics.Command.Api.ActorMarketDataAnalyticsCommandApiFactory>();
            services.AddSingleton<IActorMarketDataFeedCommandApiFactory, TomasAI.IFM.Domain.MarketData.Feed.Command.Api.ActorMarketDataFeedCommandApiFactory>();
            services.AddSingleton<IActorOptionPricerCommandApiFactory, TomasAI.IFM.Domain.OptionPricer.Command.Api.ActorOptionPricerCommandApiFactory>();
            services.AddSingleton<IActorTradeCommandApiFactory, TomasAI.IFM.Domain.Trade.Command.Api.ActorTradeCommandApiFactory>();
        }

        void RegisterEventApiServices()
        {
            logger.LogInformationEvent("ApiServer", "registering actor event api services...");
            services.AddSingleton<IActorFundEventApiFactory, TomasAI.IFM.Domain.Fund.Event.Api.ActorFundEventApiFactory>();
            services.AddSingleton<IActorMarketDataFeedEventApiFactory, TomasAI.IFM.Domain.MarketData.Feed.Event.Api.ActorMarketDataFeedEventApiFactory>();
        }

        void RegisterQueryApiServices()
        {
            logger.LogInformationEvent("ApiServer", "register query API services...");
            services.AddSingleton<IQueryServiceApiOptions>(_ => new QueryServiceApiOptions(config.GetValue<string>("AppSettings:QueryServerBaseUri")!));
            services.AddSingleton<IQueryServiceApi, QueryServiceApiClient>();
            services.AddSingleton<IFundQueryApi, FundQueryApi>();
            services.AddSingleton<IActorFundQueryApi, TomasAI.IFM.Domain.Fund.Query.Api.ActorFundQueryApi>();
            services.AddSingleton<IMarketDataAnalyticsQueryApi, MarketDataAnalyticsQueryApi>();
            services.AddSingleton<IActorMarketDataAnalyticsQueryApi, TomasAI.IFM.Domain.MarketData.Analytics.Query.Api.ActorMarketDataAnalyticsQueryApi>();
            services.AddSingleton<IMarketDataFeedQueryApi, MarketDataFeedQueryApi>();
            services.AddSingleton<IActorMarketDataFeedQueryApi, TomasAI.IFM.Domain.MarketData.Feed.Query.Api.ActorMarketDataFeedQueryApi>();
            services.AddSingleton<IMarketDataQueryApi, MarketDataQueryApi>();
            services.AddSingleton<IActorMarketDataQueryApi, TomasAI.IFM.Domain.MarketData.Query.Api.ActorMarketDataQueryApi>();
            services.AddSingleton<IOptionPricerQueryApi, OptionPricerQueryApi>();
            services.AddSingleton<IActorOptionPricerQueryApi, TomasAI.IFM.Domain.OptionPricer.Query.Api.ActorOptionPricerQueryApi>();
            services.AddSingleton<ITradePlanQueryApi, TradePlanQueryApi>();
            services.AddSingleton<ITradeQueryApi, OptionTradeQueryApi>();
            services.AddSingleton<IActorTradeQueryApi, TomasAI.IFM.Domain.Trade.Query.Api.ActorTradeQueryApi>();
            services.AddSingleton<IReferenceQueryApi, ReferenceQueryApi>();
            services.AddSingleton<IActorReferenceQueryApi, TomasAI.IFM.Domain.Reference.Query.Api.ActorReferenceQueryApi>();
        }

        void RegisterStorageServices()
        {
            logger.LogInformationEvent("ApiServer", "register storage services...");
            services.AddSingleton(_ =>
            new DbConnectionSettings()
                .Add("EventSourceActorDbConnection", config.GetConnectionString("EventSourceActorDbConnection")!, "System.Data.Postgres")
                .Add("SystemAdminDbConnection", config.GetConnectionString("SystemAdminDbConnection")
                    ?? config.GetConnectionString("EventSourceActorDbConnection")!, "System.Data.Postgres")
                .Add("LogDbConnection", config.GetConnectionString("LogDbConnection")!, "System.Data.Postgres")
                .Add("SequenceIdDbConnection", config.GetConnectionString("SequenceIdDbConnection")!, "System.Data.Postgres")
                .Add("FundDbConnection", config.GetConnectionString("FundDbConnection")!, "System.Data.ScyllaDb")
                .Add("MarketDataDbConnection", config.GetConnectionString("MarketDataDbConnection")!, "System.Data.ScyllaDb")
                .Add("OptionPricerDbConnection", config.GetConnectionString("OptionPricerDbConnection")!, "System.Data.ScyllaDb")
                .Add("ReferenceDbConnection", config.GetConnectionString("ReferenceDbConnection")!, "System.Data.ScyllaDb")
                .Add("SecuritiesDbConnection", config.GetConnectionString("SecuritiesDbConnection")!, "System.Data.ScyllaDb")
                .Add("TradeDbConnection", config.GetConnectionString("TradeDbConnection")!, "System.Data.ScyllaDb")
            );
            services.AddSingleton<IDbCache, DbCache>();
            services.AddSingleton<IDbContextResolver>(_ => new DbContextResolver(e => GetContainerInstance(e)!));
            services.AddSingleton<IDbContextFactory, DbContextFactory>();
            services.AddSingleton<ISequenceIdDbContext, SequenceIdDbContext>();
            services.AddSingleton<ISequenceIdGenerator, PostgresSequenceIdGenerator>();
            services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(type)!).Resolve<EventSourceActorDbContext>() as IEventSourceActorDbContext)!);
            services.AddSingleton<ICommandDuplicateGuard>(provider =>
                (ICommandDuplicateGuard)provider.GetRequiredService<IEventSourceActorDbContext>());
            services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(type)!).Resolve<LogDbContext>() as ILogDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(type)!).Resolve<SequenceIdDbContext>() as ISequenceIdDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(type)!).Resolve<FundDbContext>() as IFundDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(type)!).Resolve<MarketDataDbContext>() as IMarketDataDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(type)!).Resolve<OptionPricerDbContext>() as IOptionPricerDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(type)!).Resolve<ReferenceDbContext>() as IReferenceDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(type)!).Resolve<SecuritiesDbContext>() as ISecuritiesDbContext)!);
            services.AddSingleton<IFuturesContractRolloverStore>(provider =>
                provider.GetRequiredService<ISecuritiesDbContext>());
            services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(type)!).Resolve<TradeDbContext>() as ITradeDbContext)!);
            services.AddSingleton<EventSourceSchemaDb>();
            services.AddSingleton<LogSchemaDb>();
            services.AddSingleton<SequenceIdSchemaDb>();
            services.AddSingleton<FundSchemaDb>();
            services.AddSingleton<MarketDataSchemaDb>();
            services.AddSingleton<OptionPricerSchemaDb>();
            services.AddSingleton<ReferenceSchemaDb>();
            services.AddSingleton<SecuritiesSchemaDb>();
            services.AddSingleton<TradeSchemaDb>();
            services.AddSingleton<SystemAdminSchemaDb>();
            services.AddSingleton(_ =>
                   new StorageUrlSettings()
                        .Add("DomainData", config.GetValue<string>("AppSettings:DomainDataStorageBaseUri")!)
                        .Add("QueryData", config.GetValue<string>("AppSettings:QueryDataStorageBaseUri")!)
                   );
        }

        void RegisterServiceHandlers()
        {
            logger.LogInformationEvent("ApiServer", "register service handlers...");
            services.AddSingleton<IBoundedContextCommandResolver>(_ => new BoundedContextCommandResolver(cmdType => GetContainerInstance(cmdType)!));
        }

        void RegisterEventProducers()
        {
            logger.LogInformationEvent("ApiServer", "register event producers...");
            services.AddSingleton<ITradeEventProducer, TradeEventProducer>();
            services.AddSingleton<ITradePlacementEventProducer, TradePlacementEventProducer>();
            services.AddSingleton<IMarketDataEventProducer, MarketDataEventProducer>();
            services.AddSingleton<IStatusConsoleEventProducer, StatusConsoleEventProducer>();
        }

        void RegisterHostedServices()
        {
            logger.LogInformationEvent("ApiServer", "register hosted services...");
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
            var runtimeOptions = new DatabentoMarketDataRuntimeOptions
            {
                FeedOptions = feedOptions,
                Contracts = contracts
            };
            services.AddDatabentoMarketDataServices();
            services.AddSingleton<ITickAggregationEventPublisher,
                TickAggregationEventPublisher>();
            services.AddApplicationMarketDataApi(runtimeOptions);
            services.AddHostedService<FuturesContractRolloverStartupService>();
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
    /// <param name="logger">The <see cref="Microsoft.Extensions.Logging.ILogger"/> used to log configuration events.</param>
    static void RegisterGenericTypes(
        ConfigurationManager config,
        Microsoft.Extensions.Logging.ILogger logger)
    {
        logger.LogInformationEvent("ApiServer", "register open generic handlers...");
        _siContainer.RegisterSingleton<IDataCacheService, DataCacheService>();
        _siContainer.RegisterSingleton<IDatabaseBackupExecutionOutbox, DatabaseBackupExecutionOutbox>();
        var projectorReliabilityOptions = config
            .GetSection(EventProjectorReliabilityOptions.SectionName)
            .Get<EventProjectorReliabilityOptions>() ?? new EventProjectorReliabilityOptions();
        _siContainer.RegisterInstance(projectorReliabilityOptions.Validate());

        var domainAssemblies = new List<Assembly>
        {
            ApplicationActorAssembly.Current,
            FundActorAssembly.Current,
            MarketDataActorAssembly.Current,
            MarketDataAnalyticsActorAssembly.Current,
            MarketDataFeedActorAssembly.Current,
            OptionPricerActorAssembly.Current,
            ReferenceActorAssembly.Current,
            SecuritiesActorAssembly.Current,
            SystemAdminActorAssembly.Current,
            TradeActorAssembly.Current
        };
        var assemblies = new List<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
        assemblies.AddRange(domainAssemblies);
        var repositoryTypes = assemblies
            .Distinct()
            .SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => type is { IsClass: true, IsAbstract: false }
                && type != typeof(SystemAdminDbContext)
                && type.GetInterfaces().Any(static contract => contract.IsGenericType
                    && contract.GetGenericTypeDefinition() == typeof(IObjectRepository<>)))
            .Distinct()
            .ToArray();
        _siContainer.Register(typeof(IObjectRepository<>), repositoryTypes, Lifestyle.Transient);
        var systemAdminRegistration = Lifestyle.Singleton.CreateRegistration<SystemAdminDbContext>(_siContainer);
        _siContainer.AddRegistration<ISystemAdminDbContext>(systemAdminRegistration);
        _siContainer.AddRegistration<IObjectRepository<SystemAdminDbContext>>(systemAdminRegistration);
        _siContainer.Register(typeof(IActor<>), assemblies, Lifestyle.Singleton);
        _siContainer.Register(typeof(IActorStateDenormalizer<>), assemblies, Lifestyle.Singleton);
        _siContainer.Register(typeof(IEventSourceActorStateRepository<>), assemblies, Lifestyle.Singleton);
        _siContainer.Register(typeof(IEventProjector<>), domainAssemblies, Lifestyle.Singleton);
        _siContainer.Register(
            typeof(TomasAI.IFM.Application.EventProjector.Realtime.Contracts.IRealtimeProjector<>),
            domainAssemblies,
            Lifestyle.Singleton);
        _siContainer.Register(typeof(IEventSourceActorState<>), assemblies, Lifestyle.Transient);
        logger.LogInformationEvent("ApiServer", "open generic handlers registered");
    }

    /// <summary>Configures middleware and verifies the completed dependency-injection container.</summary>
    public static WebApplication ConfigureRequestPipeline(this WebApplication app, Microsoft.Extensions.Logging.ILogger logger)
    {
        // configure the HTTP request pipeline...
        app.Services.UseSimpleInjector(_siContainer);
        _siContainer.Verify();
        logger.LogInformationEvent("ApiServer", "configure HTTP request pipeline...");
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
                options.RoutePrefix = string.Empty;
            });
        }
        else
        {
            app.UseHttpsRedirection();
        }
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
            ResponseWriter = static async (context, report) =>
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
        });
        logger.LogInformationEvent("ApiServer", "web app configuration completed");
        return app;
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
    /// <param name="commandType">The <see cref="Type"/> of the object to retrieve from the container.</param>
    /// <returns>An instance of the specified type if it exists in the container; otherwise, <see langword="null"/>.</returns>
    static object? GetContainerInstance(Type commandType)
    {
        object? commandInstance;
        try
        {
            commandInstance = _siContainer.GetInstance(commandType);
        }
        catch
        {
            commandInstance = null;
        }
        return commandInstance;
    }

}
