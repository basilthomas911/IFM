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
using TomasAI.IFM.Application.MarketData.Worker;
using TomasAI.IFM.Application.Storage.EventSourceDb.HistoricalDataLoader;
using TomasAI.IFM.Application.MarketData.FinancialModelingPrep;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Application.Storage.PortfolioDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Application.Storage.OptionPricerDb.Schema;
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
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Feed;
using TomasAI.IFM.Domain.MarketData.Securities;
using TomasAI.IFM.Domain.Reference;
using TomasAI.IFM.Domain.Reference.Services;
using TomasAI.IFM.Domain.SystemAdmin;
using TomasAI.IFM.Domain.OptionPricer;
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
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;
using TomasAI.IFM.Framework.MarketData.TickAggregation;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Azure;
using TomasAI.IFM.Framework.Telemetry.Metrics;
using TomasAI.IFM.TradePlan;
using TomasAI.IFM.TradePlan.HostedService;
using TomasAI.IFM.Service.TradePosition;
using TomasAI.IFM.Service.TradePosition.HostedService;
using TomasAI.IFM.Domain.Application.Shared.ServiceApi;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProducers;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.Model;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Contracts;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.TradePlan.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.TradePlan.ServiceApi;

namespace TomasAI.IFM.Application.Api.Server.Core.DependencyInjection;

