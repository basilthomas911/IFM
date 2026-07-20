using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using StackExchange.Redis;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log.Model;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.TradePlan.ServiceApi;
using TomasAI.IFM.Shared.Fund.ServiceApi;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.AlgoTrader;
using TomasAI.IFM.Shared.AlgoTrader.ServiceApi;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventProducers;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ServiceApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Caching.Redis;
using TomasAI.IFM.Application.Command.Client;
using TomasAI.IFM.Application.Command.Client.PredictiveModel.FuturesItiTrend;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Domain.MarketData.Analytics;
using TomasAI.IFM.Domain.MarketData.Analytics.HostedServices;
using TomasAI.IFM.Domain.MarketData.Analytics.Models;
using TomasAI.IFM.Domain.MarketData.Feed;
using TomasAI.IFM.Domain.MarketData.Feed.HostedServices;
using TomasAI.IFM.Domain.MarketData.Feed.Model;
using TomasAI.IFM.Service.MarketDataFeed.InteractiveBrokers;
using TomasAI.IFM.Service.PredictiveModel;
using TomasAI.IFM.Service.PredictiveModel.HostedService;
using TomasAI.IFM.Service.AlgoTrader;
using TomasAI.IFM.Service.AlgoTrader.HostedService;
using TomasAI.IFM.Service.TradePosition;
using TomasAI.IFM.Service.TradePosition.HostedService;
using TomasAI.IFM.Service.TradePlan;
using TomasAI.IFM.Service.TradePlan.HostedService;
using TomasAI.IFM.Service.ErrorConsole;
using TomasAI.IFM.Service.ErrorConsole.HostedService;
using TomasAI.IFM.Service.Fund;
using TomasAI.IFM.Service.Fund.HostedService;
using TomasAI.IFM.Service.OptionPricer;
using TomasAI.IFM.Service.OptionPricer.HostedService;
using TomasAI.IFM.Domain.Trade.Placement;
using TomasAI.IFM.Domain.Trade.Placement.HostedService;
using TomasAI.IFM.Domain.Trade.Placement.Model;

namespace TomasAI.IFM.Application.Event.Server;

public static class Startup
{
    internal static IConfiguration Configuration { get; set; }
    internal static ILogger Logger { get; set; }

