using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.VerificationTests.Strategy.IntrinsicTime.RegimeDiscovery;

public sealed class RegimeDiscoveryVerificationFixture : IAsyncDisposable
{
    public static readonly TimeSpan ScenarioTimeout = TimeSpan.FromSeconds(30);
    readonly WebApplicationFactory<Program> factory;
    readonly IActorSupervisor supervisor;
    readonly IActorProducer publisher;

    RegimeDiscoveryVerificationFixture(
        WebApplicationFactory<Program> factory,
        IActorSupervisor supervisor,
        IActorProducer publisher,
        MarketConditionPipelineCommandProbe probe)
    {
        this.factory = factory;
        this.supervisor = supervisor;
        this.publisher = publisher;
        Probe = probe;
    }

    public IServiceProvider Services => factory.Services;
    public IDbContextFactory Database => Services.GetRequiredService<IDbContextFactory>();
    public MarketConditionPipelineCommandProbe Probe { get; }

    public static async Task<RegimeDiscoveryVerificationFixture> StartAsync(
        WebApplicationFactory<Program> source,
        Action<IServiceCollection>? configure = null)
    {
        var factory = source.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton(new IntrinsicTimeStrategyWorkflowOptions { Enabled = true });
            services.AddSingleton<IMarketConditionSnapshotProvider, BlockingMarketConditionSnapshotProvider>();
            configure?.Invoke(services);
        }));
        _ = factory.CreateClient();
        var supervisor = factory.Services.GetRequiredService<IActorSupervisor>();
        var probe = new MarketConditionPipelineCommandProbe(factory.Services);

        var publisher = factory.Services.GetRequiredService<IActorProducer>();
        await publisher.StartAsync(new ActorMailboxId(ActorType.Realtime, $"RdvPublisher{Guid.NewGuid():N}"));
        return new RegimeDiscoveryVerificationFixture(factory, supervisor, publisher, probe);
    }

    public async Task<IReadOnlyDictionary<TimeFrameType, RegimeDiscoveryParameterSet>> PrepareAsync(
        IEnumerable<VerificationExecution> executions)
    {
        var values = executions.ToArray();
        await Services.GetRequiredService<ConfigurationSchemaDb>().CreateAllAsync();
        await Services.GetRequiredService<TradeSchemaDb>().CreateAsync(["regime_discovery"]);
        var configuration = Services.GetRequiredService<IConfigurationDbContext>();
        var parameterSets = new Dictionary<TimeFrameType, RegimeDiscoveryParameterSet>();
        foreach (var horizon in values.Select(value => value.EntityId.ItiSignalEntityId.TimePeriod).Distinct())
        {
            var parameterSet = RegimeDiscoveryParameterSet.CreateDefault(
                Guid.CreateVersion7(), Guid.CreateVersion7(), horizon);
            await configuration.InsertRegimeDiscoveryDraftAsync(
                parameterSet, "RDV verification", "regime-discovery-verification");
            await configuration.PublishAsync(
                StrategyParameterSetKind.RegimeDiscovery,
                parameterSet.ParameterSetId,
                parameterSet.Version,
                DateTime.UtcNow.AddMinutes(-1));
            var marketCondition = MarketConditionParameterSet.CreateDefault(
                Guid.CreateVersion7(), parameterSet.StrategyParameterSetId, 1, horizon,
                strategyVersion: parameterSet.StrategyParameterSetVersion) with
            {
                Execution = new MarketConditionExecutionConfiguration
                {
                    MaximumExecutionMilliseconds = 25_000,
                    TransportReplyGraceMilliseconds = 5_000,
                    ResultLifetimeSeconds = 30
                }
            };
            await configuration.InsertMarketConditionDraftAsync(
                marketCondition, "RDV Market Condition dispatch boundary", "regime-discovery-verification");
            await configuration.PublishAsync(
                StrategyParameterSetKind.MarketCondition,
                marketCondition.ParameterSetId,
                marketCondition.Version,
                DateTime.UtcNow.AddMinutes(-1));
            parameterSets.Add(horizon, parameterSet);
        }

        var cache = Services.GetRequiredService<IRegimeDiscoveryMarketSignalCache>();
        cache.Clear();
        long sequence = 0;
        foreach (var execution in values)
        {
            var signalId = execution.EntityId.ItiSignalEntityId;
            var request = RegimeDiscoverySnapshotRequestFactory.Create(
                MarketSeriesIdentity.ForContract(signalId.ContractId),
                parameterSets[signalId.TimePeriod]);
            foreach (var requirement in request.Requirements.Where(requirement =>
                         !execution.Scenario.OmittedMetrics.Contains(requirement.Metric)))
            {
                var now = DateTime.UtcNow;
                cache.Upsert(new RegimeDiscoverySignalObservation
                {
                    Metric = requirement.Metric,
                    SignalKey = new MarketAnalyticsSignalKey(
                        request.MarketSeriesIdentity,
                        RegimeDiscoveryScenarioDataBuilder.Kind(requirement.Metric),
                        requirement.TimeFrame,
                        requirement.CalculationConfigurationId),
                    Value = execution.Scenario.Value(requirement.Metric),
                    MarketDataAsOfUtc = now,
                    CalculatedAtUtc = now,
                    SourceSequence = Interlocked.Increment(ref sequence),
                    SchemaVersion = 1,
                    CalculationVersion = "1",
                    IsWarm = true,
                    IsValid = true,
                    Availability = RegimeDiscoverySignalAvailability.Available,
                    SignalIdentity = $"{signalId.ContractId}.{requirement.Metric}.{requirement.TimeFrame}"
                });
            }
        }
        return parameterSets;
    }

    public async ValueTask<FuturesItiSignalGeneratedEvent> PublishAsync(
        IntrinsicTimeStrategyWorkflowEntityId entityId)
    {
        var signalId = entityId.ItiSignalEntityId;
        var now = DateTime.UtcNow;
        var trigger = new FuturesItiSignalGeneratedEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesItiSignalGeneratedEvent.RealtimeActor,
                FuturesItiSignalGeneratedEvent.Verb,
                signalId.Format()),
            Id = Guid.NewGuid(),
            EntityId = signalId,
            CommandId = Guid.NewGuid(),
            AggregateId = signalId.Format(),
            EventSource = "RegimeDiscoveryVerificationTests",
            ReceivedOn = now,
            FuturesItiSignal = new FuturesItiSignalV2ReadModel
            {
                ContractId = signalId.ContractId,
                ValueDate = signalId.ValueDate,
                TimeFrameStartValueDate = signalId.ValueDate,
                TimePeriod = signalId.TimePeriod,
                IntrinsicTime = now,
                IntrinsicPrice = 6500d
            },
            CreatedOn = now,
            CreatedBy = "regime-discovery-verification",
            VixFuturesPrice = 18d
        };
        await publisher.SendAsync<FuturesItiSignalGeneratedEvent, FuturesItiSignalEntityId>(
            trigger.Subject,
            trigger);
        return trigger;
    }

    public async Task<IntrinsicTimeStrategyWorkflowHistoryReadModel> WaitForRevisionAsync(
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        long revision,
        StrategyWorkflowStage? stage = null)
    {
        var deadline = DateTime.UtcNow + ScenarioTimeout;
        var last = "no workflow row";
        while (DateTime.UtcNow < deadline)
        {
            var rows = await Database.TradeDb.GetIntrinsicTimeStrategyWorkflowsByEntityAsync(
                entityId.Format(), DateTime.MaxValue, 20);
            if (rows.Count > 0)
            {
                var latest = rows.First();
                last = $"revision {latest.WorkflowRevision}, stage {latest.CurrentStage}, status {latest.Status}";
            }
            var match = rows.FirstOrDefault(row => row.WorkflowRevision == revision &&
                                                   (stage is null || row.CurrentStage == stage));
            if (match is not null) return match;
            await Task.Delay(50);
        }
        throw new TimeoutException(
            $"Workflow {entityId.Format()} did not reach revision {revision}/{stage}; last observed {last}.");
    }

    public async Task<IntrinsicTimeStrategyWorkflowHistoryReadModel> WaitForTerminalAsync(
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        StrategyWorkflowOutcome outcome)
    {
        var deadline = DateTime.UtcNow + ScenarioTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var rows = await Database.TradeDb.GetIntrinsicTimeStrategyWorkflowsByEntityAsync(
                entityId.Format(), DateTime.MaxValue, 20);
            var match = rows.FirstOrDefault(row => row.Status == StrategyWorkflowStatus.Stopped &&
                                                   row.Outcome == outcome);
            if (match is not null) return match;
            await Task.Delay(50);
        }
        throw new TimeoutException($"Workflow {entityId.Format()} did not stop with {outcome}.");
    }

    public async Task<IntrinsicTimeStrategyWorkflowCommandState> LoadStateAsync(
        IntrinsicTimeStrategyWorkflowEntityId entityId)
    {
        var command = new ExecuteIntrinsicTimeStrategyWorkflowCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                ExecuteIntrinsicTimeStrategyWorkflowCommand.Actor,
                ExecuteIntrinsicTimeStrategyWorkflowCommand.Verb,
                entityId.Format()),
            EntityId = entityId
        };
        var repository = supervisor.Container.Resolve<
            IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>>();
        return await repository.LoadStateAsync(command);
    }

    public async Task<RegimeDiscoveryFunctionState> LoadFunctionStateAsync(
        IntrinsicTimeStrategyWorkflowView view)
    {
        var executionId = RegimeDiscoveryExecutionEntityId.Create(view.EntityId, view.WorkflowId);
        var request = new ExecuteRegimeDiscoveryPipelineCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Function,
                ExecuteRegimeDiscoveryPipelineCommand.Actor,
                ExecuteRegimeDiscoveryPipelineCommand.Verb,
                executionId.Format()),
            EntityId = executionId,
            InputWorkflowRevision = 1,
            ParameterPayloadSha256 = view.RegimeDiscoveryParameterPayloadSha256
        };
        var repository = supervisor.Container.Resolve<IEventSourceFunctionStateRepository<
            RegimeDiscoveryFunctionState,
            ExecuteRegimeDiscoveryPipelineCommand>>();
        return await repository.LoadStateAsync(request);
    }

    public async Task<IntrinsicTimeStrategyWorkflowReadModel?> QueryWorkflowAsync(
        StrategyWorkflowId workflowId,
        long revision)
    {
        var producer = Services.GetRequiredService<IActorProducer>();
        await producer.StartAsync(new ActorMailboxId(ActorType.Query, $"RdvQuery{Guid.NewGuid():N}"));
        try
        {
            var response = await new IntrinsicTimeStrategyWorkflowQueryApi(producer)
                .GetByIdAsync(workflowId, revision);
            return response.Success ? response.Value : null;
        }
        finally
        {
            await producer.StopAsync();
        }
    }

    public static VerificationExecution Execution(
        RegimeDiscoveryScenario scenario,
        TimeFrameType horizon,
        string suffix = "")
    {
        var contract = $"ES-RDV-{scenario.Name}-{horizon}-{suffix}-{Guid.NewGuid():N}";
        return new VerificationExecution(
            IntrinsicTimeStrategyWorkflowEntityId.Create(new FuturesItiSignalEntityId(
                contract, new DateOnly(2026, 8, 28), horizon)),
            scenario);
    }

    public async ValueTask DisposeAsync()
    {
        Services.GetRequiredService<IRegimeDiscoveryMarketSignalCache>().Clear();
        await publisher.StopAsync();
        await factory.DisposeAsync();
    }

    sealed class BlockingMarketConditionSnapshotProvider : IMarketConditionSnapshotProvider
    {
        public async Task<MarketConditionSnapshotCaptureResult> CaptureAsync(
            ExecuteMarketConditionPipelineCommand command,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The verification fixture intentionally holds Market Condition.");
        }
    }
}

public sealed record VerificationExecution(
    IntrinsicTimeStrategyWorkflowEntityId EntityId,
    RegimeDiscoveryScenario Scenario);
