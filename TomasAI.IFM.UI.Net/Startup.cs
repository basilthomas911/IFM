using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using SimpleInjector;
using System.Reflection;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Service.StatusConsole;
using TomasAI.IFM.Shared.EventChannel;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProducers;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.Model;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.Application.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.TradePlan.ServiceApi;
using TomasAI.IFM.UI.Net.Views.Presentation;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.TradePlan.ServiceApi;

namespace TomasAI.IFM.UI.Net
{
    public class Startup : IAppRoot
    {
        static Container? _container;
        static IConfiguration ?_config;
        static int _shutdownStarted;

        /// <summary>
        /// initialize dependency injector container...
        /// </summary>
        /// <returns>configured application view navigator</returns>
        public static IViewNavigator Configure(IConfigurationRoot config)
        {
            _config = config;
            Interlocked.Exchange(ref _shutdownStarted, 0);
            _container = new Container();
            RegisterLogger();
            RegisterApplication();
            RegisterBaseServices();
            RegisterQueryServices();
            RegisterCommandServices();
            RegisterEventConsumers();
            RegisterEventProducers();
            RegisterPresentationServices();
            _container.Verify();
            return _container.GetInstance<IViewNavigator>();
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
            var natsServerUri = _config!.GetValue<string>("AppSettings:NatsServerUri");
            if (string.IsNullOrWhiteSpace(natsServerUri))
                throw new InvalidOperationException("AppSettings:NatsServerUri is required.");

            _container!.RegisterInstance(TimeProvider.System);
            //_container!.RegisterSingleton<IJsonSerializer, SystemTextJsonSerializer>();
            _container!.RegisterSingleton<IJsonSerializer, NewtonSoftJsonSerializer>();
            _container!.RegisterInstance<INatsProducerOptions>(new NatsProducerOptions { Url = natsServerUri });
            _container!.RegisterInstance<INatsConsumerOptions>(new NatsConsumerOptions { Url = natsServerUri });
            _container!.RegisterInstance<INatsEventListenerOptions>(new NatsEventListenerOptions { Url = natsServerUri });
            _container!.RegisterSingleton<NatsConnectionManager>();
            _container!.RegisterSingleton<IActorProducer>(() => new NatsActorProducer(
                _container.GetInstance<INatsProducerOptions>(),
                _container.GetInstance<Microsoft.Extensions.Logging.ILogger>(),
                _container.GetInstance<NatsConnectionManager>()));
            _container!.Register<IActorEventListener>(() => new NatsActorEventListener(
                _container.GetInstance<INatsEventListenerOptions>(),
                _container.GetInstance<Microsoft.Extensions.Logging.ILogger>(),
                _container.GetInstance<NatsConnectionManager>()));

        }

        static void RegisterQueryServices()
        {
            _container!.RegisterSingleton<IOptionPricerQueryApi, OptionPricerQueryApi>();
            _container!.RegisterSingleton<IMarketDataAnalyticsQueryApi, MarketDataAnalyticsQueryApi>();
            _container!.RegisterSingleton<IMarketDataFeedQueryApi, MarketDataFeedQueryApi>();
            _container!.RegisterSingleton<IMarketDataQueryApi, MarketDataQueryApi>();
            _container!.RegisterSingleton<IReferenceQueryApi, ReferenceQueryApi>();
            _container!.RegisterSingleton<IFundQueryApi, FundQueryApi>();
            _container!.RegisterSingleton<ITradeQueryApi, OptionTradeQueryApi>();
            _container!.RegisterSingleton<ITradePlanQueryApi, TradePlanQueryApi>();
            _container!.RegisterSingleton<IDatabaseBackupQueryApi, DatabaseBackupQueryApi>();
        }