    /// <summary>
    /// Configures services and dependencies for the application, including hosted services, event consumers, and API
    /// options.
    /// </summary>
    /// <remarks>This method sets up various services and hosted services required by the application,
    /// including event producers,  consumers, and APIs. It integrates Simple Injector for dependency injection and
    /// registers open generic instances  for event handling. Configuration values are retrieved from the provided
    /// <paramref name="configuration"/> object  to initialize specific services.</remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to which services will be added.</param>
    /// <param name="configuration">The application's configuration settings, used to retrieve values for service initialization.</param>
    /// <param name="container">The Simple Injector container used for dependency injection.</param>
    public static void ConfigureEventServices(this IServiceCollection services, IConfiguration configuration, Container container)
    {
        Configuration = configuration;
        services.AddSimpleInjector(container, options =>
        {
            options.AddLogging();
            Logger = CreateLogger(services);
            Logger.LogInformationEvent("EventServer","configuring event server...");
            RegisterBaseServices(services, container);
            RegisterCommandApiServices(services);
            RegisterQueryApiServices(services);
            RegisterEventProducers(services);

            // Hooks hosted services into the Generic Host pipeline
            // while resolving them through Simple Injector...
            Logger.LogInformationEvent("EventServer","registering hosted services...");
            services.AddSingleton<IEventConsumerOptions>(new KafkaEventConsumerOptions(null, Configuration.GetValue<string>("AppSettings:EventProducer:BootstrapServers"), true));

            // algo trader hosted service...
            services.AddSingleton<IAlgoTraderService, AlgoTraderService>();
            services.AddSingleton<IAlgoTraderEventConsumer, AlgoTraderEventConsumer>();
            options.AddHostedService<AlgoTraderHostedService>();

            // market data feed hosted service...
            services.AddSingleton<IMarketDataFeedEventService, MarketDataFeedEventService>();
            services.AddSingleton<IMarketDataApiOptions>(sp => new IBMarketDataApiOptions(
                host: Configuration.GetValue<string>("AppSettings:MarketDataFeedApi:Host"),
                port: Configuration.GetValue<int>("AppSettings:MarketDataFeedApi:Port"),
                clientId: Configuration.GetValue<int>("AppSettings:MarketDataFeedApi:ClientId")));
            services.AddSingleton<IMarketDataApi, IBMarketDataApi>();

            services.AddSingleton<IMarketDataSnapshotApiOptions>(sp => new IBMarketDataSnapshotApiOptions(
                 host: Configuration.GetValue<string>("AppSettings:MarketDataFeedSnapshotApi:Host"),
                 port: Configuration.GetValue<int>("AppSettings:MarketDataFeedSnapshotApi:Port"),
                clientId: Configuration.GetValue<int>("AppSettings:MarketDataFeedSnapshotApi:ClientId")));
            services.AddSingleton<IMarketDataSnapshotApi, IBMarketDataSnapshotApi>();

            services.AddSingleton<IMarketDataFeedEventConsumer, MarketDataFeedEventConsumer>();
            services.AddSingleton<IFuturesBarDataTimer, FuturesBarDataTimer>();
            options.AddHostedService<MarketDataFeedHostedService>();

            // trade position hosted service...
            services.AddSingleton<ITradePositionService, TradePositionService>();
            services.AddSingleton<ITradePositionEventConsumer, TradePositionEventConsumer>();
            options.AddHostedService<TradePositionHostedService>();

            // trade plan hosted service...
            services.AddSingleton<ITradePlanService, TradePlanService>();
            services.AddSingleton<ITradePlanEventConsumer, TradePlanEventConsumer>();
            options.AddHostedService<TradePlanHostedService>();

            // trade placement hosted service...
            services.AddSingleton<ITradePlacementEventService, TradePlacementEventService>();
            services.AddSingleton<ITradePlacementEventConsumer, TradePlacementEventConsumer>();
            services.AddSingleton<ITradePlacementTimer, TradePlacementTimer>();
            options.AddHostedService<TradePlacementHostedService>();

            // market data analytics hosted service...
            services.AddSingleton<IMarketDataAnalyticsService, MarketDataAnalyticsEventService>();
            services.AddSingleton<IMarketDataAnalyticsEventConsumer, MarketDataAnalyticsEventConsumer>();
            services.AddSingleton<IFuturesRsiSignalTimer, FuturesRsiSignalTimer>();
            options.AddHostedService<MarketDataAnalyticsHostedService>();

            // option pricer hosted service...
            services.AddSingleton<ISpreadDistributionServiceApi, SpreadDistributionService>();
            services.AddSingleton<IOptionPricerEventConsumer, OptionPricerEventConsumer>();
            options.AddHostedService<OptionPricerHostedService>();

            // error console hosted service...
            services.AddSingleton<IErrorConsoleService, ErrorConsoleService>();
            services.AddSingleton<IErrorConsoleEventConsumer, ErrorConsoleEventConsumer>();
            options.AddHostedService<ErrorConsoleHostedService>();

            // fund event hosted service...
            services.AddSingleton<IFundEventService, FundEventService>();
            services.AddSingleton<IFundEventConsumer, FundEventConsumer>();
            options.AddHostedService<FundHostedService>();

            // predictive model hosted service...
            services.AddSingleton<IPredictiveModelService, PredictiveModelService>();
            services.AddSingleton<IPredictiveModelEventConsumer, PredictiveModelEventConsumer>();
            options.AddHostedService<PredictiveModelHostedService>();

            // add container open generic instances that services collection cannot handle...
            var assemblies = new List<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
            container.Register(typeof(IAsyncEventHandler<>), assemblies, Lifestyle.Singleton);
            container.Register(typeof(IAsyncEventHandler<,>), assemblies, Lifestyle.Singleton);
            container.Register(typeof(IAsyncEventServiceHandler<,>), assemblies, Lifestyle.Singleton);
            Logger.LogInformationEvent("EventServer","event server configured successfully...");
        });

    }

    static ILogger CreateLogger(IServiceCollection services)
    {
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>() as ILogger;
        services.AddSingleton<ILogger>(logger);
        return  logger;
    }

