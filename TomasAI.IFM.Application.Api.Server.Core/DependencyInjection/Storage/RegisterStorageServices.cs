using TomasAI.IFM.Application.Api.Server.Core.Startup.ParameterSets;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Schema;
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
    static void RegisterStorageServices(IServiceCollection services, ConfigurationManager config, Microsoft.Extensions.Logging.ILogger logger, IHostEnvironment? hostEnvironment, Container siContainer, bool focusedActorIntegration)
    {
        logger.LogInformationEvent("ApiServer","{Component}.{Method} "+"register storage services...",nameof(CoreServiceRegistration),nameof(RegisterStorageServices));
        services.AddSingleton(_ =>
        {
            var isolatedPostgres = config["IFM_TEST_POSTGRES_CONNECTION"];
            return new DbConnectionSettings()
            .Add("EventSourceActorDbConnection", isolatedPostgres ?? config.GetConnectionString("EventSourceActorDbConnection")!, "System.Data.Postgres")
            .Add("ConfigurationDbConnection", isolatedPostgres ?? config.GetConnectionString("ConfigurationDbConnection")
            ?? config.GetConnectionString("EventSourceActorDbConnection")!, "System.Data.Postgres")
            .Add("MarketDataServiceDbConnection", isolatedPostgres ?? config.GetConnectionString("MarketDataServiceDbConnection")
            ?? config.GetConnectionString("EventSourceActorDbConnection")!, "System.Data.Postgres")
            .Add("SystemAdminDbConnection", isolatedPostgres ?? config.GetConnectionString("SystemAdminDbConnection")
            ?? config.GetConnectionString("EventSourceActorDbConnection")!, "System.Data.Postgres")
            .Add("SequenceIdDbConnection", isolatedPostgres ?? config.GetConnectionString("SequenceIdDbConnection")!, "System.Data.Postgres")
            .Add("PortfolioDbConnection", isolatedPostgres ?? config.GetConnectionString("PortfolioDbConnection")
            ?? config.GetConnectionString("EventSourceActorDbConnection")!, "System.Data.Postgres")
            .Add("MarketDataDbConnection", config.GetConnectionString("MarketDataDbConnection")!, "System.Data.ScyllaDb")
            .Add("OptionPricerDbConnection", config.GetConnectionString("OptionPricerDbConnection")!, "System.Data.ScyllaDb")
            .Add("ReferenceDbConnection", config.GetConnectionString("ReferenceDbConnection")!, "System.Data.ScyllaDb")
            .Add("SecuritiesDbConnection", config.GetConnectionString("SecuritiesDbConnection")!, "System.Data.ScyllaDb")
            .Add("TradeDbConnection", config.GetConnectionString("TradeDbConnection")!, "System.Data.ScyllaDb")
            .Add("TradePlanDbConnection", config.GetConnectionString("TradePlanDbConnection")
            ?? throw new InvalidOperationException("TradePlanDbConnection must target the Trade Plan ScyllaDB keyspace."), "System.Data.ScyllaDb");
        });
        services.AddSingleton<IDbCache, DbCache>();
        services.AddSingleton<IDbContextResolver>(_ => new DbContextResolver(e => GetContainerInstance(siContainer, e)!));
        services.AddSingleton<IDbContextFactory, DbContextFactory>();
        services.AddSingleton<ISequenceIdDbContext, SequenceIdDbContext>();
        services.AddSingleton<ISequenceIdGenerator, PostgresSequenceIdGenerator>();
        services.AddSingleton<IPortfolioBusinessIdAllocator, PortfolioBusinessIdAllocator>();
        services.AddSingleton<TomasAI.IFM.Application.Storage.EventSourceDb.IPostgresEventTransaction,
        TomasAI.IFM.Application.Storage.EventSourceDb.PostgresEventTransaction>();
        services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioDb.OrderComposition.PortfolioOrderCompositionStore>();
        services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioDb.OrderComposition.PortfolioCloseOrderCompositionStore>();
        services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.PortfolioFinancialSchema>();
        services.AddSingleton(provider => new TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialDevelopmentPolicy(
        provider.GetRequiredService<IHostEnvironment>().IsDevelopment()));
        services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.IGeneralLedgerStore,
        TomasAI.IFM.Application.Storage.PortfolioFinancial.GeneralLedgerStore>();
        services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.ICapacityReservationStore,
        TomasAI.IFM.Application.Storage.PortfolioFinancial.CapacityReservationStore>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model.FinancialIdentityAllocator>();
        services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.FinancialAuthorityPreparationStore>();
        services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.IFinancialHistoryProjection,
        TomasAI.IFM.Application.Storage.PortfolioFinancial.FinancialHistoryProjection>();
        services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.IFinancialQueryStore,
        TomasAI.IFM.Application.Storage.PortfolioFinancial.FinancialQueryStore>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Shared.Financial.IPortfolioFinancialApi>(provider =>
        new TomasAI.IFM.Application.Api.Nats.Client.PortfolioFinancialApi(
        provider.GetRequiredService<IActorProducer>()));
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.IPortfolioOrderCompositionApi>(provider =>
        (TomasAI.IFM.Application.Api.Nats.Client.PortfolioFinancialApi)provider.GetRequiredService<
        TomasAI.IFM.Domain.Portfolio.Shared.Financial.IPortfolioFinancialApi>());
        services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.AccountingExportStore>();
        services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.EmulatorExecutionStore>();
        services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.IPortfolioAuthorityFence,
        TomasAI.IFM.Application.Storage.PortfolioFinancial.PortfolioAuthorityFence>();
        services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.FinancialHistoryJournal>();
        services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.ILedgerConfigurationStore,
        TomasAI.IFM.Application.Storage.PortfolioFinancial.LedgerConfigurationStore>();
        // Financial history recovery, workflow recovery, and capacity expiry polling are
        // temporarily disabled pending review of retry behavior and persisted-data compatibility.
        services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.FinancialWorkflowRecoveryJournal>();
        services.AddSingleton<TomasAI.IFM.Application.Storage.EventSourceDb.RiskHistoryJournal>();
        services.AddSingleton<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime.RiskObservationRecoveryService>();
        services.AddSingleton<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime.IWorkflowRiskProjection>(provider =>
        provider.GetRequiredService<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime.RiskObservationRecoveryService>());
        services.AddSingleton<TomasAI.IFM.Application.Storage.PortfolioFinancial.CapacityExpiryDispatchStore>();
        services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(siContainer, type)!).Resolve<EventSourceActorDbContext>() as IEventSourceActorDbContext)!);
        services.AddSingleton<IPortfolioEventStore>(provider => new PortfolioEventStore(provider.GetRequiredService<IEventSourceActorDbContext>(),
        provider.GetRequiredService<TomasAI.IFM.Application.Storage.PortfolioFinancial.IPortfolioAuthorityFence>()));
        services.AddSingleton<IPortfolioProjectionRebuilder>(provider =>
        new PortfolioProjectionRebuilder(
        provider.GetRequiredService<IPortfolioEventStore>(),
        provider.GetRequiredService<IPortfolioDbWriteContext>()));
        services.AddSingleton<ICommandAuditLogger>(provider =>
        (ICommandAuditLogger)provider.GetRequiredService<IEventSourceActorDbContext>());
        services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(siContainer, type)!).Resolve<SequenceIdDbContext>() as ISequenceIdDbContext)!);
        services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(siContainer, type)!).Resolve<PortfolioDbContext>() as PortfolioDbContext)!);
        services.AddSingleton<IPortfolioDbReadContext>(provider => provider.GetRequiredService<PortfolioDbContext>());
        services.AddSingleton<IPortfolioDbWriteContext>(provider => provider.GetRequiredService<PortfolioDbContext>());
        services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(siContainer, type)!).Resolve<MarketDataDbContext>() as IMarketDataDbContext)!);
        services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(siContainer, type)!).Resolve<OptionPricerDbContext>() as IOptionPricerDbContext)!);
        services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(siContainer, type)!).Resolve<ReferenceDbContext>() as IReferenceDbContext)!);
        services.AddSingleton<TradeStrategyFamilyBootstrapper>();
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Contracts.ITradeStrategySymbolStore, TradeStrategySymbolStore>();
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Contracts.IInstrumentDefinitionStore>(provider =>
        provider.GetRequiredService<IDbContextFactory>().ReferenceDb.InstrumentDefinitions);
        services.AddSingleton<TomasAI.IFM.Framework.MarketData.Contracts.Pricing.IOptionPricingConventionStore>(provider =>
        provider.GetRequiredService<IDbContextFactory>().SecuritiesDb);
        services.AddSingleton(provider => provider.GetRequiredService<IDbContextFactory>().ReferenceDb.OptionPricingReferenceBundles);
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.EuropeanOptionUniverse>();
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.TreasuryPricingProvider>();
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.IOptionPricingContextProvider,
        TomasAI.IFM.Application.MarketData.Pricing.OptionPricingContextProvider>();
        services.AddTradeStrategySymbolCatalog();
        services.AddSingleton<ITradeStrategyFamilyCatalogStore, TradeStrategyFamilyCatalogStore>();
        services.AddSingleton<TomasAI.IFM.Domain.Reference.TradeStrategyFamilies.TradeStrategyFamilyCreationService>();
        services.AddSingleton<TomasAI.IFM.Domain.Reference.StrategyCatalog.StrategyCatalogService>();
        services.AddSingleton(provider => new TomasAI.IFM.Application.Api.Server.Core.Startup.ParameterSets.RsiHistoricalPilotOptions(
        provider.GetRequiredService<IHostEnvironment>().IsDevelopment() && config.GetValue<bool>("ParameterSets:RsiHistoricalPilotEnabled")));
        services.AddSingleton<TomasAI.IFM.Application.Api.Server.Core.Startup.ParameterSets.IRsiHistoricalPilotStartup, TomasAI.IFM.Application.Api.Server.Core.Startup.ParameterSets.RsiHistoricalPilotStartup>();
        services.AddSingleton<TomasAI.IFM.Domain.Reference.Shared.ParameterSets.IParameterSetsApi, TomasAI.IFM.Application.Api.Nats.Client.ParameterSetsApi>();
        services.AddSingleton<TomasAI.IFM.Domain.Reference.Shared.ParameterSets.IParameterRuntimeSnapshot>(provider =>
        new TomasAI.IFM.Domain.Reference.ParameterSets.Model.ParameterRuntimeSnapshotModel(
        provider.GetRequiredService<IHostEnvironment>().IsDevelopment() && config.GetValue<bool>("ParameterSets:SingleUserDevelopmentEnabled")));
        services.AddSingleton<TomasAI.IFM.Domain.Reference.Shared.ParameterSets.IParameterAccessPolicy>(provider =>
        new TomasAI.IFM.Domain.Reference.ParameterSets.Model.SingleUserDevelopmentParameterAccessPolicy(
        provider.GetRequiredService<IHostEnvironment>().EnvironmentName,
        config.GetValue<bool>("ParameterSets:SingleUserDevelopmentEnabled")));
        services.AddSingleton<TomasAI.IFM.Domain.Reference.StrategyCatalog.StrategyCatalogMigration>();
        services.AddSingleton<TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.IStrategyCatalogReferences, TomasAI.IFM.Domain.Reference.StrategyCatalog.StrategyCatalogReferenceAdapter>();
        services.AddSingleton<TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.IStrategyCatalogCapabilities>(
        _ => new TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog.StrategyCatalogCapabilityRegistry(TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.TradeSelectionCatalogCapabilities.Create()
        .Concat(TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.CompositionCatalogCapabilities.Create())
        .Concat(TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model.RiskCatalogCapabilities.Create())));
        services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(siContainer, type)!).Resolve<SecuritiesDbContext>() as ISecuritiesDbContext)!);
        services.AddSingleton<IFuturesContractRolloverStore>(provider =>
        provider.GetRequiredService<ISecuritiesDbContext>());
        services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(siContainer, type)!).Resolve<TradeDbContext>() as ITradeDbContext)!);
        services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(siContainer, type)!).Resolve<ConfigurationDbContext>() as IConfigurationDbContext)!);
        services.AddSingleton(_ => (new DbContextResolver(type => GetContainerInstance(siContainer, type)!).Resolve<MarketDataServiceDbContext>() as MarketDataServiceDbContext)!);
        services.AddSingleton<IMarketDataServiceStore>(provider => provider.GetRequiredService<MarketDataServiceDbContext>());
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Subscriptions.Persistence.IDurableSubscriptionIntentStore>(provider =>
        provider.GetRequiredService<MarketDataServiceDbContext>());
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Subscriptions.DurableSubscriptionDelivery>();
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Subscriptions.Persistence.ICommittedBusinessEventJournal,
        TomasAI.IFM.Application.Storage.EventSourceDb.PostgresCommittedBusinessEventJournal>();
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Subscriptions.Persistence.ICommittedBusinessSubscriptionSource,
        TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.CommittedCompositionSubscriptionSource>();
        services.AddSingleton<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Realtime.CommittedCompositionSubscriptionProjector>();
        services.AddSingleton<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Realtime.ICommittedCompositionSubscriptionProjector>(provider =>
        provider.GetRequiredService<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Realtime.CommittedCompositionSubscriptionProjector>());
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.ICompositionRoutePlanStore>(provider =>
        provider.GetRequiredService<MarketDataServiceDbContext>());
        services.AddSingleton<IHistoricalDataLoaderStore, PostgresHistoricalDataLoaderStore>();
        services.AddSingleton<IHistoricalObservationStore>(provider =>
        provider.GetRequiredService<IMarketDataDbContext>());
        services.AddSingleton<EventSourceSchemaDb>();
        services.AddSingleton<SequenceIdSchemaDb>();
        services.AddSingleton<PortfolioSchemaDb>();
        services.AddSingleton<MarketDataSchemaDb>();
        services.AddSingleton<OptionPricerSchemaDb>();
        services.AddSingleton<ReferenceSchemaDb>();
        services.AddSingleton<SecuritiesSchemaDb>();
        services.AddSingleton<TradeSchemaDb>();
        services.AddSingleton<TomasAI.IFM.Application.Storage.ScheduledTaskDb.ScheduledTaskSchemaDb>();
        services.AddSingleton<TradePlanSchemaDb>();
        services.AddSingleton<SystemAdminSchemaDb>();
        services.AddSingleton<ConfigurationSchemaDb>();
        services.AddSingleton<MarketDataServiceSchemaDb>();
        services.AddSingleton<ApplicationSchemaInitializer>();
        services.AddSingleton<RegimeDiscoveryMarketSignalSnapshotProvider>();
        services.AddSingleton<IRegimeDiscoveryMarketSignalSnapshotProvider>(provider =>
        provider.GetRequiredService<RegimeDiscoveryMarketSignalSnapshotProvider>());
        services.AddSingleton<IRegimeDiscoveryMarketSignalCache>(provider =>
        provider.GetRequiredService<RegimeDiscoveryMarketSignalSnapshotProvider>());



        services.AddSingleton<IMarketConditionEventRiskAdapter, MarketConditionEventRiskAdapter>();
        services.AddSingleton<IMarketConditionAssessmentSnapshotProvider, MarketConditionAssessmentSnapshotProvider>();


        services.AddSingleton(_ =>
        new StorageUrlSettings()
        .Add("DomainData", config.GetValue<string>("AppSettings:DomainDataStorageBaseUri")!)
        .Add("QueryData", config.GetValue<string>("AppSettings:QueryDataStorageBaseUri")!)
        );
    }
}
