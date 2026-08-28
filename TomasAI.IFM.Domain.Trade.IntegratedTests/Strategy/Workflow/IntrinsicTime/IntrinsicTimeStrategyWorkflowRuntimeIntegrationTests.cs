using System.Collections.Concurrent;
using System.Text;
using FluentAssertions;
using MessagePack;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Application.Actor.IntegrationTests;
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
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

/// <summary>Prevents fixed pipeline actor addresses from overlapping another integration host.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntrinsicTimeStrategyWorkflowRuntimeCollection
{
    /// <summary>Integration collection name.</summary>
    public const string Name = "IntrinsicTimeStrategyWorkflowRuntime";
}

/// <summary>
/// Exercises the strategy workflow skeleton through production actors, NATS, PostgreSQL event sourcing, and ScyllaDB
/// while using the real Regime Discovery worker and substituting only the four not-yet-implemented workers.
/// </summary>
[Trait("Category", "Integration")]
[Collection(IntrinsicTimeStrategyWorkflowRuntimeCollection.Name)]
public sealed class IntrinsicTimeStrategyWorkflowRuntimeIntegrationTests(
    WebApplicationFactory<Program> sourceFactory,
    TradeDatabaseFixture database)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<TradeDatabaseFixture>
{
    static readonly TimeSpan ScenarioTimeout = TimeSpan.FromSeconds(30);
    static readonly StrategyWorkflowStage[] Stages =
    [
        StrategyWorkflowStage.RegimeDiscovery,
        StrategyWorkflowStage.MarketCondition,
        StrategyWorkflowStage.TradeSelection,
        StrategyWorkflowStage.OrderComposition,
        StrategyWorkflowStage.RiskManagement
    ];
    static readonly StrategyWorkflowStage[] DummyStages = Stages[1..];

    /// <summary>
    /// Confirms committed Started snapshots dispatch isolated Regime executions and only a projected public completion
    /// advances the workflow to the next pipeline.
    /// </summary>
    [Fact]
    public async Task Projected_regime_completion_advances_each_workflow_to_market_condition_once()
    {
        await using var factory = sourceFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton(new IntrinsicTimeStrategyWorkflowOptions { Enabled = true })));
        _ = factory.CreateClient();
        var supervisor = factory.Services.GetRequiredService<IActorSupervisor>();
        supervisor.IsReady.Should().BeTrue();
        factory.Services.GetRequiredService<IntrinsicTimeStrategyWorkflowOptions>()
            .Enabled.Should().BeTrue("the integration host enables live workflow routing");

        await using var pipelines = await DummyPipelineHarness.StartAsync(factory.Services, supervisor);
        var workflowMailbox = new ActorMailboxId(
            ActorType.Realtime,
            IntrinsicTimeStrategyWorkflowRealtimeActor.ActorName);
        supervisor.GetRealtimeRoutes(new ActorTypeId(
                ActorType.Realtime,
                FuturesItiSignalGeneratedEvent.RealtimeActor,
                FuturesItiSignalGeneratedEvent.Verb))
            .Should().Contain(workflowMailbox);

        var publisher = factory.Services.GetRequiredService<IActorProducer>();
        await publisher.StartAsync(new ActorMailboxId(ActorType.Realtime, "ItswRuntimeTestPublisher"));
        try
        {
            var runId = Guid.NewGuid().ToString("N")[..8];
            var successEntities = new[]
            {
                Entity($"ES-ITSW-{runId}-D", TimeFrameType.Daily),
                Entity($"ES-ITSW-{runId}-W", TimeFrameType.Weekly),
                Entity($"ES-ITSW-{runId}-M", TimeFrameType.Monthly)
            };
            await PrepareRegimeDiscoveryAsync(factory.Services, successEntities);

            await Task.WhenAll(successEntities.Select(entity =>
                PublishTriggerAsync(publisher, entity.ItiSignalEntityId).AsTask()));

            var started = await Task.WhenAll(successEntities.Select(entity =>
                WaitForStatusAsync(entity, StrategyWorkflowStatus.Running)));
            started.Should().OnlyContain(model =>
                model.WorkflowRevision == 1 && model.CurrentStage == StrategyWorkflowStage.RegimeDiscovery);

            foreach (var entity in successEntities)
            {
                var history = started.Single(model => model.WorkflowEntityId == entity.Format());
                await WaitForRegimeDiscoveryAsync(history.WorkflowId, "Completed");
                await pipelines.WaitForStartCountAsync(entity, StrategyWorkflowStage.MarketCondition, 1);
                var advanced = await WaitForStageAsync(entity, StrategyWorkflowStage.MarketCondition, 2);
                await AssertPersistedAdvancedSnapshotAsync(factory.Services, entity, advanced);
                var regime = await database.TradeDb.GetRegimeDiscoveryAsync(history.WorkflowId);
                regime.Should().NotBeNull();
                regime!.Status.Should().Be("Completed");
                regime.ResultPayload.Should().NotBeEmpty();
                pipelines.ProcessedStages(entity).Should().Equal(StrategyWorkflowStage.MarketCondition);
                pipelines.StartCount(entity, StrategyWorkflowStage.MarketCondition).Should().Be(1);
                DummyStages.Skip(1).Should().OnlyContain(stage => pipelines.StartCount(entity, stage) == 0);
            }
        }
        finally
        {
            await publisher.StopAsync();
        }
    }

    /// <summary>
    /// Confirms an unexpired Started snapshot is Busy: a second trigger commits no state and dispatches no later work.
    /// </summary>
    [Fact]
    public async Task Unexpired_started_workflow_ignores_a_second_trigger_without_a_state_commit()
    {
        await using var factory = sourceFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton(new IntrinsicTimeStrategyWorkflowOptions { Enabled = true })));
        _ = factory.CreateClient();
        var supervisor = factory.Services.GetRequiredService<IActorSupervisor>();
        supervisor.IsReady.Should().BeTrue();

        await using var pipelines = await DummyPipelineHarness.StartAsync(factory.Services, supervisor);
        var publisher = factory.Services.GetRequiredService<IActorProducer>();
        await publisher.StartAsync(new ActorMailboxId(ActorType.Realtime, "ItswBusyRejectionTestPublisher"));
        try
        {
            var runId = Guid.NewGuid().ToString("N")[..8];
            var entity = Entity($"ES-ITSW-{runId}-BUSY", TimeFrameType.Daily);
            await PrepareRegimeDiscoveryAsync(factory.Services, [entity]);
            await PublishTriggerAsync(publisher, entity.ItiSignalEntityId);
            var running = await WaitForStatusAsync(entity, StrategyWorkflowStatus.Running);
            await WaitForRegimeDiscoveryAsync(running.WorkflowId, "Completed");
            await pipelines.WaitForStartCountAsync(entity, StrategyWorkflowStage.MarketCondition, 1);

            await PublishTriggerAsync(publisher, entity.ItiSignalEntityId);
            await Task.Delay(500);

            var replayed = await LoadStateAsync(factory.Services, entity);
            replayed.HasActiveWorkflow.Should().BeTrue();
            replayed.ActiveWorkflow!.WorkflowId.Should().Be(running.WorkflowId);
            replayed.CurrentView!.WorkflowRevision.Should().Be(2);
            replayed.CurrentView.CurrentStage.Should().Be(StrategyWorkflowStage.MarketCondition);
            pipelines.StartCount(entity, StrategyWorkflowStage.MarketCondition).Should().Be(1);
            foreach (var stage in DummyStages.Skip(1))
                pipelines.StartCount(entity, stage).Should().Be(0);
        }
        finally
        {
            await publisher.StopAsync();
        }
    }

    /// <summary>Confirms an expected Regime snapshot failure closes the workflow and dispatches no next stage.</summary>
    [Fact]
    public async Task Expected_regime_failure_closes_workflow_without_next_pipeline()
    {
        await using var factory = sourceFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton(new IntrinsicTimeStrategyWorkflowOptions
            {
                Enabled = true,
                RequireWarmRegimeDiscoverySignals = false
            })));
        _ = factory.CreateClient();
        var supervisor = factory.Services.GetRequiredService<IActorSupervisor>();
        await using var pipelines = await DummyPipelineHarness.StartAsync(factory.Services, supervisor);
        var publisher = factory.Services.GetRequiredService<IActorProducer>();
        await publisher.StartAsync(new ActorMailboxId(ActorType.Realtime, "ItswExpectedFailurePublisher"));
        try
        {
            var entity = Entity($"ES-ITSW-{Guid.NewGuid():N}-FAIL", TimeFrameType.Daily);
            await PrepareRegimeDiscoveryAsync(factory.Services, [entity]);
            factory.Services.GetRequiredService<IRegimeDiscoveryMarketSignalCache>().Clear();

            await PublishTriggerAsync(publisher, entity.ItiSignalEntityId);
            var started = await WaitForStatusAsync(entity, StrategyWorkflowStatus.Running);
            await WaitForTerminalAsync(entity, StrategyWorkflowStatus.Stopped,
                StrategyWorkflowOutcome.PipelineFailed);

            (await database.TradeDb.GetRegimeDiscoveryAsync(started.WorkflowId)).Should().BeNull(
                "failed Function results are returned to workflow and are never projected");

            var state = await LoadStateAsync(factory.Services, entity);
            state.CurrentView!.Status.Should().Be(WorkflowStrategyMachineStatus.Failed);
            state.CurrentView.RegimeDiscovery.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Failed);
            DummyStages.Should().OnlyContain(stage => pipelines.StartCount(entity, stage) == 0);
        }
        finally
        {
            await publisher.StopAsync();
        }
    }

    /// <summary>Confirms the fixed maximum deadline wins and no late/failed worker dispatches a next stage.</summary>
    [Fact]
    public async Task Forced_regime_timeout_closes_workflow_without_next_pipeline()
    {
        await using var factory = sourceFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(new IntrinsicTimeStrategyWorkflowOptions
                {
                    Enabled = true,
                    RequireWarmRegimeDiscoverySignals = false
                });
                services.RemoveAll<RegimeDiscoveryExecutionOptions>();
                services.AddSingleton(new RegimeDiscoveryExecutionOptions
                {
                    MaximumExecutionDuration = TimeSpan.FromSeconds(1)
                });
                services.RemoveAll<IRegimeDiscoveryMarketSignalSnapshotProvider>();
                services.AddSingleton<IRegimeDiscoveryMarketSignalSnapshotProvider>(new BlockingSnapshotProvider());
            }));
        _ = factory.CreateClient();
        var supervisor = factory.Services.GetRequiredService<IActorSupervisor>();
        await using var pipelines = await DummyPipelineHarness.StartAsync(factory.Services, supervisor);
        var publisher = factory.Services.GetRequiredService<IActorProducer>();
        await publisher.StartAsync(new ActorMailboxId(ActorType.Realtime, "ItswForcedTimeoutPublisher"));
        try
        {
            var entity = Entity($"ES-ITSW-{Guid.NewGuid():N}-TIMEOUT", TimeFrameType.Daily);
            await PrepareRegimeDiscoveryAsync(factory.Services, [entity]);

            await PublishTriggerAsync(publisher, entity.ItiSignalEntityId);
            var started = await WaitForStatusAsync(entity, StrategyWorkflowStatus.Running);
            await WaitForTerminalAsync(entity, StrategyWorkflowStatus.Stopped,
                StrategyWorkflowOutcome.TimedOut);

            (await database.TradeDb.GetRegimeDiscoveryAsync(started.WorkflowId)).Should().BeNull(
                "timed-out Function results are returned to workflow and are never projected");

            var state = await LoadStateAsync(factory.Services, entity);
            state.CurrentView!.Status.Should().Be(WorkflowStrategyMachineStatus.TimedOut);
            state.CurrentView.RegimeDiscovery.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.TimedOut);
            DummyStages.Should().OnlyContain(stage => pipelines.StartCount(entity, stage) == 0);
        }
        finally
        {
            await publisher.StopAsync();
        }
    }

    async Task AssertPersistedAdvancedSnapshotAsync(
        IServiceProvider services,
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        IntrinsicTimeStrategyWorkflowHistoryReadModel history)
    {
        var detail = await database.TradeDb.GetIntrinsicTimeStrategyWorkflowAsync(history.WorkflowId);
        detail.Should().NotBeNull();
        detail!.WorkflowRevision.Should().Be(history.WorkflowRevision);
        detail.WorkflowEntityId.Should().Be(entityId.Format());

        var projectedState = MessagePackSerializer.Deserialize<IntrinsicTimeStrategyWorkflowView>(detail.StatePayload);
        projectedState.Status.Should().Be(WorkflowStrategyMachineStatus.Started);
        projectedState.CurrentStage.Should().Be(StrategyWorkflowStage.MarketCondition);
        projectedState.WorkflowRevision.Should().Be(history.WorkflowRevision);

        var replayed = await LoadStateAsync(services, entityId);
        replayed.CurrentView.Should().NotBeNull();
        MessagePackSerializer.Serialize(replayed.CurrentView!).Should()
            .Equal(MessagePackSerializer.Serialize(projectedState));

        var queryProducer = services.GetRequiredService<IActorProducer>();
        await queryProducer.StartAsync(new ActorMailboxId(ActorType.Query, "ItswRuntimeQueryProbe"));
        try
        {
            var response = await new IntrinsicTimeStrategyWorkflowQueryApi(queryProducer)
                .GetByIdAsync(history.WorkflowId, history.WorkflowRevision);
            response.Success.Should().BeTrue();
            response.Value.Should().NotBeNull();
            response.Value!.WorkflowRevision.Should().Be(history.WorkflowRevision);

            var observation = await new IntrinsicTimeStrategyWorkflowQueryApi(queryProducer)
                .GetObservationAsync(entityId);
            observation.Success.Should().BeTrue();
            observation.Value.Should().NotBeNull();
            observation.Value!.WorkflowId.Should().Be(history.WorkflowId);
            observation.Value.WorkflowRevision.Should().Be(history.WorkflowRevision);
            observation.Value.WorkflowAcceptedRegimeTerminal.Should().BeTrue();
            observation.Value.RegimeTerminal.Should().NotBeNull();
        }
        finally
        {
            await queryProducer.StopAsync();
        }

        projectedState.RegimeDiscovery.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Completed);
        projectedState.MarketCondition.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Processing);
    }

    async Task<IntrinsicTimeStrategyWorkflowHistoryReadModel> WaitForTerminalAsync(
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        StrategyWorkflowStatus status,
        StrategyWorkflowOutcome outcome)
    {
        var deadline = DateTime.UtcNow + ScenarioTimeout;
        var lastObserved = "no ScyllaDB workflow history row";
        do
        {
            var rows = await database.TradeDb.GetIntrinsicTimeStrategyWorkflowsByEntityAsync(
                entityId.Format(), DateTime.MaxValue, 10);
            if (rows.Count > 0)
            {
                var latest = rows.First();
                lastObserved = $"{latest.Status}/{latest.Outcome} at revision {latest.WorkflowRevision}";
            }
            var terminal = rows.FirstOrDefault(row => row.Status == status && row.Outcome == outcome);
            if (terminal is not null)
                return terminal;
            await Task.Delay(100);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException(
            $"Workflow {entityId.Format()} did not reach {status}/{outcome} within {ScenarioTimeout}; " +
            $"last observed: {lastObserved}.");
    }

    async Task WaitForRegimeDiscoveryAsync(StrategyWorkflowId workflowId, string status)
    {
        var deadline = DateTime.UtcNow + ScenarioTimeout;
        do
        {
            var regime = await database.TradeDb.GetRegimeDiscoveryAsync(workflowId);
            if (regime?.Status == status)
                return;
            await Task.Delay(100);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException(
            $"Regime Discovery {workflowId} did not reach {status} within {ScenarioTimeout}.");
    }

    async Task<IntrinsicTimeStrategyWorkflowHistoryReadModel> WaitForStatusAsync(
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        StrategyWorkflowStatus status)
    {
        var deadline = DateTime.UtcNow + ScenarioTimeout;
        do
        {
            var rows = await database.TradeDb.GetIntrinsicTimeStrategyWorkflowsByEntityAsync(
                entityId.Format(), DateTime.MaxValue, 10);
            var match = rows.FirstOrDefault(row => row.Status == status);
            if (match is not null)
                return match;
            await Task.Delay(100);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException(
            $"Workflow {entityId.Format()} did not reach {status} within {ScenarioTimeout}.");
    }

    async Task<IntrinsicTimeStrategyWorkflowHistoryReadModel> WaitForStageAsync(
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        StrategyWorkflowStage stage,
        long minimumRevision)
    {
        var deadline = DateTime.UtcNow + ScenarioTimeout;
        do
        {
            var rows = await database.TradeDb.GetIntrinsicTimeStrategyWorkflowsByEntityAsync(
                entityId.Format(), DateTime.MaxValue, 10);
            var match = rows.FirstOrDefault(row =>
                row.CurrentStage == stage && row.WorkflowRevision >= minimumRevision);
            if (match is not null)
                return match;
            await Task.Delay(100);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException(
            $"Workflow {entityId.Format()} did not reach {stage} revision {minimumRevision} within {ScenarioTimeout}.");
    }

    async Task<IntrinsicTimeStrategyWorkflowStartAttemptReadModel> WaitForRejectedStartAsync(
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        Guid triggerEventId)
    {
        var deadline = DateTime.UtcNow + ScenarioTimeout;
        do
        {
            var attempts = await database.TradeDb.GetIntrinsicTimeStrategyWorkflowStartAttemptsAsync(
                entityId.Format(), DateTime.MaxValue, 10);
            var rejected = attempts.FirstOrDefault(attempt =>
                attempt.Decision == StrategyWorkflowStartDecision.Rejected
                && attempt.TriggerEventId == triggerEventId);
            if (rejected is not null)
                return rejected;
            await Task.Delay(100);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException(
            $"Workflow {entityId.Format()} did not project a rejected start for trigger {triggerEventId} " +
            $"within {ScenarioTimeout}.");
    }

    static async ValueTask<IntrinsicTimeStrategyWorkflowCommandState> LoadStateAsync(
        IServiceProvider services,
        IntrinsicTimeStrategyWorkflowEntityId entityId)
    {
        var replayCommand = new ExecuteIntrinsicTimeStrategyWorkflowCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                ExecuteIntrinsicTimeStrategyWorkflowCommand.Actor,
                ExecuteIntrinsicTimeStrategyWorkflowCommand.Verb,
                entityId.Format()),
            EntityId = entityId
        };
        var repository = services.GetRequiredService<IActorSupervisor>().Container.Resolve<
            IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>>();
        return await repository.LoadStateAsync(replayCommand);
    }

    static IntrinsicTimeStrategyWorkflowEntityId Entity(string contractId, TimeFrameType period)
        => IntrinsicTimeStrategyWorkflowEntityId.Create(new FuturesItiSignalEntityId(
            contractId,
            new DateOnly(2026, 8, 25),
            period));

    static async ValueTask<FuturesItiSignalGeneratedEvent> PublishTriggerAsync(
        IActorProducer publisher,
        FuturesItiSignalEntityId signalId)
    {
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
            EventSource = "ItswRuntimeIntegrationTest",
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
            CreatedBy = "itsw-runtime-integration",
            VixFuturesPrice = 18d
        };
        await publisher.SendAsync<FuturesItiSignalGeneratedEvent, FuturesItiSignalEntityId>(
            trigger.Subject,
            trigger);
        return trigger;
    }

    static StrategyWorkflowStageState Stage(
        IntrinsicTimeStrategyWorkflowState state,
        StrategyWorkflowStage stage)
        => stage switch
        {
            StrategyWorkflowStage.RegimeDiscovery => state.RegimeDiscovery,
            StrategyWorkflowStage.MarketCondition => state.MarketCondition,
            StrategyWorkflowStage.TradeSelection => state.TradeSelection,
            StrategyWorkflowStage.OrderComposition => state.OrderComposition,
            StrategyWorkflowStage.RiskManagement => state.RiskManagement,
            _ => throw new ArgumentOutOfRangeException(nameof(stage))
        };

    static async Task PrepareRegimeDiscoveryAsync(
        IServiceProvider services,
        IEnumerable<IntrinsicTimeStrategyWorkflowEntityId> entities)
    {
        var values = entities.ToArray();
        await services.GetRequiredService<ConfigurationSchemaDb>().CreateAllAsync();
        await services.GetRequiredService<TradeSchemaDb>().CreateAsync(["regime_discovery"]);
        var configuration = services.GetRequiredService<IConfigurationDbContext>();
        var parameterSets = new Dictionary<TimeFrameType, RegimeDiscoveryParameterSet>();
        foreach (var horizon in values.Select(value => value.ItiSignalEntityId.TimePeriod).Distinct())
        {
            var parameterSet = RegimeDiscoveryParameterSet.CreateDefault(
                Guid.CreateVersion7(), Guid.CreateVersion7(), horizon);
            await configuration.InsertRegimeDiscoveryDraftAsync(
                parameterSet, "RD-16 integration qualification", "rd-16-integration");
            await configuration.PublishAsync(
                StrategyParameterSetKind.RegimeDiscovery,
                parameterSet.ParameterSetId,
                parameterSet.Version,
                DateTime.UtcNow.AddMinutes(-1));
            parameterSets.Add(horizon, parameterSet);
        }

        var cache = services.GetRequiredService<IRegimeDiscoveryMarketSignalCache>();
        cache.Clear();
        long sourceSequence = 0;
        foreach (var entity in values)
        {
            var signalId = entity.ItiSignalEntityId;
            var parameterSet = parameterSets[signalId.TimePeriod];
            var request = RegimeDiscoverySnapshotRequestFactory.Create(
                MarketSeriesIdentity.ForContract(signalId.ContractId), parameterSet);
            foreach (var requirement in request.Requirements)
            {
                var now = DateTime.UtcNow;
                var key = new MarketAnalyticsSignalKey(
                    request.MarketSeriesIdentity,
                    SignalKind(requirement.Metric),
                    requirement.TimeFrame,
                    requirement.CalculationConfigurationId);
                cache.Upsert(new RegimeDiscoverySignalObservation
                {
                    Metric = requirement.Metric,
                    SignalKey = key,
                    Value = SignalValue(requirement.Metric),
                    MarketDataAsOfUtc = now,
                    CalculatedAtUtc = now,
                    SourceSequence = Interlocked.Increment(ref sourceSequence),
                    SchemaVersion = 1,
                    CalculationVersion = "1",
                    IsWarm = true,
                    IsValid = true,
                    Availability = RegimeDiscoverySignalAvailability.Available,
                    SignalIdentity = $"{signalId.ContractId}.{requirement.Metric}.{requirement.TimeFrame}"
                });
            }
        }
    }

    static decimal SignalValue(RegimeDiscoverySignalMetric metric) => metric switch
    {
        RegimeDiscoverySignalMetric.CurrentPrice => 105m,
        RegimeDiscoverySignalMetric.Ema20 => 103m,
        RegimeDiscoverySignalMetric.Ema50 => 101m,
        RegimeDiscoverySignalMetric.Ema200 => 99m,
        RegimeDiscoverySignalMetric.Ema20Slope => 0.08m,
        RegimeDiscoverySignalMetric.Ema50Slope => 0.06m,
        RegimeDiscoverySignalMetric.Ema200Slope => 0.04m,
        RegimeDiscoverySignalMetric.Rsi14 => 65m,
        RegimeDiscoverySignalMetric.Rsi14Slope => 2m,
        RegimeDiscoverySignalMetric.Adx14 => 30m,
        RegimeDiscoverySignalMetric.PlusDi14 => 30m,
        RegimeDiscoverySignalMetric.MinusDi14 => 15m,
        RegimeDiscoverySignalMetric.MacdHistogram => 0.5m,
        RegimeDiscoverySignalMetric.Atr14 => 2m,
        RegimeDiscoverySignalMetric.AtrBaselineRatio => 1m,
        RegimeDiscoverySignalMetric.VixLevel => 18m,
        RegimeDiscoverySignalMetric.VxFrontSecondRatio => 0.95m,
        RegimeDiscoverySignalMetric.PriorVolatilityComposite => 0.35m,
        RegimeDiscoverySignalMetric.RealizedVolatilityPercentile => 0.40m,
        RegimeDiscoverySignalMetric.BollingerWidthRatio => 1m,
        RegimeDiscoverySignalMetric.BollingerPosition => 0.5m,
        RegimeDiscoverySignalMetric.Ema20Interaction => 1m,
        RegimeDiscoverySignalMetric.AtrNormalizedRange => 1m,
        RegimeDiscoverySignalMetric.RollingHigh20 => 104m,
        RegimeDiscoverySignalMetric.RollingLow20 => 96m,
        RegimeDiscoverySignalMetric.BreakoutDistanceAtr => 0.6m,
        RegimeDiscoverySignalMetric.ItiDirection => 1m,
        RegimeDiscoverySignalMetric.ItiBandLevel => 1.2m,
        RegimeDiscoverySignalMetric.ItiReversalLevel => 0.1m,
        _ => 1m
    };

    static MarketAnalyticsSignalKind SignalKind(RegimeDiscoverySignalMetric metric) => metric switch
    {
        RegimeDiscoverySignalMetric.Ema20 or RegimeDiscoverySignalMetric.Ema50 or
            RegimeDiscoverySignalMetric.Ema200 or RegimeDiscoverySignalMetric.Ema20Slope or
            RegimeDiscoverySignalMetric.Ema50Slope or RegimeDiscoverySignalMetric.Ema200Slope or
            RegimeDiscoverySignalMetric.Ema20Interaction => MarketAnalyticsSignalKind.Ema,
        RegimeDiscoverySignalMetric.Rsi14 or RegimeDiscoverySignalMetric.Rsi14Slope => MarketAnalyticsSignalKind.Rsi,
        RegimeDiscoverySignalMetric.Adx14 or RegimeDiscoverySignalMetric.PlusDi14 or
            RegimeDiscoverySignalMetric.MinusDi14 => MarketAnalyticsSignalKind.Adx,
        RegimeDiscoverySignalMetric.MacdHistogram => MarketAnalyticsSignalKind.Macd,
        RegimeDiscoverySignalMetric.Atr14 or RegimeDiscoverySignalMetric.AtrBaselineRatio or
            RegimeDiscoverySignalMetric.AtrNormalizedRange => MarketAnalyticsSignalKind.Atr,
        RegimeDiscoverySignalMetric.BollingerWidth or RegimeDiscoverySignalMetric.BollingerWidthRatio or
            RegimeDiscoverySignalMetric.BollingerPosition => MarketAnalyticsSignalKind.BollingerBand,
        RegimeDiscoverySignalMetric.VxFrontSecondRatio or RegimeDiscoverySignalMetric.VixLevel =>
            MarketAnalyticsSignalKind.VxTermStructure,
        RegimeDiscoverySignalMetric.ItiDirection or RegimeDiscoverySignalMetric.ItiBandLevel or
            RegimeDiscoverySignalMetric.ItiReversalLevel or RegimeDiscoverySignalMetric.CurrentPrice =>
            MarketAnalyticsSignalKind.Iti,
        _ => MarketAnalyticsSignalKind.MarketStructure
    };

    sealed class BlockingSnapshotProvider : IRegimeDiscoveryMarketSignalSnapshotProvider
    {
        public async ValueTask<RegimeDiscoveryMarketSignalSnapshotResult> CaptureAsync(
            RegimeDiscoveryMarketSignalSnapshotRequest request,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The timeout owner should cancel this pure worker.");
        }
    }

    sealed class DummyPipelineHarness : IAsyncDisposable
    {
        readonly IActorSupervisor _supervisor;
        readonly DummyPipelineController _controller = new();
        readonly List<IActor> _actors = [];

        DummyPipelineHarness(IActorSupervisor supervisor) => _supervisor = supervisor;

        public static async Task<DummyPipelineHarness> StartAsync(
            IServiceProvider services,
            IActorSupervisor supervisor)
        {
            var harness = new DummyPipelineHarness(supervisor);
            foreach (var stage in DummyStages)
            {
                var source = new DummyRealtimeSourceActor(RealtimeActorName(stage));
                supervisor.AddActor(source);
                await source.StartAsync(supervisor);
                harness._actors.Add(source);

                var producer = services.GetRequiredService<IActorProducer>();
                var worker = new DummyPipelineCommandActor(stage, producer, harness._controller);
                supervisor.AddActor(worker);
                supervisor.AddProducer(worker.Id, producer);
                await worker.StartAsync(supervisor);
                harness._actors.Add(worker);
            }
            return harness;
        }

        public void FailAt(IntrinsicTimeStrategyWorkflowEntityId entityId, StrategyWorkflowStage stage)
            => _controller.FailAt(entityId, stage);

        public PipelineHold HoldAt(IntrinsicTimeStrategyWorkflowEntityId entityId, StrategyWorkflowStage stage)
            => _controller.HoldAt(entityId, stage);

        public int StartCount(IntrinsicTimeStrategyWorkflowEntityId entityId, StrategyWorkflowStage stage)
            => _controller.StartCount(entityId, stage);

        public Task WaitForStartCountAsync(
            IntrinsicTimeStrategyWorkflowEntityId entityId,
            StrategyWorkflowStage stage,
            int expectedCount)
            => _controller.WaitForStartCountAsync(entityId, stage, expectedCount);

        public StrategyWorkflowStage[] ProcessedStages(IntrinsicTimeStrategyWorkflowEntityId entityId)
            => _controller.ProcessedStages(entityId);

        public async ValueTask DisposeAsync()
        {
            foreach (var actor in _actors.AsEnumerable().Reverse())
            {
                await actor.StopAsync();
                _supervisor.RemoveActor(actor);
                if (actor.Id.ActorType == ActorType.Command)
                    _supervisor.RemoveProducer(actor.Id);
            }
        }
    }

    sealed class DummyPipelineController
    {
        readonly ConcurrentDictionary<string, StrategyWorkflowStage> _failures = new(StringComparer.Ordinal);
        readonly ConcurrentDictionary<string, ConcurrentDictionary<StrategyWorkflowStage, byte>> _processed
            = new(StringComparer.Ordinal);
        readonly ConcurrentDictionary<PipelineKey, int> _startCounts = new();
        readonly ConcurrentDictionary<PipelineKey, TaskCompletionSource> _holds = new();

        public void FailAt(IntrinsicTimeStrategyWorkflowEntityId entityId, StrategyWorkflowStage stage)
            => _failures[entityId.Format()] = stage;

        public bool ShouldFail(IntrinsicTimeStrategyWorkflowEntityId entityId, StrategyWorkflowStage stage)
            => _failures.TryGetValue(entityId.Format(), out var failureStage) && failureStage == stage;

        public void Record(IntrinsicTimeStrategyWorkflowEntityId entityId, StrategyWorkflowStage stage)
        {
            _processed.GetOrAdd(entityId.Format(), static _ => new()).TryAdd(stage, 0);
            _startCounts.AddOrUpdate(new PipelineKey(entityId.Format(), stage), 1, static (_, count) => count + 1);
        }

        public PipelineHold HoldAt(IntrinsicTimeStrategyWorkflowEntityId entityId, StrategyWorkflowStage stage)
        {
            var key = new PipelineKey(entityId.Format(), stage);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_holds.TryAdd(key, release))
                throw new InvalidOperationException($"Pipeline {stage} is already held for {entityId.Format()}.");
            return new PipelineHold(() =>
            {
                if (_holds.TryRemove(key, out var pending))
                    pending.TrySetResult();
            });
        }

        public int StartCount(IntrinsicTimeStrategyWorkflowEntityId entityId, StrategyWorkflowStage stage)
            => _startCounts.GetValueOrDefault(new PipelineKey(entityId.Format(), stage));

        public async Task WaitForStartCountAsync(
            IntrinsicTimeStrategyWorkflowEntityId entityId,
            StrategyWorkflowStage stage,
            int expectedCount)
        {
            var deadline = DateTime.UtcNow + ScenarioTimeout;
            while (DateTime.UtcNow < deadline)
            {
                if (StartCount(entityId, stage) >= expectedCount)
                    return;
                await Task.Delay(25);
            }
            throw new TimeoutException(
                $"Pipeline {stage} for {entityId.Format()} did not reach {expectedCount} start(s) within " +
                $"{ScenarioTimeout}.");
        }

        public Task WaitWhileHeldAsync(IntrinsicTimeStrategyWorkflowEntityId entityId, StrategyWorkflowStage stage)
            => _holds.TryGetValue(new PipelineKey(entityId.Format(), stage), out var release)
                ? release.Task
                : Task.CompletedTask;

        public StrategyWorkflowStage[] ProcessedStages(IntrinsicTimeStrategyWorkflowEntityId entityId)
            => _processed.TryGetValue(entityId.Format(), out var stages)
                ? stages.Keys.OrderBy(static stage => stage).ToArray()
                : [];

        readonly record struct PipelineKey(string EntityId, StrategyWorkflowStage Stage);
    }

    sealed class DummyPipelineCommandActor(
        StrategyWorkflowStage stage,
        IActorProducer producer,
        DummyPipelineController controller) : IActor
    {
        IActorSupervisor _supervisor = default!;

        public ActorMailboxId Id { get; } = new(ActorType.Command, CommandActorName(stage));
        public IActorMailbox Mailbox { get; private set; } = default!;
        public bool IsRunning { get; private set; }

        public async ValueTask StartAsync(IActorSupervisor supervisor)
        {
            _supervisor = supervisor;
            Mailbox = supervisor.CreateMailbox(Id);
            await producer.StartAsync(Id);
            IsRunning = true;
        }

        public ValueTask StartAsync(IActorSupervisor supervisor, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return StartAsync(supervisor);
        }

        public async ValueTask StopAsync()
        {
            if (!IsRunning)
                return;
            IsRunning = false;
            await producer.StopAsync();
        }

        public ValueTask StopAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return StopAsync();
        }

        public ValueTask HandleMessageAsync(IActorMessage message)
            => HandleMessageAsync(message, message.Subject.ThreadId);

        public async ValueTask HandleMessageAsync(IActorMessage message, ActorThreadId threadId)
        {
            PipelineStartInput input;
            try
            {
                input = Parse(message);
            }
            finally
            {
                message.ReleasePayload();
            }

            await message.ReplyAsync<ServiceResult<GuidResult>>(
                new ServiceOk<GuidResult>(new GuidResult(input.CommandId)));
            controller.Record(input.EntityId, stage);
            await PublishProcessingAsync(input);
            await controller.WaitWhileHeldAsync(input.EntityId, stage);
            if (controller.ShouldFail(input.EntityId, stage))
                await PublishFailureAsync(input);
            else
                await PublishCompletionAsync(input);
        }

        public ValueTask HandleMessageAsync(
            IActorMessage message,
            ActorThreadId threadId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return HandleMessageAsync(message, threadId);
        }

        PipelineStartInput Parse(IActorMessage message)
        {
            if (message.Subject.ActorType != ActorType.Command || message.Subject.Name != Id.Name)
                throw new InvalidOperationException($"Unexpected dummy pipeline subject {message.Subject}.");
            return stage switch
            {
                StrategyWorkflowStage.MarketCondition => PipelineStartInput.From(
                    message.AsCommand<StartMarketConditionPipelineCommand>()!),
                StrategyWorkflowStage.TradeSelection => PipelineStartInput.From(
                    message.AsCommand<StartTradeSelectionPipelineCommand>()!),
                StrategyWorkflowStage.OrderComposition => PipelineStartInput.From(
                    message.AsCommand<StartOrderCompositionPipelineCommand>()!),
                StrategyWorkflowStage.RiskManagement => PipelineStartInput.From(
                    message.AsCommand<StartRiskManagementPipelineCommand>()!),
                _ => throw new ArgumentOutOfRangeException(nameof(stage))
            };
        }

        async ValueTask PublishProcessingAsync(PipelineStartInput input)
        {
            switch (stage)
            {
                case StrategyWorkflowStage.MarketCondition:
                    await SendAsync(CreateProcessing<MarketConditionPipelineProcessingEvent>(input)); break;
                case StrategyWorkflowStage.TradeSelection:
                    await SendAsync(CreateProcessing<TradeSelectionPipelineProcessingEvent>(input)); break;
                case StrategyWorkflowStage.OrderComposition:
                    await SendAsync(CreateProcessing<OrderCompositionPipelineProcessingEvent>(input)); break;
                case StrategyWorkflowStage.RiskManagement:
                    await SendAsync(CreateProcessing<RiskManagementPipelineProcessingEvent>(input)); break;
            }
        }

        async ValueTask PublishCompletionAsync(PipelineStartInput input)
        {
            switch (stage)
            {
                case StrategyWorkflowStage.RegimeDiscovery:
                    await SendAsync(CreateCompletion<RegimeDiscoveryPipelineCompletedEvent>(input)); break;
                case StrategyWorkflowStage.MarketCondition:
                    await SendAsync(CreateCompletion<MarketConditionPipelineCompletedEvent>(input)); break;
                case StrategyWorkflowStage.TradeSelection:
                    await SendAsync(CreateCompletion<TradeSelectionPipelineCompletedEvent>(input)); break;
                case StrategyWorkflowStage.OrderComposition:
                    await SendAsync(CreateCompletion<OrderCompositionPipelineCompletedEvent>(input)); break;
                case StrategyWorkflowStage.RiskManagement:
                    await SendAsync(CreateCompletion<RiskManagementPipelineCompletedEvent>(input)); break;
            }
        }

        async ValueTask PublishFailureAsync(PipelineStartInput input)
        {
            switch (stage)
            {
                case StrategyWorkflowStage.RegimeDiscovery:
                    await SendAsync(CreateFailure<RegimeDiscoveryPipelineFailedEvent>(input)); break;
                case StrategyWorkflowStage.MarketCondition:
                    await SendAsync(CreateFailure<MarketConditionPipelineFailedEvent>(input)); break;
                case StrategyWorkflowStage.TradeSelection:
                    await SendAsync(CreateFailure<TradeSelectionPipelineFailedEvent>(input)); break;
                case StrategyWorkflowStage.OrderComposition:
                    await SendAsync(CreateFailure<OrderCompositionPipelineFailedEvent>(input)); break;
                case StrategyWorkflowStage.RiskManagement:
                    await SendAsync(CreateFailure<RiskManagementPipelineFailedEvent>(input)); break;
            }
        }

        async ValueTask SendAsync<TEvent>(TEvent domainEvent)
            where TEvent : class, IEvent<IntrinsicTimeStrategyWorkflowEntityId>
            => await producer.SendAsync<TEvent, IntrinsicTimeStrategyWorkflowEntityId>(
                domainEvent.Subject,
                domainEvent);

        TEvent CreateProcessing<TEvent>(PipelineStartInput input)
            where TEvent : class, IEvent<IntrinsicTimeStrategyWorkflowEntityId>, new()
        {
            var now = DateTime.UtcNow;
            return Create<TEvent>(input, EventVerb<TEvent>(),
                ("ProcessingAtUtc", now));
        }

        TEvent CreateCompletion<TEvent>(PipelineStartInput input)
            where TEvent : class, IEvent<IntrinsicTimeStrategyWorkflowEntityId>, new()
        {
            var now = DateTime.UtcNow;
            var payload = Encoding.UTF8.GetBytes($"{{\"stage\":\"{stage}\",\"revision\":{input.Revision}}}");
            var result = StrategyStageResultEnvelope.Create(
                Guid.NewGuid(),
                $"Dummy.{stage}",
                1,
                payload,
                now,
                now,
                "application/json");
            return Create<TEvent>(input, EventVerb<TEvent>(),
                ("Result", result),
                ("CompletedAtUtc", now));
        }

        TEvent CreateFailure<TEvent>(PipelineStartInput input)
            where TEvent : class, IEvent<IntrinsicTimeStrategyWorkflowEntityId>, new()
        {
            var now = DateTime.UtcNow;
            var domainEvent = new TEvent();
            Set(domainEvent, "Subject", Subject(EventVerb<TEvent>(), input.EntityId));
            Set(domainEvent, "EntityId", input.EntityId);
            Set(domainEvent, "Id", Guid.NewGuid());
            Set(domainEvent, "ErrorDate", now);
            Set(domainEvent, "CommandId", input.CommandId);
            Set(domainEvent, "EventSource", $"Dummy{stage}PipelineActor");
            Set(domainEvent, "ErrorMessage", $"Injected {stage} failure.");
            Set(domainEvent, "ErrorCode", 7303);
            Set(domainEvent, "ErrorType", ErrorType.System);
            Set(domainEvent, "ErrorData", "injected=true");
            Set(domainEvent, "ReceivedOn", now);
            Set(domainEvent, "AggregateId", input.WorkflowId.ToString());
            Set(domainEvent, "CommandName", $"Start{stage}PipelineCommand");
            Set(domainEvent, "CommandData", string.Empty);
            Set(domainEvent, "RouteTo", string.Empty);
            Set(domainEvent, "WorkflowId", input.WorkflowId);
            Set(domainEvent, "InputWorkflowRevision", input.Revision);
            Set(domainEvent, "CorrelationId", input.CorrelationId);
            Set(domainEvent, "CausationId", input.CommandId);
            Set(domainEvent, "PipelineStage", stage);
            return domainEvent;
        }

        TEvent Create<TEvent>(
            PipelineStartInput input,
            string verb,
            params (string Name, object Value)[] values)
            where TEvent : class, IEvent<IntrinsicTimeStrategyWorkflowEntityId>, new()
        {
            var now = DateTime.UtcNow;
            var domainEvent = new TEvent();
            Set(domainEvent, "Subject", Subject(verb, input.EntityId));
            Set(domainEvent, "Id", Guid.NewGuid());
            Set(domainEvent, "EntityId", input.EntityId);
            Set(domainEvent, "CommandId", input.CommandId);
            Set(domainEvent, "AggregateId", input.WorkflowId.ToString());
            Set(domainEvent, "EventSource", $"Dummy{stage}PipelineActor");
            Set(domainEvent, "ReceivedOn", now);
            Set(domainEvent, "WorkflowId", input.WorkflowId);
            Set(domainEvent, "InputWorkflowRevision", input.Revision);
            Set(domainEvent, "CorrelationId", input.CorrelationId);
            Set(domainEvent, "CausationId", input.CommandId);
            Set(domainEvent, "PipelineStage", stage);
            foreach (var (name, value) in values)
                Set(domainEvent, name, value);
            return domainEvent;
        }

        ActorSubject Subject(string verb, IntrinsicTimeStrategyWorkflowEntityId entityId)
            => new(ActorType.Realtime, RealtimeActorName(stage), verb, entityId.Format());

        static void Set(object target, string property, object? value)
            => EventInitHelper.SetProperty(target, property, value);

        static string EventVerb<TEvent>() where TEvent : class
            => (string)(typeof(TEvent).GetField("Verb")?.GetRawConstantValue()
                ?? throw new InvalidOperationException($"{typeof(TEvent).Name} does not define a public Verb constant."));
    }

    sealed class DummyRealtimeSourceActor(string name) : IActor
    {
        public ActorMailboxId Id { get; } = new(ActorType.Realtime, name);
        public IActorMailbox Mailbox { get; private set; } = default!;
        public bool IsRunning { get; private set; }

        public ValueTask StartAsync(IActorSupervisor supervisor)
        {
            Mailbox = supervisor.CreateMailbox(Id);
            IsRunning = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask StartAsync(IActorSupervisor supervisor, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return StartAsync(supervisor);
        }

        public ValueTask StopAsync()
        {
            IsRunning = false;
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return StopAsync();
        }

        public ValueTask HandleMessageAsync(IActorMessage message)
        {
            message.ReleasePayload();
            return ValueTask.CompletedTask;
        }

        public ValueTask HandleMessageAsync(IActorMessage message, ActorThreadId threadId)
            => HandleMessageAsync(message);
    }

    sealed class PipelineHold(Action release) : IDisposable
    {
        Action? _release = release;

        public void Release() => Interlocked.Exchange(ref _release, null)?.Invoke();

        public void Dispose() => Release();
    }

    readonly record struct PipelineStartInput(
        Guid CommandId,
        IntrinsicTimeStrategyWorkflowEntityId EntityId,
        StrategyWorkflowId WorkflowId,
        long Revision,
        Guid CorrelationId)
    {
        public static PipelineStartInput From(ExecuteRegimeDiscoveryPipelineCommand command)
            => New(command.CommandId, command.WorkflowEntityId, command.WorkflowId,
                command.InputWorkflowRevision, command.CorrelationId);
        public static PipelineStartInput From(StartMarketConditionPipelineCommand command)
            => New(command.CommandId, command.EntityId, command.WorkflowId, command.InputWorkflowRevision, command.CorrelationId);
        public static PipelineStartInput From(StartTradeSelectionPipelineCommand command)
            => New(command.CommandId, command.EntityId, command.WorkflowId, command.InputWorkflowRevision, command.CorrelationId);
        public static PipelineStartInput From(StartOrderCompositionPipelineCommand command)
            => New(command.CommandId, command.EntityId, command.WorkflowId, command.InputWorkflowRevision, command.CorrelationId);
        public static PipelineStartInput From(StartRiskManagementPipelineCommand command)
            => New(command.CommandId, command.EntityId, command.WorkflowId, command.InputWorkflowRevision, command.CorrelationId);

        static PipelineStartInput New(
            Guid commandId,
            IntrinsicTimeStrategyWorkflowEntityId entityId,
            StrategyWorkflowId workflowId,
            long revision,
            Guid correlationId)
            => new(commandId, entityId, workflowId, revision, correlationId);
    }

    static string CommandActorName(StrategyWorkflowStage stage) => stage switch
    {
        StrategyWorkflowStage.RegimeDiscovery => ExecuteRegimeDiscoveryPipelineCommand.Actor,
        StrategyWorkflowStage.MarketCondition => StartMarketConditionPipelineCommand.Actor,
        StrategyWorkflowStage.TradeSelection => StartTradeSelectionPipelineCommand.Actor,
        StrategyWorkflowStage.OrderComposition => StartOrderCompositionPipelineCommand.Actor,
        StrategyWorkflowStage.RiskManagement => StartRiskManagementPipelineCommand.Actor,
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    };

    static string RealtimeActorName(StrategyWorkflowStage stage) => stage switch
    {
        StrategyWorkflowStage.RegimeDiscovery => RegimeDiscoveryPipelineCompletedEvent.Actor,
        StrategyWorkflowStage.MarketCondition => MarketConditionPipelineCompletedEvent.Actor,
        StrategyWorkflowStage.TradeSelection => TradeSelectionPipelineCompletedEvent.Actor,
        StrategyWorkflowStage.OrderComposition => OrderCompositionPipelineCompletedEvent.Actor,
        StrategyWorkflowStage.RiskManagement => RiskManagementPipelineCompletedEvent.Actor,
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    };
}