    static void RegisterBaseServices(IServiceCollection services, Container container)
    {
        Logger.LogInformationEvent("EventServer","registering base services...");
        var redisUri = Configuration.GetValue<string>("AppSettings:RedisUri");
        services.AddSingleton<IConnectionMultiplexer>( _ => ConnectionMultiplexer.Connect(redisUri));
        services.AddSingleton<IRedisCache, RedisCache>();
        services.AddSingleton<IBlackboardService, BlackboardService>();
        services.AddSingleton<IJsonSerializer, NewtonSoftJsonSerializer>();
        services.AddSingleton<IStatusConsoleWriter, StatusConsoleWriter>();
        services.AddSingleton<IDataCacheService, LocalDataCacheService>();
        services.AddSingleton<IEventServiceApiResolver>(_ => new EventServiceApiResolver(eventHandlerType => container.GetInstance(eventHandlerType)));
        services.AddSingleton<IEventServiceHandlerResolver>(_ => new EventServiceHandlerResolver(eventHandlerType => container.GetInstance(eventHandlerType)));
    }

    static void RegisterCommandApiServices(IServiceCollection services)
    {
        Logger.LogInformationEvent("EventServer","registering command api services...");
        services.AddSingleton<ICommandServiceRestApiOptions>(_ => new CommandServiceRestApiOptions(Configuration.GetValue<string>("AppSettings:CommandServerBaseUri")));
        services.AddSingleton<ICommandService, CommandServiceRestApiClient>();
        services.AddSingleton<ITradeCommandApi, TradeCommandApi>();
        services.AddSingleton<ITradeOrderCommandApi, TradeOrderCommandApi>();
        services.AddSingleton<ITradePlanCommandApi, TradePlanCommandApi>();
        services.AddSingleton<IMarketDataFeedCommandApi, MarketDataFeedCommandApi>();
        services.AddSingleton<IMarketDataAnalyticsCommandApi, MarketDataAnalyticsCommandApi>();
        services.AddSingleton<IFundCommandApi, FundCommandApi>();
        services.AddSingleton<IOptionPricerCommandApi, OptionPricerCommandApi>();
        services.AddSingleton<ITradePlacementCommandApi, TradePlacementCommandApi>();
        services.AddSingleton<IFuturesItiTrendCommandApi, FuturesItiTrendCommandApi>();
    }

    static void RegisterQueryApiServices(IServiceCollection services)
    {
        Logger.LogInformationEvent("EventServer","registering query api services...");
        services.AddSingleton<IQueryServiceRestApiOptions>(_ => new QueryServiceRestApiOptions(Configuration.GetValue<string>("AppSettings:QueryServerBaseUri")));
        services.AddSingleton<IQueryService, QueryServiceRestApiClient>();
        /*
        services.AddSingleton<IMarketDataAnalyticsQueryApi, MarketDataAnalyticsQueryApi>();
        services.AddSingleton<IMarketDataFeedQueryApi, MarketDataFeedQueryApi>();
        services.AddSingleton<IMarketDataQueryApi, MarketDataQueryApi>();
        services.AddSingleton<IOptionPricerQueryApi, OptionPricerQueryApi>();
        services.AddSingleton<ITradeQueryApi, TradeQueryApi>();
        services.AddSingleton<ITradePlanQueryApi, TradePlanQueryApi>();
        services.AddSingleton<IFundQueryApi, FundQueryApi>();
        */
    }

    static void RegisterEventProducers(IServiceCollection services)
    {
        Logger.LogInformationEvent("EventServer","registering query api services...");
        services.AddSingleton<IEventProducerOptions>(new KafkaEventProducerOptions(Configuration.GetValue<string>("AppSettings:EventProducer:BootstrapServers")));
        services.AddSingleton<IStatusConsoleEventProducer, StatusConsoleEventProducer>();
        services.AddSingleton<IErrorConsoleEventProducer, ErrorConsoleEventProducer>();
        services.AddSingleton<IExceptionEventProducer, ExceptionEventProducer>();
        services.AddSingleton<IFundEventProducer, FundEventProducer>();
        services.AddSingleton<IMarketDataEventProducer, MarketDataEventProducer>();
        services.AddSingleton<IMarketDataFeedEventProducer, MarketDataFeedEventProducer>();
        services.AddSingleton<IOptionPricerEventProducer, OptionPricerEventProducer>();
        services.AddSingleton<ITradeEventProducer, TradeEventProducer>();
        services.AddSingleton<ITradeOrderEventProducer, TradeOrderEventProducer>();
        services.AddSingleton<ITradeStrategyEventProducer, TradeStrategyEventProducer>();
    }

}
