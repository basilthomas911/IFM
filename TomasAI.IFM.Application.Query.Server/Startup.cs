using Serilog;
using Serilog.Events;
using SimpleInjector;
using StackExchange.Redis;
using System.Reflection;
using System.Text.Json.Serialization;
using TomasAI.IFM.Application.Blackboard;
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
using TomasAI.IFM.Domain.MarketData;
using TomasAI.IFM.Domain.MarketData.Analytics;
using TomasAI.IFM.Domain.MarketData.Feed;
using TomasAI.IFM.Domain.OptionPricer;
using TomasAI.IFM.Domain.SystemAdmin;
using TomasAI.IFM.Domain.Trade;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Caching.Redis;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Azure;
using TomasAI.IFM.Framework.Telemetry.Logging.Serilog;
using TomasAI.IFM.Service.MarketDataFeed.InteractiveBrokers;
using TomasAI.IFM.Service.SystemAdmin.Backup;
using TomasAI.IFM.Service.SystemAdmin.HostedService;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.EventProducers;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Fund.ServiceApi;
using TomasAI.IFM.Shared.Log.Model;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Shared.Trade.ServiceApi;

namespace TomasAI.IFM.Application.Query.Server;

public static class Startup
{
    static Container _container = new();

    public static WebApplicationBuilder ConfigureWebApp(this WebApplicationBuilder builder, out Microsoft.Extensions.Logging.ILogger logger)
    {
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

        logger.LogInformationEvent("QueryServer", "configure web app...");
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
        // add web app services...
        logger.LogInformationEvent("QueryServer", "add web app services...");

        RegisterBaseServices();
        RegisterStorageServices();
        RegisterQueryHandlers();
        RegisterEventConsumers();
        RegisterEventProducers();
        RegisterHostedServices();
        return services;

        void RegisterBaseServices()
        {
            logger.LogInformationEvent("QueryServer", "register base services...");
            services.AddSingleton<IDbCache, DbCache>();
            services.AddSingleton<IDataCacheService, LocalDataCacheService>();
            services.AddSingleton<IMarketDataSnapshotApiOptions>(sp => new IBMarketDataSnapshotApiOptions(
                host: config.GetValue<string>("AppSettings:MarketDataFeedSnapshotApi:Host")!,
                port: config.GetValue<int>("AppSettings:MarketDataFeedSnapshotApi:Port"),
                clientId: config.GetValue<int>("AppSettings:MarketDataFeedSnapshotApi:ClientId")!));
            services.AddSingleton<IMarketDataSnapshotApi, IBMarketDataSnapshotApi>();
            services.AddSingleton<IStatusConsoleWriter, StatusConsoleWriter>();
            var redisUri = config.GetValue<string>("AppSettings:RedisUri")!;
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisUri));
            services.AddSingleton<IRedisCache, RedisCache>();
            services.AddSingleton<IJsonSerializer, NewtonSoftJsonSerializer>();
            services.AddSingleton<IBlackboardService, BlackboardService>();
        }

        void RegisterStorageServices()
        {
            logger.LogInformationEvent("QueryServer", "register database contexts...");
            services.AddSingleton(_ =>
            new DbConnectionSettings()
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

        void RegisterDatabaseContexts()
        {
            logger.LogInformationEvent("QueryServer", "register database contexts...");
            services.AddSingleton(_ =>
              new DbConnectionSettings()
                .Add("EventDbConnection", config.GetConnectionString("EventDbConnection")!, "System.Data.SqlClient")
                .Add("TradeDbConnection", config.GetConnectionString("TradeDbConnection")!, "System.Data.SqlClient")
                .Add("FundDbConnection", config.GetConnectionString("FundDbConnection")!, "System.Data.SqlClient")
                .Add("ReferenceDbConnection", config.GetConnectionString("ReferenceDbConnection")!, "System.Data.SqlClient")
                .Add("OptionPricerDbConnection", config.GetConnectionString("OptionPricerDbConnection")!, "System.Data.SqlClient")
                .Add("MarketDataDbConnection", config.GetConnectionString("MarketDataDbConnection")!, "System.Data.SqlClient")
                .Add("LogDbConnection", config.GetConnectionString("LogDbConnection")!, "System.Data.SqlClient")
                .Add("YieldCurveRatesDbConnection", config.GetConnectionString("YieldCurveRatesDbConnection")!, "TomasAI.IFM.Storage")
                .Add("EconomicCalendarsDbConnection", config.GetConnectionString("EconomicCalendarsDbConnection")!, "TomasAI.IFM.Storage")
            );
            services.AddSingleton<IDbContextResolver>(_ => new DbContextResolver(e => GetContainerInstance(e)!));
            services.AddSingleton<IDbContextFactory, DbContextFactory>();
            services.AddSingleton<ITradeDbContext, TradeDbContext>();
            services.AddSingleton<IFundDbContext, FundDbContext>();
            services.AddSingleton<IReferenceDbContext, ReferenceDbContext>();
            services.AddSingleton<IOptionPricerDbContext, OptionPricerDbContext>();
            services.AddSingleton<IMarketDataDbContext, MarketDataDbContext>();
            services.AddSingleton<ILogDbContext, LogDbContext>();
            services.AddSingleton<IYieldCurveRatesDbContext, YieldCurveRatesDbContext>();
            services.AddSingleton<IEconomicCalendarsDbContext, EconomicCalendarsDbContext>();
        }

        void RegisterQueryHandlers()
        {
            logger.LogInformationEvent("QueryServer", "register query service handlers...");
            services.AddSingleton<IQueryHandlerResolver>(_ => new QueryHandlerResolver(handlerType => GetContainerInstance(handlerType)!));
            services.AddSingleton<IQueryService, QueryService>();
        }

        void RegisterEventConsumers()
        {
            logger.LogInformationEvent("QueryServer", "register query event consumers...");
            services.AddSingleton<IEventConsumerOptions>(new KafkaEventConsumerOptions(
                groupId: null,
                bootstrapServers: config.GetValue<string>("AppSettings:EventConsumer:BootstrapServers")!,
                enableAutoCommit: true));
        }

        void RegisterEventProducers()
        {
            logger.LogInformationEvent("QueryServer", "register query event consumers...");
            services.AddSingleton<IEventProducerOptions>(new KafkaEventProducerOptions(config.GetValue<string>("AppSettings:EventProducer:BootstrapServers")!));
            services.AddSingleton<IFundEventProducer, FundEventProducer>();
            services.AddSingleton<IMarketDataEventProducer, MarketDataEventProducer>();
            services.AddSingleton<IMarketDataFeedEventProducer, MarketDataFeedEventProducer>();
            services.AddSingleton<IOptionPricerEventProducer, OptionPricerEventProducer>();
            services.AddSingleton<IReferenceEventProducer, ReferenceEventProducer>();
            services.AddSingleton<ITradeEventProducer, TradeEventProducer>();
            services.AddSingleton<IStatusConsoleEventProducer, StatusConsoleEventProducer>();
        }

        void RegisterHostedServices()
        {
            logger.LogInformationEvent("QueryServer", "register query hosted services...");
            // system admin hosted service...
            services.AddSingleton<ISystemAdminEventProducer, SystemAdminEventProducer>();
            services.AddSingleton<IAzureStorageOptions>(_ => config.GetSection("AzureStorage").Get<AzureStorageOptions>()!);
            services.AddSingleton<IAzureStorage, AzureStorage>();
            services.AddSingleton<IDatabaseBackupService, QueryDatabaseBackupService>();
            services.AddSingleton<IDatabaseBackupEventConsumer, DatabaseBackupEventConsumer>();
            services.AddHostedService<SystemAdminHostedService>();
        }

         object? GetContainerInstance(Type handlerType)
        {
            var containerInstance = default(object);
            try
            {
                containerInstance = _container.GetInstance(handlerType);
            }
            catch (Exception ex)
            {
                containerInstance = null;
            }
            return containerInstance;
        }
    }

    public static WebApplication ConfigureRequestPipeline(this WebApplication app, Microsoft.Extensions.Logging.ILogger logger)
    {
        // configure the HTTP request pipeline...
        InitContainer();
        logger.LogInformationEvent("QueryServer", "configure HTTP request pipeline...");
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
        logger.LogInformationEvent("QueryServer", "web app configuration completed");
        return app;

        void InitContainer()
        {
            // register open generic handlers...
            logger.LogInformationEvent("QueryServer", "register query open generic handlers...");

            var assemblies = new List<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
            assemblies.AddRange([
                FundDomainAssembly.Current,
                FundTransactionDomainAssembly.Current,
                MarketDataDomainAssembly.Current,
                MarketDataAnalyticsDomainAssembly.Current,
                MarketDataFeedDomainAssembly.Current,
                OptionPricerDomainAssembly.Current,
                 SystemAdminDomainAssembly.Current,
                TradeDomainAssembly.Current
            ]);
            _container.Register(typeof(IObjectRepository<>), assemblies, Lifestyle.Transient);
            _container.Register(typeof(IAsyncQueryHandler<,>), assemblies, Lifestyle.Singleton);
            _container.Register(typeof(IQueryHandler<,>), assemblies, Lifestyle.Singleton);
            _container.Register(typeof(IAsyncEventHandler<>), assemblies, Lifestyle.Singleton);
            _container.Register(typeof(IQueryState<>), assemblies, Lifestyle.Singleton);

            // allow Simple Injector to resolve services from ASP.NET Core...
            app.Services.UseSimpleInjector(_container);
            _container.Verify();
            logger.LogInformationEvent("QueryServer", "query open generic handlers registered");
        }
    }
}
