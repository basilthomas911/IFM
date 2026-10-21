using SimpleInjector.Diagnostics;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using System.Reflection;
using TomasAI.IFM.Application.Api.Nats.Client;
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
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.TradePlan.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.UI.EventConsumer;
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

namespace TomasAI.IFM.UI.Net.Composition;
/// <summary>Registers framework-neutral client transports, domain APIs and services for both UI hosts.</summary>
public sealed class UiClientServiceRegistration(Container container,IConfiguration configuration)
{
 readonly Container _container=container;
 readonly IConfiguration _config=configuration;
 public void Register(){RegisterBaseServices();RegisterQueryServices();RegisterCommandServices();RegisterEventConsumers();RegisterEventProducers();RegisterPresentationServices();}
        void RegisterBaseServices()
        {
            var natsServerUri = _config!.GetValue<string>("AppSettings:NatsServerUri");
            if (string.IsNullOrWhiteSpace(natsServerUri))
                throw new InvalidOperationException("AppSettings:NatsServerUri is required.");

            _container!.RegisterInstance(TimeProvider.System);
            _container!.RegisterSingleton<IValueDateProvider, FuturesValueDateProvider>();
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
            _container.RegisterSingleton<ISupervisorCommandApi>(() => new SupervisorCommandApi(
                _container.GetInstance<IActorProducer>()));
            _container!.Register<IActorEventListener>(() => new NatsActorEventListener(
                _container.GetInstance<INatsEventListenerOptions>(),
                _container.GetInstance<Microsoft.Extensions.Logging.ILogger>(),
                _container.GetInstance<NatsConnectionManager>()));

        }

        void RegisterQueryServices()
        {
            _container!.RegisterSingleton<IApplicationQueryApi, ApplicationQueryApi>();
            _container!.RegisterSingleton<IPortfolioQueryApi, PortfolioQueryApi>();
            _container!.RegisterSingleton<PortfolioFinancialApi>();
            _container!.RegisterSingleton<TomasAI.IFM.Domain.Portfolio.Shared.Financial.IPortfolioFinancialApi>(
                () => _container.GetInstance<PortfolioFinancialApi>());
            _container!.RegisterSingleton<TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.IPortfolioOrderCompositionApi>(
                () => _container.GetInstance<PortfolioFinancialApi>());
            _container!.RegisterSingleton<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement.IRiskQueryApi, RiskQueryApi>();
            _container!.RegisterSingleton<IPortfolioIdentityApi, PortfolioIdentityApi>();
            _container!.RegisterSingleton<IMarketDataAnalyticsQueryApi, MarketDataAnalyticsQueryApi>();
            _container!.RegisterSingleton<IMarketDataFeedQueryApi, MarketDataFeedQueryApi>();
            _container!.RegisterSingleton<IMarketDataQueryApi, MarketDataQueryApi>();
            _container!.RegisterSingleton<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.IMarketConditionAssessmentQueryApi, MarketConditionAssessmentQueryApi>();
            _container!.RegisterSingleton<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ServiceApi.IIntrinsicTimeStrategyWorkflowQueryApi, IntrinsicTimeStrategyWorkflowQueryApi>();
            _container!.RegisterSingleton<IReferenceQueryApi, ReferenceQueryApi>();
            _container!.RegisterSingleton<ITradeQueryApi, OptionTradeQueryApi>();
            _container!.RegisterSingleton<TomasAI.IFM.Domain.BrokerAccount.Contracts.IBrokerAccountQueryApi,
                BrokerAccountQueryApi>();
            _container!.RegisterSingleton<TomasAI.IFM.Domain.Trade.Shared.Order.Broker.IBrokerOrderCommandApi, TomasAI.IFM.Application.Api.Nats.Client.BrokerOrderCommandApi>();
            _container!.RegisterSingleton<TomasAI.IFM.Domain.Trade.Shared.Order.Broker.IBrokerOrderQueryApi,
                BrokerOrderQueryApi>();
            _container!.RegisterSingleton<TomasAI.IFM.Domain.Trade.Shared.Order.Execution.IOrderExecutionQueryApi,
                OrderExecutionQueryApi>();
            _container!.RegisterSingleton<ITradePlanQueryApi, TradePlanQueryApi>();
            _container!.RegisterSingleton<
                TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IStrategyTradePlanQueryApi,
                StrategyTradePlanQueryApi>();
            _container!.RegisterSingleton<TomasAI.IFM.Domain.Trade.Shared.ServiceApi.IStrategyPositionQueryApi,
                StrategyPositionQueryApi>();
            _container!.RegisterSingleton<IDatabaseBackupQueryApi, DatabaseBackupQueryApi>();
        }

