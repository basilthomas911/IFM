using TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery;
using TomasAI.IFM.Application.Api.Server.Core.Development.Provisioning;
using TomasAI.IFM.Application.Api.Server.Core.Development.Verification;
using TomasAI.IFM.Application.Api.Server.Core.MarketData.Initialization;
using TomasAI.IFM.Application.Api.Server.Core.MarketData.OptionChains;
using TomasAI.IFM.Application.Api.Server.Core.MarketData.Rollover;
using TomasAI.IFM.Application.Api.Server.Core.MarketData.Sessions;
using TomasAI.IFM.Application.Api.Server.Core.Observability.HealthChecks;
using TomasAI.IFM.Application.Api.Server.Core.Observability.StatusConsole;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Actor;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Supervisor.Shared.Service.Health;
using TomasAI.IFM.Domain.Supervisor.Shared.Service;
using TomasAI.IFM.Domain.Supervisor.Shared.Service.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Service.Metrics;
using TomasAI.IFM.Domain.Supervisor.Recovery;
using TomasAI.IFM.Domain.Supervisor.Recovery.Event.Projection;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using Hazelcast;
using Hazelcast.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using StackExchange.Redis;
using System.Buffers;
using System.Reflection;
using System.Text.Json.Serialization;
using TomasAI.IFM.Application.Actor.Client;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Application.MarketData.Host.Worker;
using TomasAI.IFM.Application.MarketData.FinancialModelingPrep;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Application.Storage.PortfolioDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Application.Storage.TradePlanDb.Schema;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Domain.Application.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Application.Storage.SystemAdminDb.Schema;
using TomasAI.IFM.Domain.Portfolio;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Projection;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.MarketData;
using TomasAI.IFM.Domain.MarketData.Query;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Recovery;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model.Processing;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Query;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Feed;
using TomasAI.IFM.Domain.MarketData.Securities;
using TomasAI.IFM.Domain.Reference;
using TomasAI.IFM.Domain.Reference.Services;
using TomasAI.IFM.Domain.SystemAdmin;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;
using TomasAI.IFM.Domain.Trade;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Development;
using DomainApplicationActorAssembly = TomasAI.IFM.Domain.Application.Actor.ApplicationActorAssembly;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Caching.Redis;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;
using TomasAI.IFM.Framework.MarketData.TickAggregation;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Storage.Azure;
using TomasAI.IFM.Framework.Telemetry.Metrics;
using TomasAI.IFM.Domain.Application.Shared.ServiceApi;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Application.EventServices;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.Model;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Contracts;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.TradePlan.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.TradePlan.ServiceApi;

