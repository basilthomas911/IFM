using Hazelcast;
using Hazelcast.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Serilog;
using Serilog.Events;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using StackExchange.Redis;
using System.Buffers;
using System.Reflection;
using System.Text.Json.Serialization;
using TomasAI.IFM.Application.Actor;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Command;
using TomasAI.IFM.Application.Query;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EconomicCalendarsDb;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.Postgres.LogDb;
using TomasAI.IFM.Application.Storage.Postgres.SequenceIdDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.OptionPricerDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.ReferenceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.SecuritiesDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;
using TomasAI.IFM.Application.Storage.YieldCurveRatesDb;
using TomasAI.IFM.Domain.Fund;
using TomasAI.IFM.Domain.MarketData;
using TomasAI.IFM.Domain.MarketData.Analytics;
using TomasAI.IFM.Domain.MarketData.Feed;
using TomasAI.IFM.Domain.MarketData.Securities;
using TomasAI.IFM.Domain.Reference;
using TomasAI.IFM.Domain.Reference.Services;
using TomasAI.IFM.Domain.SystemAdmin.Actor;
using TomasAI.IFM.Domain.Trade.Actor;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Caching.Redis;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.Nats.Contracts;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Azure;
using TomasAI.IFM.Service.MarketDataFeed.InteractiveBrokers;
using TomasAI.IFM.Service.TradePlan;
using TomasAI.IFM.Service.TradePlan.HostedService;
using TomasAI.IFM.Service.TradePosition;
using TomasAI.IFM.Service.TradePosition.HostedService;
using TomasAI.IFM.Shared.Application.ServiceApi;
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
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Contracts;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.TradePlan.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;

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
                               .AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);

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
                       .UseKestrel()
                       .UseSerilog();

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
        RegisterQueryApiServices();
        RegisterStorageServices();
        RegisterServiceHandlers();
        RegisterEventProducers();
        RegisterHostedServices();
        return services;

        void RegisterBaseServices()
        {
            // add web app services...
            logger.LogInformationEvent("ApiServer", "register base services...");
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
            services.AddSingleton<IEventConsumerOptions>(new KafkaEventConsumerOptions(null!, config.GetValue<string>("AppSettings:EventProducer:BootstrapServers")!, true));
            services.AddSingleton<IEventProducerOptions>(new KafkaEventProducerOptions(config.GetValue<string>("AppSettings:EventProducer:BootstrapServers")!));
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
            services.AddSingleton<IActorSupervisor, ActorSupervisor>();
            services.AddSingleton<IActorService, ActorService>();
            services.AddSingleton<IActorRegistry>(_ => {
                var actorTypes = (
                    from reg in _siContainer.GetCurrentRegistrations()
                    where reg.ServiceType.IsClosedTypeOf(typeof(IActor<>))
                    select reg.Registration.ImplementationType)
                    .Distinct()
                    .ToArray();
                return new ActorRegistry(actorTypes);
            });
            services.AddSingleton<IActorFactory>( _ => new ActorFactory(actorType => GetContainerInstance(actorType)!));
            services.AddSingleton<INatsProducerOptions, NatsProducerOptions>();
            services.AddSingleton<INatsConsumerOptions, NatsConsumerOptions>();
            services.AddTransient<IActorProducer, NatsActorProducer>();
            services.AddTransient<IActorConsumer, NatsActorConsumer>();
            services.AddSingleton<INatsJetStreamProducerOptions, NatsJetStreamProducerOptions>();
            services.AddSingleton<INatsJetStreamConsumerOptions, NatsJetStreamConsumerOptions>();
            services.AddTransient<IJSActorProducer, NatsJetStreamActorProducer>();
            services.AddTransient<IJSActorConsumer, NatsJetStreamActorConsumer>();
            services.AddSingleton<IContainerInstance>(provider => new ContainerInstance(type => {
                var instance = provider.GetService(type)!;
                instance ??= GetContainerInstance(type)!;
                return instance;
            }));
            services.AddTransient<IActorThreadQueue, ActorThreadQueue>();
            //services.AddTransient<IActorThreadQueue, NatsActorThreadQueue>();
            //services.AddTransient<IActorSpscRingBuffer<NatsMsg<byte[]>>>(_ => new NatsActorSpscRingBuffer(8192,64,64));


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
        }

        void RegisterQueryApiServices()
        {
            logger.LogInformationEvent("ApiServer", "register query API services...");
            services.AddSingleton<IQueryServiceApiOptions>(_ => new QueryServiceApiOptions(config.GetValue<string>("AppSettings:QueryServerBaseUri")!));
            services.AddSingleton<IQueryServiceApi, QueryServiceApiClient>();
            services.AddSingleton<IFundQueryApi, FundQueryApi>();
            services.AddSingleton<IMarketDataAnalyticsQueryApi, MarketDataAnalyticsQueryApi>();
            services.AddSingleton<IMarketDataFeedQueryApi, MarketDataFeedQueryApi>();
            services.AddSingleton<IMarketDataQueryApi, MarketDataQueryApi>();
            services.AddSingleton<IOptionPricerQueryApi, OptionPricerQueryApi>();
            services.AddSingleton<ITradePlanQueryApi, TradePlanQueryApi>();
            services.AddSingleton<ITradeQueryApi, OptionTradeQueryApi>();
            services.AddSingleton<IReferenceQueryApi, ReferenceQueryApi>();
        }

        void RegisterStorageServices()
        {
            logger.LogInformationEvent("ApiServer", "register storage services...");
            services.AddSingleton(_ =>
            new DbConnectionSettings()
                .Add("EventSourceDbConnection", config.GetConnectionString("EventSourceDbConnection")!, "System.Data.Postgres")
                .Add("EventSourceActorDbConnection", config.GetConnectionString("EventSourceActorDbConnection")!, "System.Data.Postgres")
                .Add("LogDbConnection", config.GetConnectionString("LogDbConnection")!, "System.Data.Postgres")
                .Add("SequenceIdDbConnection", config.GetConnectionString("SequenceIdDbConnection")!, "System.Data.Postgres")
                .Add("FundDbConnection", config.GetConnectionString("FundDbConnection")!, "System.Data.ScyllaDb")
                .Add("MarketDataDbConnection", config.GetConnectionString("MarketDataDbConnection")!, "System.Data.ScyllaDb")
                .Add("OptionPricerDbConnection", config.GetConnectionString("OptionPricerDbConnection")!, "System.Data.ScyllaDb")
                .Add("ReferenceDbConnection", config.GetConnectionString("ReferenceDbConnection")!, "System.Data.ScyllaDb")
                .Add("SecuritiesDbConnection", config.GetConnectionString("SecuritiesDbConnection")!, "System.Data.ScyllaDb")
                .Add("TradeDbConnection", config.GetConnectionString("TradeDbConnection")!, "System.Data.ScyllaDb")
                .Add("YieldCurveRatesDbConnection", config.GetConnectionString("YieldCurveRatesDbConnection")!, "TomasAI.IFM.Storage")
                .Add("EconomicCalendarsDbConnection", config.GetConnectionString("EconomicCalendarsDbConnection")!, "TomasAI.IFM.Storage")
            );
            services.AddSingleton<IDbCache, DbCache>();
            services.AddSingleton<IDbContextResolver>(_ => new DbContextResolver(e => GetContainerInstance(e)!));
            services.AddSingleton<IDbContextFactory, DbContextFactory>();
            services.AddSingleton<ISequenceIdDbContext, SequenceIdDbContext>();
            services.AddSingleton<ISequenceIdGenerator, PostgresSequenceIdGenerator>();
            services.AddSingleton(_ => (new DbContextResolver(_ => GetContainerInstance(typeof(EventSourceDbContext))!)?.Resolve<EventSourceDbContext>() as IEventSourceDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(_ => GetContainerInstance(typeof(EventSourceActorDbContext))!)?.Resolve<EventSourceActorDbContext>() as IEventSourceActorDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(_ => GetContainerInstance(typeof(LogDbContext))!)?.Resolve<LogDbContext>() as ILogDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(_ => GetContainerInstance(typeof(SequenceIdDbContext))!)?.Resolve<SequenceIdDbContext>() as ISequenceIdDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(_ => GetContainerInstance(typeof(FundDbContext))!)?.Resolve<FundDbContext>() as IFundDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(_ => GetContainerInstance(typeof(MarketDataDbContext))!)?.Resolve<MarketDataDbContext>() as IMarketDataDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(_ => GetContainerInstance(typeof(OptionPricerDbContext))!)?.Resolve<OptionPricerDbContext>() as IOptionPricerDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(_ => GetContainerInstance(typeof(ReferenceDbContext))!)?.Resolve<ReferenceDbContext>() as IReferenceDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(_ => GetContainerInstance(typeof(SecuritiesDbContext))!)?.Resolve<SecuritiesDbContext>() as ISecuritiesDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(_ => GetContainerInstance(typeof(TradeDbContext))!)?.Resolve<TradeDbContext>() as ITradeDbContext)!);
            services.AddSingleton<IYieldCurveRatesDbContext, YieldCurveRatesDbContext>();
            services.AddSingleton<IEconomicCalendarsDbContext, EconomicCalendarsDbContext>();
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
            services.AddSingleton<ICommandHandlerResolver>(_ => new CommandHandlerResolver(cmdType => GetContainerInstance(cmdType)!));
            services.AddSingleton<Command.ICommandService, CommandService>();
            services.AddSingleton<IQueryHandlerResolver>(_ => new QueryHandlerResolver(handlerType => GetContainerInstance(handlerType)!));
            services.AddSingleton<Query.IQueryService, QueryService>();
        }

        void RegisterEventProducers()
        {
            logger.LogInformationEvent("ApiServer", "register event producers...");
            services.AddSingleton<IEventProducerOptions>(new KafkaEventProducerOptions(config.GetValue<string>("AppSettings:EventProducer:BootstrapServers")!));
            services.AddSingleton<ITradeEventProducer, TradeEventProducer>();
            services.AddSingleton<ITradePlacementEventProducer, TradePlacementEventProducer>();
            services.AddSingleton<IMarketDataEventProducer, MarketDataEventProducer>();
            services.AddSingleton<IMarketDataFeedEventProducer, MarketDataFeedEventProducer>();
            services.AddSingleton<IStatusConsoleEventProducer, StatusConsoleEventProducer>();
        }

        void RegisterHostedServices()
        {
            logger.LogInformationEvent("ApiServer", "register hosted services...");
            services.AddSingleton<IStatusConsoleWriter, StatusConsoleWriter>();
            services.AddSingleton<IAzureStorageOptions>(sp => config.GetSection("AzureStorage").Get<AzureStorageOptions>()!);
            services.AddSingleton<IAzureStorage, AzureStorage>();
            services.AddSingleton<IEventConsumerOptions>(new KafkaEventConsumerOptions(null!, config.GetValue<string>("AppSettings:EventProducer:BootstrapServers")!, true));

            // market data feed hosted service...
            //services.AddSingleton<IMarketDataFeedEventService, MarketDataFeedEventService>();
            services.AddSingleton<IMarketDataApiOptions>(sp => new IBMarketDataApiOptions(
                host: config.GetValue<string>("AppSettings:MarketDataFeedApi:Host")!,
                port: config.GetValue<int>("AppSettings:MarketDataFeedApi:Port"),
                clientId: config.GetValue<int>("AppSettings:MarketDataFeedApi:ClientId")));
            services.AddSingleton<IMarketDataApi, IBMarketDataApi>();

            services.AddSingleton<IMarketDataSnapshotApiOptions>(sp => new IBMarketDataSnapshotApiOptions(
                 host: config.GetValue<string>("AppSettings:MarketDataFeedSnapshotApi:Host")!,
                 port: config.GetValue<int>("AppSettings:MarketDataFeedSnapshotApi:Port"),
                clientId: config.GetValue<int>("AppSettings:MarketDataFeedSnapshotApi:ClientId")));
            services.AddSingleton<IMarketDataSnapshotApi, IBMarketDataSnapshotApi>();

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
    /// Configures the HTTP request pipeline for the specified <see cref="WebApplication"/> instance.
    /// </summary>
    /// <remarks>This method sets up middleware and services for the application, including Swagger for
    /// development environments, HTTPS redirection for non-development environments, and authorization middleware. It
    /// also registers open generic types and integrates Simple Injector for dependency injection.</remarks>
    /// <param name="app">The <see cref="WebApplication"/> instance to configure.</param>
    /// <param name="logger">The <see cref="Microsoft.Extensions.Logging.ILogger"/> used to log configuration events.</param>
    /// <returns>The configured <see cref="WebApplication"/> instance.</returns>
    public static WebApplication ConfigureRequestPipeline(this WebApplication app, Microsoft.Extensions.Logging.ILogger logger)
    {
        // configure the HTTP request pipeline...
        RegisterGenericTypes();
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
        app.UseAuthorization();
        logger.LogInformationEvent("ApiServer", "web app configuration completed");
        return app;

        void RegisterGenericTypes()
        {
            // register open generic handlers...
            logger.LogInformationEvent("ApiServer", "register open generic handlers...");
            _siContainer.RegisterSingleton<IDataCacheService, DataCacheService>();

            // register all open generics...
            var domainAssemblies = new List<Assembly>();
            domainAssemblies.AddRange([
                ApplicationActorAssembly.Current,
                FundActorAssembly.Current,
                MarketDataActorAssembly.Current,
                MarketDataAnalyticsActorAssembly.Current,
                MarketDataFeedActorAssembly.Current,
                ReferenceActorAssembly.Current,
                SecuritiesActorAssembly.Current,
                SystemAdminActorAssembly.Current,
                TradeActorAssembly.Current,
            ]);
            var assemblies = new List<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
            assemblies.AddRange(domainAssemblies);
            /*
            _siContainer.Register(typeof(IBoundedContext<>), assemblies, Lifestyle.Transient);
            _siContainer.Register(typeof(IBoundedContextState<>), assemblies, Lifestyle.Transient);
            _siContainer.Register(typeof(IObjectRepository<>), assemblies, Lifestyle.Transient);
            _siContainer.Register(typeof(IBoundedContextCommandHandler<,>), assemblies, Lifestyle.Singleton);
            _siContainer.Register(typeof(ICommandContext<>), assemblies, Lifestyle.Singleton);
            _siContainer.Register(typeof(IEventRepository<>), assemblies, Lifestyle.Singleton);
            _siContainer.Register(typeof(IEventDenormalizer<>), assemblies, Lifestyle.Singleton);
            _siContainer.Register(typeof(IValidationRules<>), assemblies, Lifestyle.Singleton);
            _siContainer.Register(typeof(IValidationCommandDecorator<>), assemblies, Lifestyle.Singleton);
            _siContainer.Register(typeof(IExceptionCommandDecorator<>), assemblies, Lifestyle.Singleton);
            _siContainer.Register(typeof(IAsyncEventHandler<>), assemblies, Lifestyle.Singleton);
            _siContainer.Register(typeof(IAsyncEventHandler<,>), assemblies, Lifestyle.Singleton);
            _siContainer.Register(typeof(IAsyncEventServiceHandler<,>), assemblies, Lifestyle.Singleton);
            _siContainer.Register(typeof(IAsyncQueryHandler<,>), assemblies, Lifestyle.Singleton);
            _siContainer.Register(typeof(IQueryHandler<,>), assemblies, Lifestyle.Singleton);
            _siContainer.Register(typeof(IQueryState<>), assemblies, Lifestyle.Singleton);*/
            _siContainer.Register(typeof(IObjectRepository<>), assemblies, Lifestyle.Transient);
            _siContainer.Register(typeof(IActor<>), assemblies, Lifestyle.Singleton);
            _siContainer.Register(typeof(IActorStateDenormalizer<>), assemblies, Lifestyle.Singleton);
            _siContainer.Register(typeof(IEventSourceActorStateRepository<>), assemblies, Lifestyle.Singleton);
            _siContainer.Register(typeof(IEventSourceActorState<>), assemblies, Lifestyle.Transient);

            //_siContainer.RegisterDecorator(typeof(ICommandContext<>), typeof(ValidationCommandDecorator<>), Lifestyle.Singleton);
            //_siContainer.RegisterDecorator(typeof(ICommandContext<>), typeof(CommandLoggerDecorator<>), Lifestyle.Singleton);
            //_siContainer.RegisterDecorator(typeof(ICommandContext<>), typeof(ExceptionCommandDecorator<>), Lifestyle.Singleton);

            // allow Simple Injector to resolve services from ASP.NET Core...
            app.Services.UseSimpleInjector(_siContainer);
            _siContainer.Verify();
            logger.LogInformationEvent("ApiServer", "open generic handlers registered");
            return;


        }
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