        static void RegisterCommandServices()
        {
            _container!.RegisterSingleton<IApplicationCommandApi, ApplicationCommandApi>();
            _container!.RegisterSingleton<IDatabaseBackupCommandApi, DatabaseBackupCommandApi>();
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

        /// <summary>Connects the shared command/query transport before the WinForms shell starts loading data.</summary>
        public static ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            if (_container is null)
                throw new InvalidOperationException("The UI container has not been configured.");

            return _container.GetInstance<IActorProducer>().StartAsync(
                new ActorMailboxId(ActorType.Query, "IFM.UI"),
                cancellationToken);
        }

        /// <summary>Stops UI producers and disposes the shared NATS connection after all forms have closed.</summary>
        public static async ValueTask ShutdownAsync()
        {
            if (_container is null || Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
                return;

            List<Exception> failures = [];
            await StopAsync(_container.GetInstance<IStatusConsoleEventProducer>());
            await StopAsync(_container.GetInstance<IActorProducer>());
            try
            {
                await _container.GetInstance<NatsConnectionManager>().DisposeAsync();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
            finally
            {
                _container.Dispose();
                Log.CloseAndFlush();
            }

            if (failures.Count > 0)
                throw new AggregateException("One or more UI NATS transports failed to stop.", failures);

            async ValueTask StopAsync(IActorProducer producer)
            {
                try
                {
                    await producer.StopAsync();
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }
        }

        static void RegisterEventConsumers()
        {
            _container!.RegisterSingleton<IFuturesEodDataUIEventConsumer, FuturesEodDataUIEventConsumer>();
            _container!.RegisterSingleton<IFuturesTradeSignalUIEventConsumer, FuturesTradeSignalUIEventConsumer>();
            _container!.RegisterSingleton<IFuturesRsiSignalUIEventConsumer, FuturesRsiSignalUIEventConsumer>();
            _container!.RegisterSingleton<IFundRiskMarginUIEventConsumer, FundRiskMarginUIEventConsumer>();
            _container!.RegisterSingleton<IFuturesBarDataUIEventConsumer, FuturesBarDataUIEventConsumer>();
            _container!.RegisterSingleton<IFuturesOptionTickDataUIEventConsumer, FuturesOptionTickDataUIEventConsumer>();
            _container!.RegisterSingleton<ITradePositionUIEventConsumer, TradePositionUIEventConsumer>();
            _container!.RegisterSingleton<IMarketDataFeedResetUIEventConsumer, MarketDataFeedResetUIEventConsumer>();
            _container!.RegisterSingleton<IMarketDataFeedStatusUIEventConsumer, MarketDataFeedStatusUIEventConsumer>();
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
            // Calendar dashboard and editor own independent listener lifecycles and may be open concurrently.
            _container!.Register<IEconomicCalendarUIEventConsumer, EconomicCalendarUIEventConsumer>(Lifestyle.Transient);
            _container!.RegisterSingleton<ISystemAdminUIEventConsumer, SystemAdminUIEventConsumer>();
            _container!.RegisterSingleton<IApplicationUIEventConsumer, ApplicationUIEventConsumer>();
            _container!.RegisterSingleton<IOptionTradeSpreadBarDataUIEventConsumer, OptionTradeSpreadBarDataUIEventConsumer>();
            _container!.RegisterSingleton<IFuturesItiSignalUIEventConsumer, FuturesItiSignalUIEventConsumer>();
        }

        static void RegisterEventProducers()
        {
            _container!.RegisterSingleton<IStatusConsoleEventProducer, StatusConsoleEventProducer>();
            _container!.RegisterSingleton<IStatusConsoleWriter, StatusConsoleWriter>();
        }

        static void RegisterPresentationServices()
        {
            _container!.RegisterSingleton<YieldCurveRateEditViewModel>();
            _container!.RegisterSingleton<IViewNavigator>(() =>
                new WinFormsViewNavigator(viewType =>
                {
                    var formContract = typeof(IForm<>).MakeGenericType(viewType);
                    return _container.GetInstance(formContract);
                }));
            _container.RegisterSingleton<IUserInteraction>(() => new WinFormsUserInteraction());
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

        public Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);
            cancellationToken.ThrowIfCancellationRequested();
            return operation(cancellationToken);
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
