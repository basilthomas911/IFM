using TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;
using TomasAI.IFM.Application.Api.Server.Core.Messaging.JetStream;
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
    static void RegisterGenericTypes(
    Container siContainer,
    ConfigurationManager config,
    Microsoft.Extensions.Logging.ILogger logger)
    {
        logger.LogInformationEvent("ApiServer","{Component}.{Method} "+"register open generic handlers...",nameof(CoreServiceRegistration),nameof(RegisterGenericTypes));
        siContainer.RegisterSingleton<IDataCacheService, DataCacheService>();
        siContainer.RegisterInstance<TimeProvider>(TimeProvider.System);
        siContainer.RegisterInstance<TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts.IScheduledTaskOutputReader>(
        new TomasAI.IFM.Application.Storage.ScheduledTaskDb.ScheduledTaskOutputReader(
        config["ScheduledTasks:OutputRoot"] ?? Path.Combine(Environment.GetEnvironmentVariable("IFM_REPOSITORY_ROOT") ?? Directory.GetCurrentDirectory(), ".artifacts", "scheduled-tasks", "development", "TaskRuns")));
        siContainer.RegisterInstance<TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels.IDatabaseBackupOutputReader>(
        new TomasAI.IFM.Application.Storage.DatabaseBackupOutputReader(
        config["DatabaseBackup:OutputRoot"] ?? Path.Combine(Environment.GetEnvironmentVariable("IFM_REPOSITORY_ROOT") ?? Directory.GetCurrentDirectory(), ".artifacts", "database-backup", "output")));
        siContainer.RegisterSingleton<TomasAI.IFM.Application.Storage.ScheduledTaskDb.ScheduledTaskReadStore>();
        siContainer.RegisterSingleton<TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts.IScheduledTaskReadStore>(
        () => siContainer.GetInstance<TomasAI.IFM.Application.Storage.ScheduledTaskDb.ScheduledTaskReadStore>());
        siContainer.RegisterSingleton<TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts.IScheduledTaskProjectionWriter>(
        () => siContainer.GetInstance<TomasAI.IFM.Application.Storage.ScheduledTaskDb.ScheduledTaskReadStore>());
        RegisterTradeBrokerEmulator(siContainer, config, logger);
        siContainer.RegisterSingleton<IDatabaseBackupExecutionOutbox, DatabaseBackupExecutionOutbox>();
        var projectorReliabilityOptions = config
        .GetSection(EventProjectorReliabilityOptions.SectionName)
        .Get<EventProjectorReliabilityOptions>() ?? new EventProjectorReliabilityOptions();
        siContainer.RegisterInstance((projectorReliabilityOptions with
        {
            DurableProjectorAllowlist = FinancialJetStreamPolicy.DurableProjectors
        }).Validate());
        var eventLogPersistenceOptions = config
        .GetSection(TomasAI.IFM.Application.Storage.EventSourceDb.Persistence.EventLogPersistenceOptions.SectionName)
        .Get<TomasAI.IFM.Application.Storage.EventSourceDb.Persistence.EventLogPersistenceOptions>()
        ?? new TomasAI.IFM.Application.Storage.EventSourceDb.Persistence.EventLogPersistenceOptions();
        siContainer.RegisterInstance(eventLogPersistenceOptions.Validate());
        var commandAuditPersistenceOptions = config
        .GetSection(TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditPersistenceOptions.SectionName)
        .Get<TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditPersistenceOptions>()
        ?? new TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditPersistenceOptions();
        siContainer.RegisterInstance(commandAuditPersistenceOptions.Validate());
        var inMemoryEventSourceActorOptions = config
        .GetSection(InMemoryEventSourceActorOptions.SectionName)
        .Get<InMemoryEventSourceActorOptions>() ?? new InMemoryEventSourceActorOptions();
        siContainer.RegisterInstance(inMemoryEventSourceActorOptions.Validate());

        var domainAssemblies = new List<Assembly>
        {
            ApplicationActorAssembly.Current,
            DomainApplicationActorAssembly.Current,
            PortfolioActorAssembly.Current,
            MarketDataActorAssembly.Current,
            MarketDataAnalyticsActorAssembly.Current,
            MarketDataFeedActorAssembly.Current,
            OptionPricerActorAssembly.Current,
            ReferenceActorAssembly.Current,
            SecuritiesActorAssembly.Current,
            SystemAdminActorAssembly.Current,
            TradeActorAssembly.Current,
            TomasAI.IFM.Domain.Supervisor.SupervisorActorAssembly.Current,
            TomasAI.IFM.Domain.BrokerAccount.BrokerAccountActorAssembly.Current
        };
        var selectedDomain = config["IFM_TEST_ACTOR_DOMAIN"];
        var actorAssemblies = string.IsNullOrWhiteSpace(selectedDomain)
        ? domainAssemblies
        : domainAssemblies
        .Where(assembly =>
        assembly == TomasAI.IFM.Domain.Supervisor.SupervisorActorAssembly.Current
        || selectedDomain
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Contains(assembly.GetName().Name, StringComparer.Ordinal))
        .ToList();
        if (actorAssemblies.Count == 0)
        throw new InvalidOperationException($"No actor assembly matched IFM_TEST_ACTOR_DOMAIN '{selectedDomain}'.");

        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
        .Where(static assembly => assembly.GetName().Name is { } assemblyName
        && !assemblyName.EndsWith("Tests", StringComparison.Ordinal)
        && !assemblyName.EndsWith("Benchmarks", StringComparison.Ordinal))
        .ToList();
        assemblies.AddRange(domainAssemblies);
        assemblies = assemblies.Distinct().ToList();
        var repositoryTypes = ObjectRepositoryDiscovery.Discover(assemblies)
        .Where(static type => type != typeof(TomasAI.IFM.Application.Storage.ScheduledTaskDb.ScheduledTaskReadStore)
        && type != typeof(SystemAdminDbContext)
        && type != typeof(EventSourceActorDbContext)
        && type != typeof(TomasAI.IFM.Domain.BrokerAccount.Query.Model.BrokerAccountReadStore))
        .ToArray();
        siContainer.Register(typeof(IObjectRepository<>), repositoryTypes, Lifestyle.Transient);
        // The repository contract must use the same singleton as the account reader and projector.
        siContainer.AddRegistration<IObjectRepository<TomasAI.IFM.Domain.BrokerAccount.Query.Model.BrokerAccountReadStore>>(
        siContainer.GetCurrentRegistrations().Single(registration => registration.ServiceType ==
        typeof(TomasAI.IFM.Domain.BrokerAccount.Query.Model.BrokerAccountReadStore)).Registration);
        siContainer.AddRegistration<IObjectRepository<TomasAI.IFM.Application.Storage.ScheduledTaskDb.ScheduledTaskReadStore>>(
        siContainer.GetCurrentRegistrations().Single(registration => registration.ServiceType ==
        typeof(TomasAI.IFM.Application.Storage.ScheduledTaskDb.ScheduledTaskReadStore)).Registration);
        var eventSourceRegistration = EventLogQualification.Active is null
        ? Lifestyle.Singleton.CreateRegistration<EventSourceActorDbContext>(siContainer)
        : Lifestyle.Singleton.CreateRegistration(() => new EventSourceActorDbContext(
        siContainer.GetInstance<IDbConnectionSettings>(),
        siContainer.GetInstance<IDbContextFactory>(),
        siContainer.GetInstance<IBlackboardService>(),
        siContainer.GetInstance<Microsoft.Extensions.Logging.ILogger<DbProvider>>(),
        eventLogPersistenceOptions, commandAuditPersistenceOptions, true), siContainer);
        siContainer.AddRegistration<IObjectRepository<EventSourceActorDbContext>>(eventSourceRegistration);
        var systemAdminRegistration = Lifestyle.Singleton.CreateRegistration<SystemAdminDbContext>(siContainer);
        siContainer.AddRegistration<ISystemAdminDbContext>(systemAdminRegistration);
        siContainer.AddRegistration<IObjectRepository<SystemAdminDbContext>>(systemAdminRegistration);
        siContainer.Register(typeof(IActor<>), actorAssemblies, Lifestyle.Singleton);
        siContainer.Register(typeof(ICommandActorContext<>), domainAssemblies, Lifestyle.Singleton);
        siContainer.Register(typeof(IFunctionActorContext<>), domainAssemblies, Lifestyle.Singleton);
        // Both context contracts share the same singleton registration.
        siContainer.AddRegistration<TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.Actor.ICapacityReservationFunctionContext>(
        siContainer.GetCurrentRegistrations().Single(registration => registration.ServiceType ==
        typeof(IFunctionActorContext<TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.Actor.CapacityReservationFunctionActor>)).Registration);
        siContainer.AddRegistration<TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.Actor.ICapacityConsumptionFunctionContext>(
        siContainer.GetCurrentRegistrations().Single(registration => registration.ServiceType ==
        typeof(IFunctionActorContext<TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.Actor.CapacityConsumptionFunctionActor>)).Registration);
        siContainer.AddRegistration<TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.Actor.IPortfolioOrderCompositionFunctionContext>(
        siContainer.GetCurrentRegistrations().Single(registration => registration.ServiceType ==
        typeof(IFunctionActorContext<TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.Actor.PortfolioOrderCompositionFunctionActor>)).Registration);
        siContainer.AddRegistration<TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.Actor.IPortfolioCloseOrderCompositionFunctionContext>(
        siContainer.GetCurrentRegistrations().Single(registration => registration.ServiceType ==
        typeof(IFunctionActorContext<TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.Actor.PortfolioCloseOrderCompositionFunctionActor>)).Registration);
        siContainer.AddRegistration<IRegimeDiscoveryFunctionContext>(
        siContainer.GetCurrentRegistrations().Single(registration =>
        registration.ServiceType == typeof(IFunctionActorContext<RegimeDiscoveryFunctionActor>)).Registration);
        siContainer.AddRegistration<IMarketConditionFunctionContext>(
        siContainer.GetCurrentRegistrations().Single(registration =>
        registration.ServiceType == typeof(IFunctionActorContext<MarketConditionFunctionActor>)).Registration);
        siContainer.AddRegistration<ITradeSelectionFunctionContext>(
        siContainer.GetCurrentRegistrations().Single(registration =>
        registration.ServiceType == typeof(IFunctionActorContext<TradeSelectionFunctionActor>)).Registration);
        siContainer.AddRegistration<IOrderCompositionFunctionContext>(
        siContainer.GetCurrentRegistrations().Single(registration =>
        registration.ServiceType == typeof(IFunctionActorContext<OrderCompositionFunctionActor>)).Registration);
        siContainer.AddRegistration<IRiskManagementFunctionContext>(
        siContainer.GetCurrentRegistrations().Single(registration =>
        registration.ServiceType == typeof(IFunctionActorContext<RiskManagementFunctionActor>)).Registration);
        siContainer.AddRegistration<TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.Actor.IIronCondorTradePlanFunctionContext>(
        siContainer.GetCurrentRegistrations().Single(registration => registration.ServiceType ==
        typeof(IFunctionActorContext<TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.Actor.IronCondorTradePlanFunctionActor>)).Registration);
        siContainer.AddRegistration<TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.Actor.IVerticalSpreadTradePlanFunctionContext>(
        siContainer.GetCurrentRegistrations().Single(registration => registration.ServiceType ==
        typeof(IFunctionActorContext<TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.Actor.VerticalSpreadTradePlanFunctionActor>)).Registration);
        siContainer.AddRegistration<TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.Actor.IFuturesTradePlanFunctionContext>(
        siContainer.GetCurrentRegistrations().Single(registration => registration.ServiceType ==
        typeof(IFunctionActorContext<TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.Actor.FuturesTradePlanFunctionActor>)).Registration);
        siContainer.Register(typeof(IEventActorContext<>), domainAssemblies, Lifestyle.Singleton);
        siContainer.Register(typeof(IQueryActorContext<>), domainAssemblies, Lifestyle.Singleton);
        siContainer.Register(typeof(IRealtimeActorContext<>), domainAssemblies, Lifestyle.Singleton);
        siContainer.Register(typeof(IActorStateDenormalizer<>), assemblies, Lifestyle.Singleton);
        siContainer.Register(typeof(IEventSourceActorStateRepository<>), assemblies, Lifestyle.Singleton);
        siContainer.Register(typeof(IResidentEventSourceActorStateRepository<>), assemblies, Lifestyle.Singleton);
        siContainer.Register(typeof(IEventSourceFunctionStateRepository<,>), assemblies, Lifestyle.Singleton);
        if (siContainer.GetRegistration<IEventSourceFunctionStateRepository<
        TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.State.PortfolioOrderCompositionFunctionState,
        TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.EvaluatePortfolioOrderCompositionCommand>>(false) is null)
        siContainer.Register<IEventSourceFunctionStateRepository<
        TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.State.PortfolioOrderCompositionFunctionState,
        TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.EvaluatePortfolioOrderCompositionCommand>,
        TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.State.PortfolioOrderCompositionFunctionStateRepository>(Lifestyle.Singleton);
        siContainer.Register(typeof(IFunctionProjector<>), domainAssemblies, Lifestyle.Singleton);
        siContainer.Register(typeof(IEventProjector<>), domainAssemblies, Lifestyle.Singleton);
        siContainer.Register<TomasAI.IFM.Domain.Portfolio.CapacityReservation.Emulator.Command.EmulatorExecutionCommandServices>(Lifestyle.Singleton);
        siContainer.Register<TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command.GeneralLedgerCommandServices>(Lifestyle.Singleton);
        siContainer.Register<TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command.LedgerConfigurationCommandServices>(Lifestyle.Singleton);
        siContainer.Register<TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query.FinancialBookPreparation>(Lifestyle.Singleton);
        siContainer.Register<TomasAI.IFM.Domain.Portfolio.GeneralLedger.IPortfolioTradeAccountingApi,
        TomasAI.IFM.Domain.Portfolio.GeneralLedger.BrokerExecutionAccountingApi>(Lifestyle.Singleton);
        siContainer.Register<TomasAI.IFM.Domain.Portfolio.GeneralLedger.IPortfolioTradeValuationApi,
        TomasAI.IFM.Domain.Portfolio.GeneralLedger.PortfolioTradeValuationApi>(Lifestyle.Singleton);
        siContainer.Register<TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query.FinancialAuthorityPreparation>(Lifestyle.Singleton);
        siContainer.Register<TomasAI.IFM.Domain.Portfolio.CapacityReservation.Command.CapacityReservationCommandServices>(Lifestyle.Singleton);
        siContainer.Register(
        typeof(TomasAI.IFM.Application.EventProjector.Realtime.Contracts.IRealtimeProjector<>),
        domainAssemblies,
        Lifestyle.Singleton);
        siContainer.Register(typeof(IEventSourceActorState<>), assemblies, Lifestyle.Transient);
        logger.LogInformationEvent("ApiServer","{Component}.{Method} "+"open generic handlers registered",nameof(CoreServiceRegistration),nameof(RegisterGenericTypes));
    }
}
