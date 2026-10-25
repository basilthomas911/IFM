using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Diagnostics;
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
using TomasAI.IFM.Application.EventServices;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.Model;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.Application.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.TradePlan.ServiceApi;
using TomasAI.IFM.UI.Net.Views.Presentation;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.Services.SystemAdmin;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.Services;
using TomasAI.IFM.UI.Net.Services.Analytics;
using TomasAI.IFM.UI.Net.Services.Application;
using TomasAI.IFM.UI.Net.Services.MarketData;
using TomasAI.IFM.UI.Net.Services.MarketDataFeed;
using TomasAI.IFM.UI.Net.Services.OptionPricing;
using TomasAI.IFM.UI.Net.Services.Trade;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.TradePlan.ServiceApi;

namespace TomasAI.IFM.UI.Net
{
    public class Startup : IAppRoot
    {
        public Microsoft.Extensions.Logging.ILogger DiagnosticLogger => _container?.GetInstance<Microsoft.Extensions.Logging.ILogger>() ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        public DesktopTradeExecutionContext TradeExecution => DesktopTradeExecutionContext.Resolve(_config, AppEnvironment);
        static Container? _container;
        static IConfiguration? _config;
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
            new TomasAI.IFM.UI.Net.Composition.UiClientServiceRegistration(_container,config).Register();
            _container.RegisterSingleton<YieldCurveRateEditViewModel>();
            _container.RegisterSingleton<IViewNavigator>(()=>new WinFormsViewNavigator(viewType=>_container.GetInstance(typeof(IForm<>).MakeGenericType(viewType))));
            _container.RegisterSingleton<IUserInteraction>(()=>new WinFormsUserInteraction());
            _container.Verify();
            return _container.GetInstance<IViewNavigator>();
        }

        static void RegisterLogger()
        {
            var logging = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.Debug()
                .WriteTo.Async(sink => sink.File(
                    new Serilog.Formatting.Json.JsonFormatter(renderMessage: true),
                    Path.Combine(AppContext.BaseDirectory, "Logs", "ifm-ui-.log"),
                    rollingInterval: RollingInterval.Day,
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(1)),
                    bufferSize: 4096, blockWhenFull: false,
                    monitor: new TomasAI.IFM.Framework.Telemetry.Logging.AsyncLogBufferMonitor());
            if (_config?.GetValue<bool>("Telemetry:Logs:Enabled") == true)
                logging.WriteTo.Sink(new TomasAI.IFM.Framework.Telemetry.Logging.OtlpStructuredLogSink(_config, "TomasAI.IFM.UI"));
            Log.Logger = logging.CreateLogger();
            var loggerFactory = new SerilogLoggerFactory(Log.Logger);
            var uiLogger = loggerFactory.CreateLogger("IFM.UI");
            _container!.RegisterInstance(uiLogger);
            UiExceptionReporter.Configure(uiLogger);
            _container!.RegisterSingleton<ILogger<EventChannel>>(() => new EventChannelLogger(_container.GetInstance<Microsoft.Extensions.Logging.ILogger>()));
        }

        static IAppRoot RegisterApplication()
        {
            var appRoot = new Startup();
            _container!.Register<IAppRoot>(() => appRoot, Lifestyle.Singleton);
            var asmForm = new Assembly[] { typeof(IForm<>).Assembly };
            // Each window owns its ViewModels and event subscriptions; a closed form must never be reused.
            _container!.Register(typeof(IForm<>), asmForm, Lifestyle.Transient);
            foreach (var producer in _container.GetCurrentRegistrations().Where(producer =>
                         producer.ServiceType.IsGenericType
                         && producer.ServiceType.GetGenericTypeDefinition() == typeof(IForm<>)))
            {
                producer.Registration.SuppressDiagnosticWarning(
                    DiagnosticType.DisposableTransientComponent,
                    "WinForms owns each transient form and disposes it when its window lifetime ends.");
            }
            return appRoot;
        }








        /// <summary>Connects the shared command/query transport before the WinForms shell starts loading data.</summary>
        public static async ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            if (_container is null)
                throw new InvalidOperationException("The UI container has not been configured.");

            var timeoutSeconds = _config?.GetValue<int?>("AppSettings:NatsStartupTimeoutSeconds") ?? 15;
            if (timeoutSeconds <= 0)
                throw new InvalidOperationException("AppSettings:NatsStartupTimeoutSeconds must be positive.");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                await _container.GetInstance<IActorProducer>().StartAsync(
                    new ActorMailboxId(ActorType.Query, "IFM.UI"),
                    timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                var natsUrl = _config?.GetValue<string>("AppSettings:NatsServerUri") ?? "<not configured>";
                throw new TimeoutException(
                    $"NATS startup at '{natsUrl}' did not complete within {timeoutSeconds} seconds.",
                    exception);
            }
        }

        /// <summary>Stops UI producers and disposes the shared NATS connection after all forms have closed.</summary>
        public static async ValueTask ShutdownAsync()
        {
            if (_container is null || Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
                return;

            List<Exception> failures = [];
            await StopIfRegisteredAsync<IStatusConsoleEventProducer>();
            await StopIfRegisteredAsync<IActorProducer>();
            try
            {
                // The container owns the shared NATS connection manager. Dispose
                // it asynchronously so async-only singleton registrations are
                // released without Simple Injector falling back to Dispose().
                await _container.DisposeAsync();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
            finally
            {
                _container = null;
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

            async ValueTask StopIfRegisteredAsync<TProducer>() where TProducer : class, IActorProducer
            {
                try
                {
                    var registration = _container.GetRegistration(typeof(TProducer), throwOnFailure: false);
                    if (registration?.GetInstance() is TProducer producer)
                        await StopAsync(producer);
                }
                catch (Exception exception)
                {
                    UiExceptionReporter.Report(exception, "UiShutdown",
                        $"ResolveOrStop:{typeof(TProducer).Name}", fatal: false);
                    failures.Add(exception);
                }
            }
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

        /// <summary>Gets the immutable catalog of explicitly registered UI domain services.</summary>
        public IUiServiceCatalog Services
            => _container!.GetInstance<IUiServiceCatalog>();

        /// <summary>
        /// return status console api
        /// </summary>
        /// <returns></returns>
        public IStatusConsoleWriter GetStatusConsoleWriter()
            => (_container!.GetInstance<IStatusConsoleWriter>()!);

        /// <summary>Executes an asynchronous UI operation through the application root boundary.</summary>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="cancellationToken">Cancels the operation before or during execution.</param>
        /// <returns>The task returned by the operation.</returns>
        public Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);
            cancellationToken.ThrowIfCancellationRequested();
            return operation(cancellationToken);
        }
    }

    /// <summary>Adapts the application logger to the event-channel logging contract.</summary>
    /// <param name="logger">The application logger that receives event-channel records.</param>
    public class EventChannelLogger(Microsoft.Extensions.Logging.ILogger logger)
        : ILogger<EventChannel>
    {
        readonly Microsoft.Extensions.Logging.ILogger _logger = logger;

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => _logger.BeginScope(state);

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
            => _logger.IsEnabled(logLevel);

        /// <summary>Writes an event-channel message at the supplied severity.</summary>
        /// <param name="level">The record severity.</param>
        /// <param name="message">The formatted event-channel message.</param>
        public void Log(LogLevel level, string message)
        {
            _logger.Log(level, message);
        }

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