        void RegisterCommandServices()
        {
            _container!.RegisterSingleton<IApplicationCommandApi, ApplicationCommandApi>();
            _container!.RegisterSingleton<IPortfolioCommandApi, PortfolioCommandApi>();
            _container!.RegisterSingleton<IPortfolioFinancialPolicyCommandApi, PortfolioFinancialPolicyCommandApi>();
            _container!.RegisterSingleton<IPortfolioFundCommandApi, PortfolioFundCommandApi>();
            _container!.RegisterSingleton<TomasAI.IFM.Domain.BrokerAccount.Contracts.IBrokerAccountCommandApi,
                BrokerAccountCommandApi>();
            _container!.RegisterSingleton<IDatabaseBackupCommandApi, DatabaseBackupCommandApi>();
            _container!.RegisterSingleton<TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.ServiceApi.IScheduledTaskCommandApi, ScheduledTaskCommandApi>();
            _container!.RegisterSingleton<TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.ServiceApi.IScheduledTaskQueryApi, ScheduledTaskQueryApi>();
            _container!.RegisterSingleton<ITradePlacementCommandApi, TradePlacementCommandApi>();
            _container!.RegisterSingleton<TomasAI.IFM.Domain.Trade.Shared.ServiceApi.IStrategyPositionCommandApi,
                StrategyPositionCommandApi>();
            _container!.RegisterSingleton<TomasAI.IFM.Domain.Trade.Shared.ServiceApi.IEstablishedTradeQueryApi, EstablishedTradeQueryApi>();
            _container!.RegisterSingleton<TomasAI.IFM.Domain.Trade.Shared.ServiceApi.ITradeOrderLifecycleApi,
                TradeOrderLifecycleApi>();
            _container!.RegisterSingleton<IMarketDataFeedCommandApi, MarketDataFeedCommandApi>();
            _container!.RegisterSingleton<IMarketDataCommandApi, MarketDataCommandApi>();
            _container!.RegisterSingleton<IMarketDataAnalyticsCommandApi, MarketDataAnalyticsCommandApi>();
            _container!.RegisterSingleton<ITradePlanCommandApi, TradePlanCommandApi>();
            _container!.RegisterSingleton<IReferenceCommandApi, ReferenceCommandApi>();
            _container!.RegisterSingleton<TomasAI.IFM.Domain.Reference.Shared.ParameterSets.IParameterSetsApi, ParameterSetsApi>();
        }

        void RegisterEventConsumers()
        {
            _container!.RegisterSingleton<IFuturesEodDataUIEventConsumer, FuturesEodDataUIEventConsumer>();
            _container!.RegisterSingleton<IFuturesTradeSignalUIEventConsumer, FuturesTradeSignalUIEventConsumer>();
            _container!.RegisterSingleton<IMarketOutlookUIEventConsumer, MarketOutlookUIEventConsumer>();
            _container!.RegisterSingleton<IFuturesRsiSignalUIEventConsumer, FuturesRsiSignalUIEventConsumer>();
            _container!.RegisterSingleton<IFuturesBarDataUIEventConsumer, FuturesBarDataUIEventConsumer>();
            _container!.RegisterSingleton<IFuturesOptionTickDataUIEventConsumer, FuturesOptionTickDataUIEventConsumer>();
            _container!.RegisterSingleton<ITradePositionUIEventConsumer, TradePositionUIEventConsumer>();
            _container!.RegisterSingleton<IMarketDataFeedResetUIEventConsumer, MarketDataFeedResetUIEventConsumer>();
            _container!.RegisterSingleton<IMarketDataFeedStatusUIEventConsumer, MarketDataFeedStatusUIEventConsumer>();
            _container!.RegisterSingleton<ITradePlanUIEventConsumer, TradePlanUIEventConsumer>();
            _container!.RegisterSingleton<ITradePlacementUIEventConsumer, TradePlacementUIEventConsumer>();
            _container!.RegisterSingleton<ITradePlanActionUIEventConsumer, TradePlanActionUIEventConsumer>();
            _container!.RegisterSingleton<IMarketDataUIEventConsumer, MarketDataUIEventConsumer>();
            _container!.RegisterSingleton<IStatusConsoleEventConsumer, StatusConsoleEventConsumer>();
            _container!.RegisterSingleton<ICommandResponseUIEventConsumer, CommandResponseUIEventConsumer>();
            // Calendar dashboard and editor own independent listener lifecycles and may be open concurrently.
            _container!.Register<IEconomicCalendarUIEventConsumer, EconomicCalendarUIEventConsumer>(Lifestyle.Transient);
            _container!.Register<ILookupTypeUIEventConsumer, LookupTypeUIEventConsumer>(Lifestyle.Transient);
            _container!.Register<ISystemAdminUIEventConsumer, SystemAdminUIEventConsumer>(Lifestyle.Transient);
            _container!.Register<IScheduledTaskUIEventConsumer, ScheduledTaskUIEventConsumer>(Lifestyle.Transient);
            // These consumers belong to individual view subscriptions. Their owners call StopAsync
            // when closing, which unsubscribes and disposes the listener connection; sharing a
            // singleton would let one window stop another window's notifications.
            foreach (var consumerType in new[] { typeof(IEconomicCalendarUIEventConsumer), typeof(ILookupTypeUIEventConsumer), typeof(ISystemAdminUIEventConsumer), typeof(IScheduledTaskUIEventConsumer) })
            {
                _container.GetRegistration(consumerType)!.Registration.SuppressDiagnosticWarning(
                    DiagnosticType.DisposableTransientComponent,
                    "The owning view stops its independent listener and disposes its notification subscription when it closes.");
            }
            _container!.RegisterSingleton<IApplicationUIEventConsumer, ApplicationUIEventConsumer>();
            _container!.RegisterSingleton<IOptionTradeSpreadBarDataUIEventConsumer, OptionTradeSpreadBarDataUIEventConsumer>();
            _container!.RegisterSingleton<IFuturesItiSignalUIEventConsumer, FuturesItiSignalUIEventConsumer>();
            _container!.RegisterSingleton<IIntrinsicTimeStrategyWorkflowUIEventConsumer, IntrinsicTimeStrategyWorkflowUIEventConsumer>();
        }

