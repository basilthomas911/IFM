using System.Reflection;
using SimpleInjector;
using Serilog;
using Serilog.Events;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Storage;

using TomasAI.IFM.Domain.Application;
using TomasAI.IFM.Domain.MarketData.Analytics;
using TomasAI.IFM.Domain.MarketData.Feed;
using TomasAI.IFM.Domain.OptionPricer;
using TomasAI.IFM.Domain.SystemAdmin;
using TomasAI.IFM.Domain.Trade;
using TomasAI.IFM.Domain.Telemetry;
using TomasAI.IFM.Domain.PredictiveModel;
using TomasAI.IFM.Framework.Telemetry.Logging.Serilog;
using System.Text.Json.Serialization;
using SimpleInjector.Lifestyles;
using StackExchange.Redis;

using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.PredictiveModel.Client;
using TomasAI.IFM.Application.Query.Client;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.Postgres.LogDb;
using TomasAI.IFM.Application.Storage.Postgres.SequenceIdDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.OptionPricerDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.ReferenceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.SecuritiesDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;
using TomasAI.IFM.Domain.Trade.Option.Algorithm.Model;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Shared.Application.ServiceApi;
using TomasAI.IFM.Shared.EventProducers;
using TomasAI.IFM.Shared.Fund.ServiceApi;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.Telemetry.ServiceApi;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.TradePlan.ServiceApi;
using TomasAI.IFM.Shared.Log.Model;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ServiceApi;
using TomasAI.IFM.Shared.TradeAlgorithm.ServiceApi;
using TomasAI.IFM.Service.SystemAdmin.Backup;
using TomasAI.IFM.Service.SystemAdmin.HostedService;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Storage.Azure;
using TomasAI.IFM.Framework.Caching.Redis;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Framework.Messaging.Kafka;

namespace TomasAI.IFM.Application.Command.Server;

public static class Startup
{
    static Container _container = new();

    public static WebApplicationBuilder ConfigureWebApp(this WebApplicationBuilder builder, out Microsoft.Extensions.Logging.ILogger logger)
    {
        _container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        _ = builder.WebHost
                       .ConfigureAppConfiguration((ctx, configBuilder) => {
                           configBuilder.SetBasePath(Directory.GetCurrentDirectory())
                               .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                               .AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);

                           var config = configBuilder.Build();
                           var telemetryServerBaseUri = config.GetValue<string>("AppSettings:TelemetryServerBaseUri")!;

                           Log.Logger = new LoggerConfiguration()
                               .MinimumLevel.Information()
                               .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                               .MinimumLevel.Override("System", LogEventLevel.Error)
                               .Enrich.FromLogContext()
                               .WriteTo.Console()
                               .WriteTo.Http(requestUri: telemetryServerBaseUri, httpClient: new SerilogHttpClient(), queueLimitBytes: 10000)
                               .CreateLogger();
                       })
                       .UseKestrel()
                       .UseSerilog();

        // configure web app...
        var serviceProvider = builder.Services.BuildServiceProvider();
        logger = serviceProvider.GetRequiredService<ILogger<Program>>() as Microsoft.Extensions.Logging.ILogger;
        builder.Services.AddSingleton<Microsoft.Extensions.Logging.ILogger>(logger);

        logger.LogInformationEvent("CommandServer", "configure web app...");
        builder.Services.AddControllers()
            .AddNewtonsoftJson()
            .AddJsonOptions(options => {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddSimpleInjector(_container);
        return builder;
    }

    public static IServiceCollection RegisterServices(this IServiceCollection services, ConfigurationManager config, Microsoft.Extensions.Logging.ILogger logger)
    {
        logger.LogInformationEvent("CommandServer", "add web app services...");
        RegisterBaseServices();
        RegisterQueryApiServices();
        RegisterStorageServices();
        RegisterServiceHandlers();
        RegisterEventProducers();
        RegisterHostedServices();
        return services;

        void RegisterBaseServices()
        {
            // add web app services...
            logger.LogInformationEvent("CommandServer", "register base services...");
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            var redisUri = config.GetValue<string>("AppSettings:RedisUri")!;
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisUri));
            services.AddSingleton<IRedisCache, RedisCache>();
            services.AddSingleton<IBlackboardService, BlackboardService>();
            services.AddSingleton<IJsonSerializer, NewtonSoftJsonSerializer>();
            services.AddSingleton<IEventConsumerOptions>(new KafkaEventConsumerOptions(null!, config.GetValue<string>("AppSettings:EventProducer:BootstrapServers")!, true));
            services.AddSingleton<IEventProducerOptions>(new KafkaEventProducerOptions(config.GetValue<string>("AppSettings:EventProducer:BootstrapServers")!));
            services.AddSingleton<IBoundedContextFactoryResolver, BoundedContextFactoryResolver>(_ => new BoundedContextFactoryResolver(e => GetCommandInstance(e)!));
            services.AddSingleton<IBoundedContextFactory, BoundedContextFactory>();
            services.AddSingleton<IAlgorithmBuilder, AlgorithmBuilder>();
            services.AddSingleton<IExceptionDecoratorFactory>(_ => new ExceptionDecoratorFactory(e => GetCommandInstance(e)!));
            services.AddSingleton<IValidationDecoratorFactory>(_ => new ValidationDecoratorFactory(e => GetCommandInstance(e)!));
        }

