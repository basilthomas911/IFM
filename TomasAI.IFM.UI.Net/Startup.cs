using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using SimpleInjector;
using System.Net;
using System.Reflection;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Service.StatusConsole;
using TomasAI.IFM.Shared.EventChannel;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProducers;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.Model;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.Application.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.TradePlan.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net
{
    public class Startup : IAppRoot
    {
        static Container? _container;
        static IConfiguration ?_config;

        /// <summary>
        /// initialize dependency injector container...
        /// </summary>
        /// <returns>application root</returns>
        public static IAppRoot Configure(IConfigurationRoot config)
        {
            _config = config;
            _container = new Container();
            RegisterLogger();
            var appRoot = RegisterApplication();
            RegisterBaseServices();
            RegisterQueryServices();
            RegisterCommandServices();
            RegisterEventConsumers();
            RegisterEventProducers();
            _container.Verify();
            return appRoot;
        }

        static void RegisterLogger()
        {
            Log.Logger = new LoggerConfiguration()
              .Enrich.FromLogContext()
              .MinimumLevel.Debug()
              .WriteTo.Console()
              .CreateLogger();
            var loggerFactory = new SerilogLoggerFactory(Log.Logger);
            _container!.RegisterInstance(loggerFactory.CreateLogger("IFM.UI"));
            _container!.RegisterSingleton<ILogger<EventChannel>>(() => new EventChannelLogger(_container.GetInstance<Microsoft.Extensions.Logging.ILogger>()));
        }

        static IAppRoot RegisterApplication()
        {
            var appRoot = new Startup();
            _container!.Register<IAppRoot>(() => appRoot, Lifestyle.Singleton);
            var asmForm = new Assembly[] { typeof(IForm<>).Assembly };
            _container!.Register(typeof(IForm<>), asmForm, Lifestyle.Singleton);
            var asmModel = new Assembly[] { typeof(IModel<>).Assembly };
            _container!.Register(typeof(IModel<>), asmModel, Lifestyle.Transient);
            return appRoot;
        }


        static void RegisterBaseServices()
        {
             _container!.RegisterSingleton(() => {
                var services = new ServiceCollection();
                services.AddHttpClient("HttpRestApi").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
                {
                    UseDefaultCredentials = true,
                    Credentials = new NetworkCredential("", ""),
                });
                var serviceProvider = services.BuildServiceProvider();
                return serviceProvider.GetService<IHttpClientFactory>()!;
            });
            //_container!.RegisterSingleton<IJsonSerializer, SystemTextJsonSerializer>();
            _container!.RegisterSingleton<IJsonSerializer, NewtonSoftJsonSerializer>();
            _container!.RegisterSingleton<INatsProducerOptions, NatsProducerOptions>();
            _container!.RegisterSingleton<INatsConsumerOptions, NatsConsumerOptions>();
            _container!.RegisterSingleton<INatsEventListenerOptions, NatsEventListenerOptions>();
            _container!.Register<IActorProducer, NatsActorProducer>();
            _container!.Register<IActorEventListener, NatsActorEventListener>();

        }

        static void RegisterQueryServices()
        {
            _container!.RegisterSingleton<IQueryServiceApiOptions>(() => new QueryServiceApiOptions(_config!.GetValue<string>("AppSettings:QueryServerBaseUri")!));
            _container!.RegisterSingleton<IQueryServiceApi, QueryServiceApiClient>();
            _container!.RegisterSingleton<IOptionPricerQueryApi, OptionPricerQueryApi>();
            _container!.RegisterSingleton<IMarketDataAnalyticsQueryApi, MarketDataAnalyticsQueryApi>();
            _container!.RegisterSingleton<IMarketDataFeedQueryApi, MarketDataFeedQueryApi>();
            _container!.RegisterSingleton<IMarketDataQueryApi, MarketDataQueryApi>();
            _container!.RegisterSingleton<IReferenceQueryApi, ReferenceQueryApi>();
            _container!.RegisterSingleton<IFundQueryApi, FundQueryApi>();
            _container!.RegisterSingleton<ITradeQueryApi, OptionTradeQueryApi>();
            _container!.RegisterSingleton<ITradePlanQueryApi, TradePlanQueryApi>();
           _container!.RegisterSingleton<ISystemAdminQueryApi, SystemAdminQueryApi>();
        }

        static void RegisterCommandServices()
        {
            _container!.RegisterSingleton<ICommandServiceApiOptions>(() => new CommandServiceApiOptions(_config!.GetValue<string>("AppSettings:CommandServerBaseUri")!));
            _container!.RegisterSingleton<ICommandServiceApi, CommandServiceApiClient>();
            _container!.RegisterSingleton<IApplicationCommandApi, ApplicationCommandApi>();
            _container!.RegisterSingleton<ISystemAdminCommandApi, SystemAdminCommandApi>();
            _container!.RegisterSingleton<ITradeCommandApi, OptionTradeCommandApi>();
            _container!.RegisterSingleton<ITradePlacementCommandApi, TradePlacementCommandApi>();
            _container!.RegisterSingleton<IOptionPricerCommandApi, OptionPricerCommandApi>();
            _container!.RegisterSingleton<IMarketDataFeedCommandApi, MarketDataFeedCommandApi>();
            _container!.RegisterSingleton<IMarketDataCommandApi, MarketDataCommandApi>();
            _container!.RegisterSingleton<IMarketDataAnalyticsCommandApi, MarketDataAnalyticsCommandApi>();
            _container!.RegisterSingleton<IFundCommandApi, FundCommandApi>();
            _container!.RegisterSingleton<ITradePlanCommandApi, TradePlanCommandApi>();
            _container!.RegisterSingleton<IReferenceCommandApi, ReferenceCommandApi>();
        }

        static void RegisterEventConsumers()
        {
            _container!.RegisterSingleton<IEventConsumerOptions>(() => new KafkaEventConsumerOptions(default!, _config!.GetValue<string>("AppSettings:BootstrapServers")!, true));
            _container!.RegisterSingleton<IFuturesEodDataUIEventConsumer, FuturesEodDataUIEventConsumer>();
            _container!.RegisterSingleton<IFuturesTradeSignalUIEventConsumer, FuturesTradeSignalUIEventConsumer>();
            _container!.RegisterSingleton<IFuturesRsiSignalUIEventConsumer, FuturesRsiSignalUIEventConsumer>();
            _container!.RegisterSingleton<IFundRiskMarginUIEventConsumer, FundRiskMarginUIEventConsumer>();
            _container!.RegisterSingleton<IFuturesBarDataUIEventConsumer, FuturesBarDataUIEventConsumer>();
            _container!.RegisterSingleton<IFuturesOptionTickDataUIEventConsumer, FuturesOptionTickDataUIEventConsumer>();
            _container!.RegisterSingleton<ITradePositionUIEventConsumer, TradePositionUIEventConsumer>();
            _container!.RegisterSingleton<IMarketDataFeedResetUIEventConsumer, MarketDataFeedResetUIEventConsumer>();
            _container!.RegisterSingleton<ITradePlanUIEventConsumer, TradePlanUIEventConsumer>();
            _container!.RegisterSingleton<ITradePlacementUIEventConsumer, TradePlacementUIEventConsumer>();
            _container!.RegisterSingleton<IFundOrderTradeStateUIEventConsumer, FundOrderTradeStateUIEventConsumer>();
            _container!.RegisterSingleton<ITradePlanActionUIEventConsumer, TradePlanActionUIEventConsumer>();
            _container!.RegisterSingleton<IFundUIEventConsumer, FundUIEventConsumer>();
            _container!.RegisterSingleton<IFundOrderUIEventConsumer, FundOrderUIEventConsumer>();
            _container!.RegisterSingleton<IMarketDataUIEventConsumer, MarketDataUIEventConsumer>();
            _container!.RegisterSingleton<IEndOfDayProcessUIEventConsumer, EndOfDayProcessUIEventConsumer>();
            _container!.RegisterSingleton<IStatusConsoleEventConsumer, StatusConsoleEventConsumer>();
            _container!.RegisterSingleton<ICommandResponseUIEventConsumer, CommandResponseUIEventConsumer>();
            _container!.RegisterSingleton<IEconomicCalendarUIEventConsumer, EconomicCalendarUIEventConsumer>();
            _container!.RegisterSingleton<ISystemAdminUIEventConsumer, SystemAdminUIEventConsumer>();
            _container!.RegisterSingleton<IApplicationUIEventConsumer, ApplicationUIEventConsumer>();
            _container!.RegisterSingleton<IOptionTradeSpreadBarDataUIEventConsumer, OptionTradeSpreadBarDataUIEventConsumer>();
            _container!.RegisterSingleton<IFuturesItiSignalUIEventConsumer, FuturesItiSignalUIEventConsumer>();
            _container!.RegisterSingleton<IFuturesOptionQuoteDataUIEventConsumer, FuturesOptionQuoteDataUIEventConsumer>();
        }

        static void RegisterEventProducers()
        {
            _container!.RegisterSingleton<IEventProducerOptions>(() => new KafkaEventProducerOptions(_config!.GetValue<string>("AppSettings:BootstrapServers")!));
            _container!.RegisterSingleton<IStatusConsoleEventProducer, StatusConsoleEventProducer>();
            _container!.RegisterSingleton<IStatusConsoleWriter, StatusConsoleWriter>();
        }

        /// <summary>
        /// create singleton instance of application root
        /// </summary>
        /// <param name="container"></param>
        Startup()
        {
            AppEnvironment = _config!.GetValue<string>("AppSettings:AppEnvironment")!;
        }

        /// <summary>
        /// startup environment PROD/DEV
        /// </summary>
        public string AppEnvironment { get; }

        /// <summary>
        /// return container instance object that implements windows form
        /// </summary>
        /// <typeparam name="TWindowsForm"></typeparam>
        /// <returns>instance of specified windows form</returns>
        public TWindowsForm GetForm<TWindowsForm>() where TWindowsForm : Form 
            => (_container!.GetInstance<IForm<TWindowsForm>>() as TWindowsForm)!;

        /// <summary>
        /// return container instance object that implements controller class type
        /// </summary>
        /// <typeparam name="TController">controller class type</typeparam>
        /// <returns>instance of controller class type</returns>
        public TModel GetModel<TModel>() where TModel : class 
            => (_container!.GetInstance<IModel<TModel>>() as TModel)!;

        /// <summary>
        /// return status console api
        /// </summary>
        /// <returns></returns>
        public IStatusConsoleWriter GetStatusConsoleWriter() 
            => (_container!.GetInstance<IStatusConsoleWriter>()!);

        public void Execute(Action viewAction)
        {
            try
            {
                viewAction();
            }
            catch { }
        }
    }

    public class EventChannelLogger(Microsoft.Extensions.Logging.ILogger logger) 
        : ILogger<EventChannel>
    {
        readonly Microsoft.Extensions.Logging.ILogger _logger = logger;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => _logger.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel)
            => _logger.IsEnabled(logLevel);

        public void Log(LogLevel level, string message)
        {
            _logger.Log(level, message);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
