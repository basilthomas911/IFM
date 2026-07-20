using Serilog;
using Serilog.Events;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using StackExchange.Redis;
using System.Reflection;
using System.Text.Json.Serialization;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.PredictiveModel.Query.Services;
using TomasAI.IFM.Application.PredictiveModel.Server.HostedService;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.SequenceIdDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Caching.Redis;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Telemetry.Logging.Serilog;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.EventProducers;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Log.Model;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ServiceApi;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.PredictiveModel.Server;

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
                               .WriteTo.Http(requestUri: telemetryServerBaseUri, httpClient: new SerilogHttpClient(),queueLimitBytes: 10000)
                               .CreateLogger();
                       })
                       .UseKestrel()
                       .UseSerilog();

        // configure web app...
        var serviceProvider = builder.Services.BuildServiceProvider();
        logger = serviceProvider.GetRequiredService<ILogger<Program>>() as Microsoft.Extensions.Logging.ILogger;
        builder.Services.AddSingleton<Microsoft.Extensions.Logging.ILogger>(logger);

        logger.LogInformationEvent("PredictiveModelServer", "configure web app...");
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
        logger.LogInformationEvent("PredictiveModelServer", "add web app services...");
        RegisterBaseServices();
        RegisterDatabaseContexts();
        RegisterQueryHandlers();
        RegisterEventConsumers();
        RegisterEventProducers();
        RegisterHostedServices();
        return services;

        void RegisterBaseServices()
        {
            logger.LogInformationEvent("PredictiveModelServer", "register base services...");
            services.AddSingleton<IDbCache, DbCache>();
            services.AddSingleton<IDataCacheService, LocalDataCacheService>();
            services.AddSingleton<IStatusConsoleWriter, StatusConsoleWriter>();
            services.AddSingleton<IEventServiceHandlerResolver>(_ => new EventServiceHandlerResolver(eventHandlerType => GetContainerInstance(eventHandlerType)));
            var redisUri = config.GetValue<string>("AppSettings:RedisUri")!;
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisUri));
            services.AddSingleton<IRedisCache, RedisCache>();
            services.AddSingleton<IJsonSerializer, NewtonSoftJsonSerializer>();
            services.AddSingleton<IBlackboardService, BlackboardService>();
            services.AddSingleton<IFuturesItiPredictiveTrendModel, FuturesItiPredictiveTrendModel>();
        }

        void RegisterDatabaseContexts()
        {
            logger.LogInformationEvent("PredictiveModelServer", "register database contexts...");
            services.AddSingleton(_ =>
              new DbConnectionSettings()
                .Add("SequenceIdDbConnection", config.GetConnectionString("SequenceIdDbConnection")!, "System.Data.Postgres")
                .Add("MarketDataDbConnection", config.GetConnectionString("MarketDataDbConnection")!, "System.Data.ScyllaDb")
                .Add("SecuritiesDbConnection", config.GetConnectionString("SecuritiesDbConnection")!, "System.Data.ScyllaDb")
            );
            services.AddSingleton<IDbContextResolver>(_ => new DbContextResolver(e => GetContainerInstance(e)));
            services.AddSingleton<IDbContextFactory, DbContextFactory>();
            services.AddSingleton<IMarketDataDbContext, MarketDataDbContext>();
            services.AddSingleton<ISequenceIdDbContext, SequenceIdDbContext>();
            services.AddSingleton<ISequenceIdGenerator, PostgresSequenceIdGenerator>();

        }

        void RegisterQueryHandlers()
        {
            logger.LogInformationEvent("PredictiveModelServer", "register query service handlers...");
            services.AddSingleton<IQueryHandlerResolver>(_ => new QueryHandlerResolver(handlerType => GetContainerInstance(handlerType)));
            services.AddSingleton<IQueryService, QueryService>();
        }

        void RegisterEventConsumers()
        {
            logger.LogInformationEvent("PredictiveModelServer", "register query event consumers...");
            services.AddSingleton<IEventConsumerOptions>(new KafkaEventConsumerOptions(
                groupId: null!,
                bootstrapServers: config.GetValue<string>("AppSettings:EventConsumer:BootstrapServers")!,
                enableAutoCommit: true));
        }

        void RegisterEventProducers()
        {
            logger.LogInformationEvent("PredictiveModelServer", "register query event producers...");
            services.AddSingleton<IEventProducerOptions>(new KafkaEventProducerOptions(config.GetValue<string>("AppSettings:EventProducer:BootstrapServers")!));
            services.AddSingleton<IFuturesItiTrendEventProducer, FuturesItiTrendEventProducer>();
            services.AddSingleton<IStatusConsoleEventProducer, StatusConsoleEventProducer>();
        }

        void RegisterHostedServices()
        {
            logger.LogInformationEvent("PredictiveModelServer", "register query hosted services...");
            // predictive model service...
            services.AddSingleton<IPredictiveModelService, PredictiveModelService>();
            services.AddSingleton<IPredictiveModelServerEventConsumer, PredictiveModelServerEventConsumer>();
            services.AddHostedService<PredictiveModelServerHostedService>();
        }

        object GetContainerInstance(Type handlerType)
        {
            var containerInstance = default(object);
            try
            {
                containerInstance = _container.GetInstance(handlerType);
            }
            catch (Exception ex)
            {
                logger.LogErrorEvent("PredictiveModelServer", ex, "fatal error during GetInstance call..." );
                containerInstance = null;
            }
            return containerInstance!;
        }

    }

    public static WebApplication ConfigureRequestPipeline(this WebApplication app, Microsoft.Extensions.Logging.ILogger logger)
    {
        // configure the HTTP request pipeline...
        InitContainer();
        logger.LogInformationEvent("PredictiveModelServer", "configure HTTP request pipeline...");
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
        logger.LogInformationEvent("PredictiveModelServer", "web app configuration completed");
        return app;

        void InitContainer()
        {
            // register open generic handlers...
            logger.LogInformationEvent("PredictiveModelServer", "register query open generic handlers...");

            var assemblies = new List<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
            _container.Register(typeof(IObjectRepository<>), assemblies, Lifestyle.Transient);
            _container.Register(typeof(IAsyncQueryHandler<,>), assemblies, Lifestyle.Singleton);
            _container.Register(typeof(IQueryHandler<,>), assemblies, Lifestyle.Singleton);
            _container.Register(typeof(IAsyncEventHandler<>), assemblies, Lifestyle.Singleton);
            _container.Register(typeof(IAsyncEventHandler<,>), assemblies, Lifestyle.Singleton);
            _container.Register(typeof(IAsyncEventServiceHandler<,>), assemblies, Lifestyle.Singleton);

            // allow Simple Injector to resolve services from ASP.NET Core...
            app.Services.UseSimpleInjector(_container);
            _container.Verify();
            logger.LogInformationEvent("PredictiveModelServer", "query open generic handlers registered");
        }
    }
}