         void RegisterQueryApiServices()
        {
            logger.LogInformationEvent("CommandServer", "register query API services...");
            services.AddSingleton<IQueryServiceRestApiOptions>(_ => new QueryServiceRestApiOptions(config.GetValue<string>("AppSettings:QueryServerBaseUri")!));
            services.AddSingleton<IQueryService, QueryServiceRestApiClient>();
            services.AddSingleton<IMarketDataAnalyticsQueryApi, MarketDataAnalyticsQueryApi>();
            services.AddSingleton<IMarketDataFeedQueryApi, MarketDataFeedQueryApi>();
            services.AddSingleton<IMarketDataQueryApi, MarketDataQueryApi>();
            services.AddSingleton<IOptionPricerQueryApi, OptionPricerQueryApi>();
            services.AddSingleton<ITradeQueryApi, TradeQueryApi>();
            services.AddSingleton<ITradePlanQueryApi, TradePlanQueryApi>();
            services.AddSingleton<IFundQueryApi, FundQueryApi>();
        }

        void RegisterStorageServices()
        {
            logger.LogInformationEvent("CommandServer", "register storage services...");
            services.AddSingleton(_ =>
            new DbConnectionSettings()
                .Add("EventSourceDbConnection", config.GetConnectionString("EventSourceDbConnection")!, "System.Data.Postgres")
                .Add("LogDbConnection", config.GetConnectionString("LogDbConnection")!, "System.Data.Postgres")
                .Add("SequenceIdDbConnection", config.GetConnectionString("SequenceIdDbConnection")!, "System.Data.Postgres")
                .Add("FundDbConnection", config.GetConnectionString("FundDbConnection")!, "System.Data.ScyllaDb")
                .Add("MarketDataDbConnection", config.GetConnectionString("MarketDataDbConnection")!, "System.Data.ScyllaDb")
                .Add("OptionPricerDbConnection", config.GetConnectionString("OptionPricerDbConnection")!, "System.Data.ScyllaDb")
                .Add("ReferenceDbConnection", config.GetConnectionString("ReferenceDbConnection")!, "System.Data.ScyllaDb")
                .Add("SecuritiesDbConnection", config.GetConnectionString("SecuritiesDbConnection")!, "System.Data.ScyllaDb")
                .Add("TradeDbConnection", config.GetConnectionString("TradeDbConnection")!, "System.Data.ScyllaDb")
                .Add("YieldCurveRatesDbConnection", config.GetConnectionString("YieldCurveRatesDbConnection")!, "TomasAI.IFM.Storage"));
             services.AddSingleton<IDbCache, DbCache>();
             services.AddSingleton<IDbContextResolver>(_ => new DbContextResolver(e => GetCommandInstance(e)!));
             services.AddSingleton<IDbContextFactory, DbContextFactory>();
            services.AddSingleton<ISequenceIdDbContext, SequenceIdDbContext>();
            services.AddSingleton<ISequenceIdGenerator, PostgresSequenceIdGenerator>();
            services.AddSingleton(_ => (new DbContextResolver(_ => GetCommandInstance(typeof(EventSourceDbContext))!)?.Resolve<EventSourceDbContext>() as IEventSourceDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(_ => GetCommandInstance(typeof(LogDbContext))!)?.Resolve<LogDbContext>() as ILogDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(_ => GetCommandInstance(typeof(SequenceIdDbContext))!)?.Resolve<SequenceIdDbContext>() as ISequenceIdDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(_ => GetCommandInstance(typeof(FundDbContext))!)?.Resolve<FundDbContext>() as IFundDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(_ => GetCommandInstance(typeof(MarketDataDbContext))!)?.Resolve<MarketDataDbContext>() as IMarketDataDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(_ => GetCommandInstance(typeof(OptionPricerDbContext))!)?.Resolve<OptionPricerDbContext>() as IOptionPricerDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(_ => GetCommandInstance(typeof(ReferenceDbContext))!)?.Resolve<ReferenceDbContext>() as IReferenceDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(_ => GetCommandInstance(typeof(SecuritiesDbContext))!)?.Resolve<SecuritiesDbContext>() as ISecuritiesDbContext)!);
            services.AddSingleton(_ => (new DbContextResolver(_ => GetCommandInstance(typeof(TradeDbContext))!)?.Resolve<TradeDbContext>() as ITradeDbContext)!);
            services.AddSingleton(_ =>
                   new StorageUrlSettings()
                        .Add("DomainData", config.GetValue<string>("AppSettings:DomainDataStorageBaseUri")!)
                        .Add("QueryData", config.GetValue<string>("AppSettings:QueryDataStorageBaseUri")!)
                   );
           }

