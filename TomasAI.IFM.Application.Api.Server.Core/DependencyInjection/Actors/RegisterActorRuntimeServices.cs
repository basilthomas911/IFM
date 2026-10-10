using TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery;
using TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;
using TomasAI.IFM.Application.Api.Server.Core.Observability.Logging;
using TomasAI.IFM.Application.Api.Server.Core.Recovery.Databento.Verification;
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
    static void RegisterActorRuntimeServices(IServiceCollection services, ConfigurationManager config, Microsoft.Extensions.Logging.ILogger logger, IHostEnvironment? hostEnvironment, Container siContainer, bool focusedActorIntegration)
    {
        // register Event Model Actor instances...
        var admissionOptions = config
        .GetSection(ActorAdmissionOptions.SectionName)
        .Get<ActorAdmissionOptions>() ?? new ActorAdmissionOptions();
        admissionOptions.Validate();
        var actorInformationLoggingPolicy = (config
        .GetSection(ActorInformationLoggingOptions.SectionName)
        .Get<ActorInformationLoggingOptions>() ?? new ActorInformationLoggingOptions())
        .Compile();
        var natsConsumerOptions = config
        .GetSection(NatsConsumerOptions.SectionName)
        .Get<NatsConsumerOptions>() ?? new NatsConsumerOptions();
        var natsJetStreamConsumerOptions = config
        .GetSection(NatsJetStreamConsumerOptions.SectionName)
        .Get<NatsJetStreamConsumerOptions>() ?? new NatsJetStreamConsumerOptions();
        var brokerUrl = config["IFM_TEST_NATS_URL"];
        if (!string.IsNullOrWhiteSpace(brokerUrl))
        {
            natsConsumerOptions.Url = brokerUrl;
            natsJetStreamConsumerOptions.Url = brokerUrl;
        }
        natsConsumerOptions.Validate(admissionOptions);
        natsJetStreamConsumerOptions.Validate(admissionOptions);

        services.AddSingleton(admissionOptions);
        services.AddSingleton(actorInformationLoggingPolicy);
        services.AddSingleton<ActorAdmissionController>();
        services.AddSingleton<IActorSupervisor, ActorSupervisor>();
        services.AddSingleton<IActorService, ActorService>();
        services.AddSingleton<ISupervisorActorMetricsState, SupervisorActorMetricsState>();
        services.AddSingleton<TomasAI.IFM.Domain.Supervisor.Shared.Service.Health.Evaluation.SupervisorActorThreadHealthEvaluator>();
        services.AddSingleton<ISupervisorManagedActorMetricsSource>(provider =>
        new SupervisorManagedActorMetricsSource(
        provider.GetRequiredService<IActorSupervisor>().RuntimeContext,
        provider.GetRequiredService<TomasAI.IFM.Domain.Supervisor.Shared.Service.Health.Evaluation.SupervisorActorThreadHealthEvaluator>(),
        provider.GetRequiredService<ILogger<SupervisorManagedActorMetricsSource>>()));
        services.AddSingleton<ISupervisorExceptionLog, SupervisorExceptionLog>();
        services.AddSingleton<ISupervisorHealthLlmAdvisorySink, NoOpSupervisorHealthLlmAdvisorySink>();
        services.AddSingleton<ISupervisorActorMetricsPollingService, SupervisorActorMetricsPollingService>();
        services.AddSingleton<ISupervisorBootstrap,
        TomasAI.IFM.Domain.Supervisor.Shared.Service.Lifecycle.SupervisorBootstrap>();
        services.AddSingleton<ISupervisorManagedActorLifecycle,
        TomasAI.IFM.Domain.Supervisor.Shared.Service.Lifecycle.SupervisorManagedActorLifecycle>();
        services.AddSingleton<ISupervisorOperatorAuthorizer>(_ =>
        new TomasAI.IFM.Domain.Supervisor.Shared.Service.Lifecycle.SupervisorOperatorAuthorizer(
        config.GetSection("Supervisor:AllowedOperators").Get<string[]>() ?? []));
        services.AddSingleton<SupervisorOperationStore>();
        services.AddSingleton<RecoveryCanaryProjectionStore>();
        services.AddSingleton<SupervisorRecoveryCanaryProbe>();
        services.AddSingleton<ISupervisorOperationStore>(provider => provider.GetRequiredService<SupervisorOperationStore>());
        services.AddSingleton<ISupervisorHealthManager, SupervisorHealthManager>();
        services.AddSingleton<IActorRuntimeMetricsSourceProvider>(provider =>
        provider.GetRequiredService<IActorSupervisor>().RuntimeContext);
        services.AddSingleton<ISupervisorHistoryPersistence, SupervisorFileHistoryPersistence>();
        services.AddSingleton(new SupervisorHealthActionOptions
        {
            AutomaticMutationEnabled = config.GetValue<bool>("Supervisor:AutomaticMutationEnabled")
        });
        services.AddSingleton<SupervisorIncidentStore>();
        services.AddSingleton<ISupervisorIncidentStore>(provider => provider.GetRequiredService<SupervisorIncidentStore>());
        services.AddSingleton<SupervisorHistoryStore>();
        services.AddSingleton<ISupervisorHistoryStore>(provider => provider.GetRequiredService<SupervisorHistoryStore>());
        services.AddSingleton<ISupervisorHealthActionCoordinator, SupervisorHealthActionCoordinator>();
        services.AddSingleton<IActorRegistry>(_ =>
        {
            var actorTypes = (
            from reg in siContainer.GetCurrentRegistrations()
            where reg.ServiceType.IsClosedTypeOf(typeof(IActor<>))
            select reg.ServiceType)
            .Distinct()
            .ToArray();
            return new ActorRegistry(actorTypes);
        });
        services.AddSingleton<IActorFactory>(_ => new ActorFactory(actorType => GetContainerInstance(siContainer, actorType)!,
        actorType => RealtimeActorReplacementFactory.Create(actorType, type => GetContainerInstance(siContainer, type)!)));
        services.AddSingleton<INatsProducerOptions>(_ => new NatsProducerOptions
        {
            Url = EventLogQualification.Active is null ? brokerUrl ?? new NatsProducerOptions().Url : EventLogQualification.Active.BrokerUrl
        });
        services.AddSingleton<INatsConsumerOptions>(natsConsumerOptions);
        services.AddSingleton<INatsEventListenerOptions>(_ => new NatsEventListenerOptions
        {
            Url = EventLogQualification.Active is null ? brokerUrl ?? new NatsEventListenerOptions().Url : EventLogQualification.Active.BrokerUrl
        });
        services.AddSingleton<NatsConnectionManager>();
        services.AddTransient<IActorProducer, NatsActorProducer>();
        services.AddTransient<IActorConsumer, NatsActorConsumer>();
        services.AddSingleton<INatsJetStreamProducerOptions>(_ => new NatsJetStreamProducerOptions
        {
            Url = EventLogQualification.Active is null ? brokerUrl ?? new NatsJetStreamProducerOptions().Url : EventLogQualification.Active.BrokerUrl
        });
        services.AddSingleton<INatsJetStreamConsumerOptions>(natsJetStreamConsumerOptions);
        services.AddSingleton<IDurableReplayQueue, NatsJSDurableReplayQueue>();
        services.AddTransient<IJSActorProducer, NatsJetStreamActorProducer>();
        services.AddTransient<IJSActorConsumer, NatsJetStreamActorConsumer>();
        services.AddSingleton<IContainerInstance>(provider => new ContainerInstance(type =>
        {
            var instance = provider.GetService(type)!;
            instance ??= GetContainerInstance(siContainer, type)!;
            return instance;
        }));
        services.AddTransient<IActorThreadQueue>(provider =>
        admissionOptions.MailboxImplementation switch
        {
            ActorMailboxImplementation.Channel => new ActorThreadQueueV2(
            provider.GetRequiredService<ActorAdmissionController>(),
            admissionOptions.DefaultMailboxMessageLimit,
            32,
            32),
            ActorMailboxImplementation.MpscRing => new ActorThreadQueueMpscRing(
            provider.GetRequiredService<ActorAdmissionController>(),
            admissionOptions.DefaultMailboxMessageLimit),
            ActorMailboxImplementation.SpscRing => new ActorThreadQueueSpscRing(
            provider.GetRequiredService<ActorAdmissionController>(),
            admissionOptions.DefaultMailboxMessageLimit),
            _ => throw new InvalidOperationException(
            $"Unknown actor mailbox implementation '{admissionOptions.MailboxImplementation}'.")
        });


    }
}
