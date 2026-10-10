using TomasAI.IFM.Application.Api.Server.Core.Trading.Emulation;
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
    static void RegisterTradeBrokerEmulator(SimpleInjector.Container container, ConfigurationManager config, Microsoft.Extensions.Logging.ILogger logger)
    {
        var accountAlias = config["TradeBroker:Emulator:AccountAlias"] ?? "IFM-EMULATOR-PAPER";
        var startingCash = config.GetValue<decimal?>("TradeBroker:Emulator:StartingCash") ?? 1_000_000m;
        var scenario = new TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.EmulatorScenario(
        accountAlias, "USD", startingCash, 0.65m, TimeSpan.FromSeconds(2));
        container.RegisterInstance(scenario);
        var ledgerPath = config["TradeBroker:Emulator:LedgerPath"]
        ?? Path.Combine(AppContext.BaseDirectory, "data", "trade-broker", "emulator-ledger.json");
        container.RegisterInstance<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.IEmulatorLedgerStore>(
        new TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.FileEmulatorLedgerStore(ledgerPath));
        container.RegisterSingleton<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.IEmulatorClock,
        TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.SystemEmulatorClock>();
        container.RegisterSingleton<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.EmulatorLedger>(() =>
        new TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.EmulatorLedger(
        container.GetInstance<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.EmulatorScenario>(),
        container.GetInstance<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.IEmulatorClock>(),
        container.GetInstance<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.IEmulatorLedgerStore>()));
        container.RegisterSingleton<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.OfflineFillSimulation>(() =>
        new(container.GetInstance<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.EmulatorLedger>(),
        new() {
            Enabled = config.GetValue("TradeBroker:Emulator:OfflineSimulation:Enabled", true),
            CompletionTime = TimeSpan.FromSeconds(config.GetValue("TradeBroker:Emulator:OfflineSimulation:CompletionSeconds", 30)),
            MaximumUnitsPerFill = config.GetValue("TradeBroker:Emulator:OfflineSimulation:MaximumUnitsPerFill", 3),
            RandomSeed = config.GetValue("TradeBroker:Emulator:OfflineSimulation:RandomSeed", 1)
        }, () => !container.GetInstance<IFuturesMarketSessionAuthority>().Current.IsMarketOpen, logger));
        container.RegisterSingleton<TomasAI.IFM.Framework.TradeBroker.Contracts.IFrameworkOrderExecutionBroker>(() =>
        new FrozenEmulatorOrderExecutionBroker(
        new TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.OrderExecution.EmulatedOrderExecutionBroker(
        container.GetInstance<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.EmulatorLedger>()),
        container.GetInstance<IFuturesMarketSessionAuthority>(),
        container.GetInstance<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.IEmulatorClock>(),
        container.GetInstance<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.OfflineFillSimulation>()));
        container.RegisterSingleton<TomasAI.IFM.Framework.TradeBroker.Contracts.IFrameworkBrokerAccount,
        TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.BrokerAccount.EmulatedBrokerAccount>();
        container.RegisterSingleton<TomasAI.IFM.Application.TradeBroker.Contracts.ITradeBroker>(() =>
        new TomasAI.IFM.Application.TradeBroker.InteractiveBrokersEmulatorTradeBroker(
        container.GetInstance<TomasAI.IFM.Framework.TradeBroker.Contracts.IFrameworkOrderExecutionBroker>(),
        container.GetInstance<TomasAI.IFM.Framework.TradeBroker.Contracts.IFrameworkBrokerAccount>(), logger));
        container.RegisterSingleton<TomasAI.IFM.Domain.Trade.Order.Broker.Realtime.BrokerOrderObservationBridge>();
        container.RegisterSingleton<TomasAI.IFM.Domain.Trade.Order.Broker.Query.Model.IBrokerOrderReadStore,
        TomasAI.IFM.Domain.Trade.Order.Broker.Query.Model.BrokerOrderReadStore>();
        container.RegisterSingleton<TomasAI.IFM.Domain.BrokerAccount.Query.Model.BrokerAccountReadStore>();
        container.RegisterSingleton<TomasAI.IFM.Domain.BrokerAccount.Query.Model.IBrokerAccountProjectionWriter>(
        () => container.GetInstance<TomasAI.IFM.Domain.BrokerAccount.Query.Model.BrokerAccountReadStore>());
        container.RegisterSingleton<TomasAI.IFM.Domain.BrokerAccount.Query.Model.IBrokerAccountReadStore>(
        () => container.GetInstance<TomasAI.IFM.Domain.BrokerAccount.Query.Model.BrokerAccountReadStore>());
        container.RegisterSingleton<TomasAI.IFM.Domain.BrokerAccount.Realtime.BrokerAccountObservationBridge>();
    }
}