        void RegisterServiceHandlers()
        {
            logger.LogInformationEvent("CommandServer", "register service handlers...");
            services.AddSingleton<IBoundedContextCommandResolver>(_ => new BoundedContextCommandResolver(cmdType => GetCommandInstance(cmdType)!));
            services.AddSingleton<ICommandHandlerResolver>(_ => new CommandHandlerResolver(cmdType => GetCommandInstance(cmdType)!));
            services.AddSingleton<ICommandService, CommandService>();
            services.AddSingleton<IQueryServiceRestApiOptions>(_ => new QueryServiceRestApiOptions(config.GetValue<string>("AppSettings:QueryServerBaseUri")!));
            services.AddSingleton<IPredictiveModelQueryServiceRestApiOptions>(_ => new PredictiveModelQueryServiceRestApiOptions(config.GetValue<string>("AppSettings:PredictiveModelServerBaseUri")!));
            services.AddSingleton<IQueryService, QueryServiceRestApiClient>();
            services.AddSingleton<IPredictiveModelQueryService>(_ => new PredictiveModelQueryServiceRestClientApi(
                    (GetCommandInstance(typeof(IPredictiveModelQueryServiceRestApiOptions)) as IPredictiveModelQueryServiceRestApiOptions)!,
                    (GetCommandInstance(typeof(IJsonSerializer)) as IJsonSerializer)!));
            services.AddSingleton<IReferenceQueryApi, ReferenceQueryApi>();
            services.AddSingleton<IMarketDataFeedQueryApi, MarketDataFeedQueryApi>();
            services.AddSingleton<ISystemAdminQueryApi, SystemAdminQueryApi>();
            services.AddSingleton<IFuturesItiTrendQueryApi, FuturesItiTrendQueryApi>();
            services.AddSingleton<IMarketDataAnalyticsQueryApi, MarketDataAnalyticsQueryApi>();
        }

        void RegisterEventProducers()
        {
            logger.LogInformationEvent("CommandServer", "register event producers...");
            services.AddSingleton<IEventProducerOptions>(new KafkaEventProducerOptions(config.GetValue<string>("AppSettings:EventProducer:BootstrapServers")!));
            services.AddSingleton<IExceptionEventProducer, ExceptionEventProducer>();
            services.AddSingleton<ITradeEventProducer, TradeEventProducer>();
            services.AddSingleton<ITradeOrderEventProducer, TradeOrderEventProducer>();
            services.AddSingleton<ITradePlacementEventProducer, TradePlacementEventProducer>();
            services.AddSingleton<IOptionPricerEventProducer, OptionPricerEventProducer>();
            services.AddSingleton<IFundEventProducer, FundEventProducer>();
            services.AddSingleton<IMarketDataEventProducer, MarketDataEventProducer>();
            services.AddSingleton<IMarketDataFeedEventProducer, MarketDataFeedEventProducer>();
            services.AddSingleton<IMarketDataAnalyticsEventProducer, MarketDataAnalyticsEventProducer>();
            services.AddSingleton<IReferenceEventProducer, ReferenceEventProducer>();
            services.AddSingleton<ISystemAdminEventProducer, SystemAdminEventProducer>();
            services.AddSingleton<IApplicationEventProducer, ApplicationEventProducer>();
            services.AddSingleton<IStatusConsoleEventProducer, StatusConsoleEventProducer>();
            services.AddSingleton<ITelemetryEventProducer, TelemetryEventProducer>();
            services.AddSingleton<IFuturesItiTrendEventProducer, FuturesItiTrendEventProducer>();
            services.AddSingleton<ITradeAlgorithmEventProducer, TradeAlgorithmEventProducer>();
        }