using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Application.MarketData.OptionChainCache;
namespace TomasAI.IFM.Application.Api.Server.Core.DependencyInjection;
public static partial class CoreServiceRegistration
{
    static void RegisterRemoteMarketDataRuntimeServices(IServiceCollection services, ConfigurationManager config)
    {
        services.AddSingleton<IStatusConsoleWriter, StatusConsoleWriter>();
        services.AddSingleton<IAzureStorageOptions>(_ => config.GetSection("AzureStorage").Get<AzureStorageOptions>()!);
        services.AddSingleton<IAzureStorage, AzureStorage>();
        services.AddSingleton(new MarketOutlookSnapshotPersistencePolicy(MarketOutlookSnapshotSource.DatabentoLive));
        services.AddSingleton(new MarketOutlookSnapshotQueryPolicy(RejectSyntheticSnapshots: true));
        services.AddSingleton<FuturesValueDateProvider>();
        services.AddSingleton<IValueDateProvider>(p => p.GetRequiredService<FuturesValueDateProvider>());
        services.AddSingleton<FuturesMarketSessionAuthority>();
        services.AddSingleton<IFuturesMarketSessionAuthority>(p => p.GetRequiredService<FuturesMarketSessionAuthority>());
        services.AddSingleton<ICompletedFuturesEndOfDayProjection>(p => p.GetRequiredService<FuturesMarketSessionAuthority>());
        services.AddHostedService<FuturesMarketSessionAuthorityHostedService>();
        services.AddSingleton<CmeFuturesMarketSessionCalendar>();
        services.AddSingleton<IMarketSessionCalendar>(p => p.GetRequiredService<CmeFuturesMarketSessionCalendar>());
        services.AddSingleton<IFuturesExchangeBusinessCalendar>(p => p.GetRequiredService<CmeFuturesMarketSessionCalendar>());
        services.AddSingleton<IHistoricalReplayPublisher, NullHistoricalReplayPublisher>();
        services.AddSingleton<IHistoricalAnalyticsSignalReader, NullHistoricalAnalyticsSignalReader>();
        services.AddSingleton<IFuturesEodAnalyticsAssembler, FuturesEodAnalyticsAssembler>();
        services.AddSingleton(p => new HistoricalDataLoader(p.GetRequiredService<IMarketDataApi>()));
        services.AddSingleton<OptionChainCache>();
        services.AddSingleton<IOptionChainCache>(p => p.GetRequiredService<OptionChainCache>());
        services.AddSingleton<RemoteReferenceDataApi>();
        services.AddSingleton<IReferenceDataApi>(p => p.GetRequiredService<RemoteReferenceDataApi>());
        services.AddSingleton<TomasAI.IFM.Shared.MarketData.Contracts.ITreasuryCurve>(p => p.GetRequiredService<RemoteReferenceDataApi>());
        services.AddSingleton<TomasAI.IFM.Shared.MarketData.Contracts.IEconomicCalendar>(p => p.GetRequiredService<RemoteReferenceDataApi>());
        services.AddSingleton<MarketDataReadMirror>();
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Recovery.OptionFeedProcessingHealth>();
        services.AddSingleton(p => new MarketDataHostConnection(p.GetRequiredService<NatsConnectionManager>(),
            config["MarketDataHost:NatsUrl"] ?? p.GetService<INatsProducerOptions>()?.Url ?? "nats://localhost:4222", config["MarketDataHost:Scope"] ?? "development"));
        services.AddSingleton<IMarketDataApi>(p => new MarketDataApiClient(p.GetRequiredService<MarketDataHostConnection>(), p.GetRequiredService<MarketDataReadMirror>()));
        services.AddSingleton<ITickAggregationEventPublisher, TickAggregationEventPublisher>();
        services.AddSingleton<FourHourDatabentoSeedReplay>();
        services.AddSingleton<MarketDataClientReceiver>();
        services.AddSingleton<MarketDataRemoteAdmission>();
        services.AddSingleton<IRealtimeSourceAdmission>(p => p.GetRequiredService<MarketDataRemoteAdmission>());
        services.AddHostedService(p => p.GetRequiredService<MarketDataClientReceiver>());
        services.AddHostedService<TomasAI.IFM.Application.Api.Server.Core.HostedServices.OptionFeedRecoveryNotificationService>();
        services.AddSingleton<IOptionPipelineRecoveryRuntime, OptionPipelineRecoveryRuntime>();
        services.AddSingleton<OptionPipelineRecoveryCoordinator>();
        services.AddSingleton<TomasAI.IFM.Application.Api.Server.Core.HostedServices.ApiRecoveryRestartEscalation>();
        services.AddSingleton<TomasAI.IFM.Application.Api.Server.Core.HostedServices.TradeFeedRecoveryStartupService>();
        services.AddHostedService(p => p.GetRequiredService<TomasAI.IFM.Application.Api.Server.Core.HostedServices.TradeFeedRecoveryStartupService>());
        services.AddHostedService<OptionRecoveryLiveVerificationService>();
        services.AddHostedService<TomasAI.IFM.Application.Api.Server.Core.HostedServices.OptionPipelineRecoveryHostedService>();
        services.AddSingleton<TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.TradeRealtimeCacheRegistry>();
        services.AddHostedService<TomasAI.IFM.Application.Api.Server.Core.HostedServices.TradeRealtimeCacheExpiryService>();
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.ICompositionPreparationStore>(p => p.GetRequiredService<IMarketDataDbContext>());
        services.AddSingleton<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.CompositionDiscoveryHandoff>();
        services.AddSingleton<IHistoricalDailyReplayPublisher, FuturesEmaBbHistoricalDailyReplayPublisher>();
        services.AddSingleton(_ =>
        {
            var configured = config
            .GetSection("AppSettings:HistoricalAnalyticsWarmup")
            .Get<HistoricalAnalyticsWarmupOptions>() ?? new HistoricalAnalyticsWarmupOptions();
            return configured.Validate();
        });
        services.AddSingleton<HistoricalAnalyticsWarmupService>();
        services.AddSingleton<IFuturesTradeSessionBarSeriesResolver>(_ =>
        new PrefixFuturesTradeSessionBarSeriesResolver(
        new Dictionary<string, MarketSeriesIdentity>(StringComparer.OrdinalIgnoreCase)
        {
            ["ES"] = MarketSeriesIdentity.ForFuturesSeries(
            new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1))
        }));
        services.AddSingleton<FuturesTradeSessionBarAccumulatorRegistry>();
        services.AddSingleton(MarketOutlookHotCache.Shared);
        services.AddSingleton<IMarketOutlookHotCache>(provider =>
        provider.GetRequiredService<MarketOutlookHotCache>());
        services.AddSingleton<IMarketOutlookHotCacheWriter>(provider =>
        provider.GetRequiredService<MarketOutlookHotCache>());
        services.AddSingleton<MarketOutlookProcessorMetrics>();
        services.AddSingleton<DatabentoWatchdogMetrics>();
        services.AddSingleton<DatasetWorkerAdmissionRegistry>(p => p.GetRequiredService<MarketDataRemoteAdmission>().Futures);
        services.AddSingleton<MarketDataOperationsHealthService>();
        services.AddSingleton<IMarketDataOperationsRecorder>(provider =>
        new CompositeMarketDataOperationsRecorder(
        provider.GetRequiredService<MarketOutlookProcessorMetrics>(),
        provider.GetRequiredService<DatabentoWatchdogMetrics>(),
        provider.GetRequiredService<MarketDataOperationsHealthService>()));
        services.AddSingleton<MarketOutlookUpdateChannel>();
        services.AddSingleton<IMarketOutlookUpdateWriter>(provider =>
        provider.GetRequiredService<MarketOutlookUpdateChannel>());
        services.AddSingleton<IMarketOutlookUpdateReader>(provider =>
        provider.GetRequiredService<MarketOutlookUpdateChannel>());
        services.AddSingleton<LatestMarketOutlookSnapshotPublisher>();
        services.AddSingleton<IMarketOutlookSnapshotPublisher>(provider =>
        provider.GetRequiredService<LatestMarketOutlookSnapshotPublisher>());
        services.AddHostedService(provider =>
        provider.GetRequiredService<LatestMarketOutlookSnapshotPublisher>());
        services.AddSingleton<MarketOutlookUpdateProcessor>();
        services.AddSingleton<IMarketOutlookOperations>(provider =>
        provider.GetRequiredService<MarketOutlookUpdateProcessor>());
        services.AddHostedService(provider =>
        provider.GetRequiredService<MarketOutlookUpdateProcessor>());
        services.AddHostedService<MarketDataBackendHealthObserver>();
        services.AddSingleton<TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility.LiveOptionChainIvCollector>(p => new(
            p.GetRequiredService<IMarketDataDbContext>(), p.GetRequiredService<IHostEnvironment>(), p.GetRequiredService<ILogger<TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility.LiveOptionChainIvCollector>>()));
        services.AddSingleton<TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility.IOptionChainIvCollector>(p => p.GetRequiredService<TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility.LiveOptionChainIvCollector>());
        services.AddHostedService(p => p.GetRequiredService<TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility.LiveOptionChainIvCollector>());
        services.AddHostedService<DevelopmentOptionChainParameterSeeder>();
        services.AddSingleton<LivePipelineEvidence>();
        services.AddSingleton<FuturesItiSignalRuntimeTelemetry>();
        services.AddSingleton<MarketDataRuntimeHealthCheck>();
        services.AddSingleton<ActorRuntimeHealthCheck>();
        services.AddSingleton((config.GetSection("MarketDataRecovery:LivePipeline").Get<LivePipelineMonitorOptions>() ?? new()) with { RecoveryOwnedByHost = true });
        services.AddSingleton<ILivePipelineProbe, RemoteLivePipelineProbe>();
        services.AddSingleton<LivePipelineMonitor>();
        services.AddHostedService(p => p.GetRequiredService<LivePipelineMonitor>());
        services.AddHostedService<HistoricalDailyAnalyticsInitializationService>();
        services.AddHostedService<FuturesRolloverPreparationHostedService>();
        services.AddSingleton((config.GetSection("AppSettings:Databento:OptionExpiryCalendar")
        .Get<OptionContractExpiryCalendarOptions>() ?? new()).Validate());
        services.AddSingleton<OptionContractExpiryCalendarRefreshService>();
        services.AddHostedService<OptionContractExpiryCalendarStartupService>();
        services.AddHostedService<ApplicationStartupCommandDispatcher>();
    }
}