        void RegisterEventProducers()
        {
            _container!.RegisterSingleton<IStatusConsoleEventProducer, StatusConsoleEventProducer>();
            _container!.RegisterSingleton<IStatusConsoleWriter, StatusConsoleWriter>();
        }

        void RegisterPresentationServices()
        {
            _container!.RegisterInstance(_config!.GetSection("SupervisorControl").Get<SupervisorControlOptions>()
                ?? new SupervisorControlOptions());
            _container!.RegisterSingleton<CommandResponseEventService>();
            _container.RegisterSingleton<ApplicationEventService>();
            _container.RegisterSingleton<ApplicationQueryService>();
            _container.RegisterSingleton<StatusConsoleService>();
            _container.RegisterSingleton<MarketDataCommandService>();
            _container.RegisterSingleton<MarketDataQueryService>();
            _container.RegisterSingleton<IMarketDataOperationsHealthQueryService>(() =>
            {
                var configured = _config!.GetValue<string>("MarketDataOperationsHealth:Endpoint");
                var endpoint = Uri.TryCreate(configured, UriKind.Absolute, out var uri) ? uri : null;
                return new MarketDataOperationsHealthQueryService(
                    new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }), endpoint, ownsHttpClient: true);
            });
            _container.Register<IActorHealthQueryService>(() =>
            {
                var configured = _config!.GetValue<string>("MarketDataOperationsHealth:Endpoint");
                var operationsEndpoint = Uri.TryCreate(configured, UriKind.Absolute, out var uri) ? uri : null;
                var endpoint = operationsEndpoint is null ? null : new Uri(operationsEndpoint, "/api/actor-health");
                return new ActorHealthQueryService(
                    new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }), endpoint, ownsHttpClient: true);
            }, Lifestyle.Transient);
            _container.RegisterSingleton<MarketDataEventService>();
            _container.RegisterSingleton<OptionTradeSpreadBarDataEventService>();
            _container.RegisterSingleton<MarketDataFeedCommandService>();
            _container.RegisterSingleton<MarketDataFeedQueryService>();
            _container.RegisterSingleton<MarketDataAnalyticsCommandService>();
            _container.RegisterSingleton<MarketDataAnalyticsQueryService>();
            _container.RegisterSingleton<MarketDataAnalyticsEventService>();
            _container.RegisterSingleton<IOptionPricingService, OptionPricingService>();
            _container.RegisterSingleton<StrategyOperationsService>(() => StrategyOperationsComposition.Create(
                _container.GetInstance<IMarketDataAnalyticsQueryApi>(),
                _container.GetInstance<IFuturesItiSignalUIEventConsumer>(),
                _container.GetInstance<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ServiceApi.IIntrinsicTimeStrategyWorkflowQueryApi>(),
                _container.GetInstance<IIntrinsicTimeStrategyWorkflowUIEventConsumer>()));
            _container.RegisterSingleton<TradeQueryService>();
            _container.RegisterSingleton<TradePlacementCommandService>();
            _container.RegisterSingleton<TradePlacementEventService>();
            _container.RegisterSingleton<IOrderExecutionNotificationService>(() =>
                new OrderExecutionNotificationService(() => _container.GetInstance<IActorEventListener>()));
            _container.RegisterSingleton<TradePlanQueryService>();
            _container.RegisterSingleton<StrategyTradePlanQueryService>();
            _container.RegisterSingleton<StrategyPositionService>();
            _container.RegisterSingleton<PortfolioTradeOrderService>();
            _container.RegisterSingleton<TradePlanEventService>();
            _container.RegisterSingleton<TradePlanActionEventService>();
            _container.RegisterSingleton<TradePositionFeedEventService>();
            _container.RegisterSingleton<IUiServiceCatalog, UiServiceCatalog>();
            _container!.Register<IDatabaseBackupService, DatabaseBackupService>(Lifestyle.Transient);
            _container!.Register<IScheduledTaskService, ScheduledTaskService>(Lifestyle.Transient);
            _container.Register<IReferenceDataService, ReferenceDataService>(Lifestyle.Transient);
            _container.Register<IEconomicCalendarService, EconomicCalendarService>(Lifestyle.Transient);
        }
}