        void RegisterHostedServices()
        {
            logger.LogInformationEvent("CommandServer", "register hosted services...");
            services.AddSingleton<IStatusConsoleWriter, StatusConsoleWriter>();
            services.AddSingleton<ISystemAdminEventProducer, SystemAdminEventProducer>();
            services.AddSingleton<IAzureStorageOptions>(sp => config.GetSection("AzureStorage").Get<AzureStorageOptions>()!);
            services.AddSingleton<IAzureStorage, AzureStorage>();
            services.AddSingleton<IDatabaseBackupService, DomainDatabaseBackupService>();
            services.AddSingleton<IDatabaseBackupEventConsumer, DatabaseBackupEventConsumer>();
            services.AddHostedService<SystemAdminHostedService>();
        }
    }

    public static WebApplication ConfigureRequestPipeline(this WebApplication app, Microsoft.Extensions.Logging.ILogger logger)
    {
        // configure the HTTP request pipeline...
        InitContainer();
        logger.LogInformationEvent("CommandServer", "configure HTTP request pipeline...");
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
        app.MapControllers();
        logger.LogInformationEvent("CommandServer", "web app configuration completed");
        return app;

        void InitContainer()
        {
            // register open generic handlers...
            logger.LogInformationEvent("CommandServer", "register open generic handlers...");
            _container.RegisterSingleton<IDataCacheService, DataCacheService>();

            // register all open generics...
            var domainAssemblies = new List<Assembly>();
            domainAssemblies.AddRange([
                ApplicationDomainAssembly.Current,
                MarketDataAnalyticsDomainAssembly.Current,
                MarketDataFeedDomainAssembly.Current,
                OptionPricerDomainAssembly.Current,
                SystemAdminDomainAssembly.Current,
                TelemetryDomainAssembly.Current,
                TradeDomainAssembly.Current,
                PredictiveModelDomainAssembly.Current
            ]);
            var assemblies = new List<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
            assemblies.AddRange(domainAssemblies);
            _container.Register(typeof(IBoundedContext<>), assemblies, Lifestyle.Transient);
            _container.Register(typeof(IBoundedContextState<>), assemblies, Lifestyle.Transient);
            _container.Register(typeof(IObjectRepository<>), assemblies, Lifestyle.Transient);
            //_container.Register(typeof(IAggregateCommandHandler<>), assemblies, Lifestyle.Singleton);
            _container.Register(typeof(IBoundedContextCommandHandler<,>), assemblies, Lifestyle.Singleton);
            _container.Register(typeof(ICommandContext<>), assemblies, Lifestyle.Singleton);
            _container.Register(typeof(IEventRepository<>), assemblies, Lifestyle.Singleton);
            _container.Register(typeof(IEventDenormalizer<>), assemblies, Lifestyle.Singleton);
            _container.Register(typeof(IValidationRules<>), assemblies, Lifestyle.Singleton);
            _container.Register(typeof(IValidationCommandDecorator<>), assemblies, Lifestyle.Singleton);
            _container.Register(typeof(IExceptionCommandDecorator<>), assemblies, Lifestyle.Singleton);
            _container.Register(typeof(IAsyncEventHandler<>), assemblies, Lifestyle.Singleton);
            _container.RegisterDecorator(typeof(ICommandContext<>), typeof(ValidationCommandDecorator<>), Lifestyle.Singleton);
            _container.RegisterDecorator(typeof(ICommandContext<>), typeof(CommandLoggerDecorator<>), Lifestyle.Singleton);
            _container.RegisterDecorator(typeof(ICommandContext<>), typeof(ExceptionCommandDecorator<>), Lifestyle.Singleton);

            // register all bounded context state types to bounded context state type map...

            // allow Simple Injector to resolve services from ASP.NET Core...
            app.Services.UseSimpleInjector(_container);
            _container.Verify();
            logger.LogInformationEvent("CommandServer", "query generic handlers registered");
            return;

            IEnumerable<Type> GetAllTypesImplementingOpenGenericType(Type openGenericType, List<Assembly> assemblies)
            {
                foreach (var assembly in assemblies)
                {
                    var getOpenGenricQuery = from x in assembly.GetTypes()
                                             from z in x.GetInterfaces()
                                             let y = x.BaseType
                                             where (y != null && y.IsGenericType
                                                  && openGenericType.IsAssignableFrom(y.GetGenericTypeDefinition())) ||
                                                  (z.IsGenericType && openGenericType.IsAssignableFrom(z.GetGenericTypeDefinition()))
                                             select x;
                    foreach (var openGenricType in getOpenGenricQuery)
                        yield return openGenricType;
                }
            }

        }
    }

    static object? GetCommandInstance(Type commandType)
    {
        var commandInstance = default(object);
        try
        {
            commandInstance = _container.GetInstance(commandType);
        }
        catch
        {
            commandInstance = null;
        }
        return commandInstance;
    }

}
