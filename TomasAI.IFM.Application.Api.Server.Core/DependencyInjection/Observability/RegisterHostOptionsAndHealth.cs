using TomasAI.IFM.Application.Api.Server.Core.Deployment.Identity;
using TomasAI.IFM.Application.Api.Server.Core.Deployment.Validation;
using TomasAI.IFM.Application.Api.Server.Core.Development.Verification;
using TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;
using TomasAI.IFM.Application.Api.Server.Core.MarketData.Treasury;
using TomasAI.IFM.Application.Api.Server.Core.Observability.HealthChecks;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Actors;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Readiness;
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
    static void RegisterHostOptionsAndHealth(IServiceCollection services, ConfigurationManager config, Microsoft.Extensions.Logging.ILogger logger, IHostEnvironment? hostEnvironment, Container siContainer, bool focusedActorIntegration)
    {
        // add web app services...
        logger.LogInformationEvent("ApiServer","{Component}.{Method} "+"register base services...",nameof(CoreServiceRegistration),nameof(RegisterBaseServices));
        services.Configure<HostOptions>(options =>
        options.BackgroundServiceExceptionBehavior =
        BackgroundServiceExceptionBehavior.Ignore);
        services.AddIfmMetrics(config, "TomasAI.IFM.Application.Api.Server");
        var portfolioOperations = config.GetSection(PortfolioOperationalOptions.SectionName)
        .Get<PortfolioOperationalOptions>() ?? new PortfolioOperationalOptions();
        services.AddSingleton(portfolioOperations.Validate());
        services.AddSingleton<IPortfolioOperationalGuard, PortfolioOperationalGuard>();
        var applicationStartup = config.GetSection(ApplicationStartupOptions.SectionName)
        .Get<ApplicationStartupOptions>() ?? new ApplicationStartupOptions();
        services.AddSingleton(applicationStartup.Validate());
        var actorRuntimeStartup = config.GetSection(ActorRuntimeStartupOptions.SectionName)
        .Get<ActorRuntimeStartupOptions>() ?? new ActorRuntimeStartupOptions();
        services.AddSingleton(actorRuntimeStartup.Validate());
        var startupOrchestration = config.GetSection(StartupOrchestrationOptions.SectionName)
        .Get<StartupOrchestrationOptions>() ?? new StartupOrchestrationOptions();
        services.AddSingleton(startupOrchestration.Validate());
        var eventLogPersistence = config
        .GetSection(TomasAI.IFM.Application.Storage.EventSourceDb.Persistence.EventLogPersistenceOptions.SectionName)
        .Get<TomasAI.IFM.Application.Storage.EventSourceDb.Persistence.EventLogPersistenceOptions>()
        ?? new TomasAI.IFM.Application.Storage.EventSourceDb.Persistence.EventLogPersistenceOptions();
        services.AddSingleton(eventLogPersistence.Validate());
        var commandAuditPersistence = config
        .GetSection(TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditPersistenceOptions.SectionName)
        .Get<TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditPersistenceOptions>()
        ?? new TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditPersistenceOptions();
        services.AddSingleton(commandAuditPersistence.Validate());
        var inMemoryEventSourceActor = config
        .GetSection(InMemoryEventSourceActorOptions.SectionName)
        .Get<InMemoryEventSourceActorOptions>() ?? new InMemoryEventSourceActorOptions();
        services.AddSingleton(inMemoryEventSourceActor.Validate());
        services.AddSingleton<IApplicationStartupStatusStore, ApplicationStartupStatusStore>();
        services.AddSingleton<IApplicationStartupHandoffStatusStore, ApplicationStartupHandoffStatusStore>();
        services.AddSingleton<IApplicationStartupActivities, ApiApplicationStartupActivities>();
        services.AddSingleton<IApplicationBootstrapReadiness, ApplicationBootstrapReadiness>();
        services.AddSingleton<ActorRuntimeStartupSignal>();
        services.AddSingleton<IActorRuntimeStartupSignal>(provider =>
        provider.GetRequiredService<ActorRuntimeStartupSignal>());
        var deploymentIdentity = config.GetSection(DeploymentIdentityOptions.SectionName)
        .Get<DeploymentIdentityOptions>() ?? new DeploymentIdentityOptions();
        services.AddSingleton(deploymentIdentity);
        services.AddSingleton<DeploymentIdentityMonitor>();
        if (!focusedActorIntegration)
        services.AddHostedService<DeploymentIdentityEnforcementService>();
        var healthChecks = services.AddHealthChecks()
        .AddCheck<ProcessLivenessHealthCheck>("process_liveness", tags: ["live"])
        .AddCheck<DeploymentIdentityHealthCheck>("deployment_identity", tags: ["bootstrap", "launch", "ready"])
        .AddCheck<PortfolioOperationalHealthCheck>("portfolio_operations", tags: ["bootstrap", "launch", "ready"]);
        healthChecks
        .AddCheck<ActorRuntimeHealthCheck>("actor_runtime", tags: ["actor", "bootstrap", "launch", "ready"])
        .AddCheck<FmpConfigurationHealthCheck>("fmp_configuration", tags: ["application", "ready"])
        .AddCheck<LivePipelineHealthCheck>("live_pipeline", tags: ["application", "ready"])
        .AddCheck<MarketDataRuntimeHealthCheck>("market_data_runtime", tags: ["application", "ready"])
        .AddCheck<ApplicationLifecycleHealthCheck>("application_lifecycle", tags: ["application", "launch", "ready"]);
        var fmpEnabled = config.GetValue("AppSettings:Fmp:Enabled", true);
        services.AddFinancialModelingPrepMarketData(options =>
        {
            options.Enabled = fmpEnabled;
            options.LatestTreasuryLookbackDays = config.GetValue("AppSettings:Fmp:LatestTreasuryLookbackDays", 14);
            options.MaximumProviderWindowDays = config.GetValue("AppSettings:Fmp:MaximumProviderWindowDays", 90);
            options.MaximumRequestRangeDays = config.GetValue("AppSettings:Fmp:MaximumRequestRangeDays", 3_660);
            options.MaximumConcurrentRequests = config.GetValue("AppSettings:Fmp:MaximumConcurrentRequests", 2);
        });
        services.AddFinancialModelingPrepReferenceDataApi();
        // FMP continues to own the economic calendar. Pricing and yield-curve imports use the official feed.
        services.AddHttpClient("USTreasury", client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton<TomasAI.IFM.Framework.MarketData.ReferenceData.UsTreasuryCurve>(sp => new(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("USTreasury"), sp.GetRequiredService<TimeProvider>()));
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.Replace(services,
        ServiceDescriptor.Singleton<TomasAI.IFM.Framework.MarketData.Contracts.ITreasuryCurve>(
        sp => sp.GetRequiredService<TomasAI.IFM.Framework.MarketData.ReferenceData.UsTreasuryCurve>()));
        services.AddSingleton(TomasAI.IFM.Framework.MarketData.ReferenceData.UsTreasuryCurve.ConversionPolicy);
        services.AddSingleton(TomasAI.IFM.Application.MarketData.Pricing.UsTreasuryPublicationCalendar.Default2026);
        if (EventLogQualification.Active is null && !focusedActorIntegration)
        services.AddHostedService<UsTreasuryRefreshHostedService>();
        services.AddFmpMarketDataImport(options =>
        options.MaximumRangeDays = config.GetValue("AppSettings:Fmp:MaximumImportRangeDays", 366));
        services.AddSingleton(new MarketDataImportPolicyOptions
        {
            Treasury = ParseImportPolicy(config, "AppSettings:Fmp:TreasuryDuplicatePolicy"),
            EconomicCalendar = ParseImportPolicy(config, "AppSettings:Fmp:EconomicCalendarDuplicatePolicy")
        }.Validate());
        services.AddOpenApiDocument();


    }
}