public static partial class CoreServiceRegistration
{
    static void RegisterMarketDataRuntimeServices(IServiceCollection services, ConfigurationManager config, Microsoft.Extensions.Logging.ILogger logger, IHostEnvironment? hostEnvironment, Container siContainer, bool focusedActorIntegration)
    {
        logger.LogInformationEvent("ApiServer","{Component}.{Method} "+"register hosted services...",nameof(CoreServiceRegistration),nameof(RegisterHostedServices));
        services.AddSingleton<IStatusConsoleWriter, StatusConsoleWriter>();
        services.AddSingleton<IAzureStorageOptions>(sp => config.GetSection("AzureStorage").Get<AzureStorageOptions>()!);
        services.AddSingleton<IAzureStorage, AzureStorage>();
        var dataset = config.GetValue<string>("AppSettings:Databento:Dataset")
        ?? "GLBX.MDP3";
        var profileName = config.GetValue<string>(
        "AppSettings:Databento:DeploymentProfile");
        var deploymentProfile = Enum.TryParse<FeedDeploymentProfile>(
        profileName, true, out var configuredProfile)
        ? configuredProfile
        : FeedDeploymentProfile.Development;
        var contracts = config
        .GetSection("AppSettings:Databento:Contracts")
        .Get<DatabentoContractRegistration[]>() ?? [];
        var feedOptions = DatabentoFeedOptions.ForProfile(
        deploymentProfile, dataset);
        var dataSourceName = config.GetValue<string>(
        "AppSettings:Databento:DataSource");
        if (Enum.TryParse<FeedDataSourceMode>(
        dataSourceName, true, out var configuredDataSource))
        {
            feedOptions = feedOptions with
            {
                DataSource = configuredDataSource
            };
        }
        logger.LogInformationEvent(
        "ApiServer",
        $"configure Databento market-data source: configured='{dataSourceName ?? "(default)"}', effective='{feedOptions.DataSource}'.");
        var configuredSynthetic = config
        .GetSection("AppSettings:Databento:Synthetic")
        .Get<SyntheticFeedOptions>();
        if (configuredSynthetic is not null
        && feedOptions.DataSource == FeedDataSourceMode.Synthetic)
        {
            feedOptions = feedOptions with
            {
                Synthetic = configuredSynthetic
            };
        }
        SyntheticPersistenceIsolationGuard.Validate(
        feedOptions,
        config.GetConnectionString("EventSourceActorDbConnection"),
        config.GetConnectionString("MarketDataDbConnection"));
        var snapshotSource = feedOptions.DataSource == FeedDataSourceMode.Synthetic
        ? MarketOutlookSnapshotSource.Synthetic
        : MarketOutlookSnapshotSource.DatabentoLive;
        services.AddSingleton(new MarketOutlookSnapshotPersistencePolicy(snapshotSource));
        services.AddSingleton(new MarketOutlookSnapshotQueryPolicy(
        RejectSyntheticSnapshots:
        feedOptions.DataSource == FeedDataSourceMode.DatabentoLive));
        var runtimeOptions = new DatabentoMarketDataRuntimeOptions
        {
            FeedOptions = feedOptions,
            OptionPricingRefresh = config.GetSection("AppSettings:Databento:OptionPricingRefresh")
            .Get<TomasAI.IFM.Application.MarketData.Pricing.OptionPricingRefreshPolicy>() ?? new(),
            Contracts = contracts,
            FuturesQuoteBatchCapacity = config.GetValue(
            "AppSettings:Databento:FuturesQuoteBatchCapacity", (ushort)64),
            FuturesOptionQuoteBatchCapacity = config.GetValue(
            "AppSettings:Databento:FuturesOptionQuoteBatchCapacity", (ushort)64),
            TradeStrategySymbolDatasets = config.GetSection("AppSettings:Databento:TradeStrategySymbolDatasets")
            .Get<string[]>() ?? []
        };
        services.AddDatabentoMarketDataServices();
        services.AddSingleton<FuturesValueDateProvider>();
        services.AddSingleton<IValueDateProvider>(provider => provider.GetRequiredService<FuturesValueDateProvider>());
        services.AddSingleton<ICompletedFuturesEndOfDayProjection>(provider => provider.GetRequiredService<FuturesMarketSessionAuthority>());
        services.AddSingleton<FuturesMarketSessionAuthority>();
        services.AddSingleton<IFuturesMarketSessionAuthority>(provider =>
        provider.GetRequiredService<FuturesMarketSessionAuthority>());
        services.AddHostedService<FuturesMarketSessionAuthorityHostedService>();
        services.AddSingleton<ITickAggregationEventPublisher,
        TickAggregationEventPublisher>();
        services.AddApplicationMarketDataApi(runtimeOptions);
        services.AddSingleton<FourHourDatabentoSeedReplay>();
        services.AddSingleton(new DatabentoWatchdogOptions
        {
            Enabled = config.GetValue("MarketDataRecovery:Enabled", true),
            NativeBackend = config.GetValue("MarketDataRecovery:NativeBackend", "Cpp")!,
            PollInterval = config.GetValue("MarketDataRecovery:PollInterval", TimeSpan.FromSeconds(15)),
            ProbeTimeout = config.GetValue("MarketDataRecovery:ProbeTimeout", TimeSpan.FromSeconds(1)),
            AttemptTwoDelay = config.GetValue("MarketDataRecovery:AttemptTwoDelay", TimeSpan.FromSeconds(5)),
            AttemptThreeDelay = config.GetValue("MarketDataRecovery:AttemptThreeDelay", TimeSpan.FromSeconds(15)),
            PersistenceRetryDelay = config.GetValue("MarketDataRecovery:PersistenceRetryDelay", TimeSpan.FromMilliseconds(100)),
            HardStallTimeout = config.GetValue("MarketDataRecovery:HardStallTimeout", TimeSpan.FromMinutes(5)),
            DatasetTeardownTimeout = config.GetValue("MarketDataRecovery:DatasetTeardownTimeout", TimeSpan.FromSeconds(10)),
            DatasetQualificationTimeout = config.GetValue("MarketDataRecovery:DatasetQualificationTimeout", TimeSpan.FromSeconds(30))
        }.Validate());
        var stage3Options = (config.GetSection("MarketDataRecovery:Stage3")
        .Get<DatabentoStage3Options>() ?? new DatabentoStage3Options()).Validate();
        if (runtimeOptions.FuturesQuoteBatchCapacity is 0 or > FuturesTickQuoteDataSegment.MaximumCount
        || runtimeOptions.FuturesOptionQuoteBatchCapacity is 0 or > FuturesTickQuoteDataSegment.MaximumCount)
        throw new ArgumentOutOfRangeException(
        nameof(runtimeOptions), "Configured quote batch capacities must be between 1 and 4096.");
        if ((runtimeOptions.FuturesQuoteBatchCapacity > 64
        || runtimeOptions.FuturesOptionQuoteBatchCapacity > 64)
        && (!stage3Options.Enabled
        || feedOptions.DataSource != FeedDataSourceMode.Synthetic))
        throw new InvalidOperationException(
        "Quote batches above 64 are qualified only for isolated Synthetic Development with bounded Stage 3 publishing.");
        services.AddSingleton((config.GetSection("MarketDataRecovery:Stage4")
        .Get<TomasAI.IFM.Application.MarketData.Subscriptions.Stage4SubscriptionOptions>()
        ?? new TomasAI.IFM.Application.MarketData.Subscriptions.Stage4SubscriptionOptions())
        .ValidateForApplicationStartup());
        if (stage3Options.Enabled && feedOptions.DataSource != FeedDataSourceMode.Synthetic
        && !(deploymentProfile == FeedDeploymentProfile.Development
        && config.GetValue<bool>("MarketDataRecovery:Stage3:AllowDevelopmentLiveQualification")))
        throw new InvalidOperationException(
        "Stage 3 live-provider workers require an explicit Development live-qualification opt-in.");
        services.AddSingleton(new TomasAI.IFM.Application.MarketData.Pricing.PricingSourceClockPolicy(
        deploymentProfile == FeedDeploymentProfile.Development && stage3Options.Enabled
        && feedOptions.DataSource == FeedDataSourceMode.DatabentoLive
        && config.GetValue<bool>("MarketDataRecovery:Stage3:AllowDevelopmentLiveQualification") ? 2000 : 0));
        services.AddSingleton(stage3Options);
        services.AddSingleton<DatasetDesiredSubscriptionRegistry>();
        if (stage3Options.Enabled)
        {
            services.AddSingleton<DatasetWorkerCurrentValues>();
            services.AddSingleton((config.GetSection("MarketDataRecovery:Stage3:RealtimePublisher")
            .Get<TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation.RealtimeTickPublisherPolicy>()
            ?? new TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation.RealtimeTickPublisherPolicy()).Validate());
        }
        services.AddSingleton(new DatabentoSupervisedWorkerOptions
        {
            DotNetHostPath = stage3Options.Enabled
            ? ResolveDotNetHostPath() : Environment.ProcessPath!,
            WorkerAssemblyPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            config.GetValue<string>("MarketDataRecovery:Stage3:WorkerAssemblyPath")
            ?? (deploymentProfile == FeedDeploymentProfile.Development
            ? config.GetValue<string>("MarketDataRecovery:Stage3:DevelopmentWorkerAssemblyPath")
            : null)
            ?? typeof(DatasetWorkerAssemblyMarker).Assembly.Location)),
            DeploymentProfile = deploymentProfile,
            DataSource = feedOptions.DataSource,
            OptionPricingRefresh = runtimeOptions.OptionPricingRefresh,
            Synthetic = feedOptions.Synthetic
        });
        services.AddSingleton<IDatabentoWatchdogPublisher, DatabentoWatchdogStatusConsolePublisher>();
        services.AddSingleton<ICurrentFuturesContractCatalog>(provider =>
        provider.GetRequiredService<ISecuritiesDbContext>());
        services.AddSingleton<IDatabentoContractAuthority, DatabentoContractAuthority>();
        if (stage3Options.Enabled)
        services.AddSingleton<IDatabentoLifecycleRuntime, SupervisedDatabentoLifecycleRuntime>();
        else
        services.AddSingleton<IDatabentoLifecycleRuntime, DatabentoLifecycleRuntime>();
        services.AddSingleton<DatabentoMarketDataWatchdogService>();
        services.AddSingleton<IMarketDataLifecycleRequests>(provider =>
        provider.GetRequiredService<DatabentoMarketDataWatchdogService>());
        services.AddHostedService(provider =>
        provider.GetRequiredService<DatabentoMarketDataWatchdogService>());
        if (stage3Options.Enabled)
        services.AddHostedService<RealtimePublicationRecoveryService>();
        var historicalOptions = new DatabentoHistoricalOptions
        {
            StagingRoot = Path.Combine(AppContext.BaseDirectory, "market-data-history"),
            SeriesProfiles = CreateHistoricalSeriesProfiles(dataset)
        };
        services.AddDatabentoHistoricalMarketDataServices(new DatabentoHistoricalProviderOptions
        {
            UseSyntheticProvider = feedOptions.DataSource == FeedDataSourceMode.Synthetic
        });
        services.AddApplicationMarketDataHistoricalApi(historicalOptions);
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
        services.AddSingleton<DatasetWorkerAdmissionRegistry>();
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.IndividualOptionRiskReader>();
        services.AddSingleton<IRealtimeSourceAdmission>(provider =>
        provider.GetRequiredService<DatasetWorkerAdmissionRegistry>());
        services.AddSingleton<DatasetPublicationIngress>();
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.IOptionTradeEvidenceWriter>(provider =>
        provider.GetRequiredService<IMarketDataDbContext>());
        services.AddSingleton<DatasetWorkerProcessRecoveryService>();
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.ICompositionMarketDataApi>(provider =>
        provider.GetRequiredService<DatasetWorkerProcessRecoveryService>());
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.ICompositionPreparationStore>(provider =>
        provider.GetRequiredService<IMarketDataDbContext>());
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.CompositionPreparationService>();
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.QualifiedCompositionDiscovery>();
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.CompositionMarketPreparation>();
        TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCacheServiceCollectionExtensions
        .AddStrategyOptionChainCache<TomasAI.IFM.Domain.MarketData.OptionChainCache.StoredOptionUniverseSource>(services);
        services.AddHostedService<DevelopmentOptionChainParameterSeeder>();
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Subscriptions.DurableCompositionRuntime>();
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Subscriptions.IDurableCompositionReconciler>(provider =>
        provider.GetRequiredService<TomasAI.IFM.Application.MarketData.Subscriptions.DurableCompositionRuntime>());
        services.AddSingleton<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.CompositionDiscoveryHandoff>();
        services.AddHostedService(provider => provider.GetRequiredService<TomasAI.IFM.Application.MarketData.Subscriptions.DurableCompositionRuntime>());
        services.AddSingleton<IDatabentoDatasetProcessRecovery>(provider =>
        provider.GetRequiredService<DatasetWorkerProcessRecoveryService>());
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
        services.AddHostedService<MarketDataOperationsHealthObserver>();
        services.AddSingleton<LivePipelineEvidence>();
        services.AddSingleton<FuturesItiSignalRuntimeTelemetry>();
        services.AddSingleton<MarketDataRuntimeHealthCheck>();
        services.AddSingleton<ActorRuntimeHealthCheck>();
        services.AddSingleton((config.GetSection("MarketDataRecovery:LivePipeline")
        .Get<LivePipelineMonitorOptions>() ?? new LivePipelineMonitorOptions()).Validate()); services.AddSingleton<ILivePipelineProbe, LivePipelineProbe>();
        services.AddSingleton<LivePipelineMonitor>();
        services.AddHostedService(provider => provider.GetRequiredService<LivePipelineMonitor>());
        services.AddHostedService<HistoricalDailyAnalyticsInitializationService>();
        services.AddHostedService<FuturesRolloverPreparationHostedService>();
        services.AddSingleton((config.GetSection("AppSettings:Databento:OptionExpiryCalendar")
        .Get<OptionContractExpiryCalendarOptions>() ?? new()).Validate());
        services.AddSingleton<OptionContractExpiryCalendarRefreshService>();
        services.AddHostedService<OptionContractExpiryCalendarStartupService>();
        services.AddHostedService<ApplicationStartupCommandDispatcher>();

    }
}
