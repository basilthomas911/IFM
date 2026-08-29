using System.Collections.Concurrent;
using System.Reflection;
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
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

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
    static readonly StrategyWorkflowStage[] DummyStages = Stages[2..];

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

            var tradeSelectionHolds = successEntities
                .Select(entity => pipelines.HoldAt(entity, StrategyWorkflowStage.TradeSelection))
                .ToArray();

            await Task.WhenAll(successEntities.Select(entity =>
                PublishTriggerAsync(publisher, entity.ItiSignalEntityId).AsTask()));

            var started = await Task.WhenAll(successEntities.Select(entity =>
                WaitForStatusAsync(entity, StrategyWorkflowStatus.Running)));
            started.Should().OnlyContain(model =>
                model.WorkflowRevision >= 1
                && (model.CurrentStage == StrategyWorkflowStage.RegimeDiscovery
                    || model.CurrentStage == StrategyWorkflowStage.MarketCondition),
                "the real Regime and Market Condition workers may advance before the observation query completes");

            foreach (var entity in successEntities)
            {
                var history = started.Single(model => model.WorkflowEntityId == entity.Format());
                await WaitForRegimeDiscoveryAsync(history.WorkflowId, "Completed");
                var advanced = await WaitForStageAsync(factory.Services, entity, StrategyWorkflowStage.TradeSelection, 3);
                await pipelines.WaitForStartCountAsync(entity, StrategyWorkflowStage.TradeSelection, 1);
                await AssertPersistedAdvancedSnapshotAsync(factory.Services, entity, advanced);
                var regime = await database.TradeDb.GetRegimeDiscoveryAsync(history.WorkflowId);
                regime.Should().NotBeNull();
                regime!.Status.Should().Be("Completed");
                regime.ResultPayload.Should().NotBeEmpty();
                pipelines.ProcessedStages(entity).Should().Contain(StrategyWorkflowStage.TradeSelection);
                pipelines.StartCount(entity, StrategyWorkflowStage.TradeSelection).Should().Be(1);
            }
            foreach (var hold in tradeSelectionHolds) hold.Release();
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
            using var hold = pipelines.HoldAt(entity, StrategyWorkflowStage.TradeSelection);
            await PublishTriggerAsync(publisher, entity.ItiSignalEntityId);
            var running = await WaitForStatusAsync(entity, StrategyWorkflowStatus.Running);
            await WaitForRegimeDiscoveryAsync(running.WorkflowId, "Completed");
            await WaitForStageAsync(factory.Services, entity, StrategyWorkflowStage.TradeSelection, 3);
            await pipelines.WaitForStartCountAsync(entity, StrategyWorkflowStage.TradeSelection, 1);

            await PublishTriggerAsync(publisher, entity.ItiSignalEntityId);
            await Task.Delay(500);

            var replayed = await LoadStateAsync(factory.Services, entity);
            replayed.HasActiveWorkflow.Should().BeTrue();
            replayed.ActiveWorkflow!.WorkflowId.Should().Be(running.WorkflowId);
            replayed.CurrentView!.WorkflowRevision.Should().BeGreaterThanOrEqualTo(3);
            pipelines.StartCount(entity, StrategyWorkflowStage.TradeSelection).Should().Be(1);
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
            var started = await WaitForFirstWorkflowAsync(entity);
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

    [Fact]
    public async Task Market_condition_projector_exception_fails_workflow_without_projection_state_or_continuation()
    {
        await using var factory = sourceFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(new IntrinsicTimeStrategyWorkflowOptions { Enabled = true });
                OverrideSimpleInjector<IFunctionProjector<MarketConditionPipelineCompletedEvent>>(
                    services, new ThrowingMarketConditionProjector());
            }));
        _ = factory.CreateClient();
        var supervisor = factory.Services.GetRequiredService<IActorSupervisor>();
        await using var pipelines = await DummyPipelineHarness.StartAsync(factory.Services, supervisor);
        var publisher = factory.Services.GetRequiredService<IActorProducer>();
        await publisher.StartAsync(new ActorMailboxId(ActorType.Realtime, "ItswMcProjectionFailurePublisher"));
        try
        {
            var entity = Entity($"ES-ITSW-{Guid.NewGuid():N}-MCPROJ", TimeFrameType.Daily);
            await PrepareRegimeDiscoveryAsync(factory.Services, [entity]);

            await PublishTriggerAsync(publisher, entity.ItiSignalEntityId);
            var started = await WaitForStatusAsync(entity, StrategyWorkflowStatus.Running);
            await WaitForTerminalAsync(entity, StrategyWorkflowStatus.Stopped,
                StrategyWorkflowOutcome.PipelineFailed);

            (await database.TradeDb.GetMarketConditionAsync(started.WorkflowId)).Should().BeNull();
            var state = await LoadStateAsync(factory.Services, entity);
            state.CurrentView!.MarketCondition.Failure!.ErrorData.Should().Be(MarketConditionReasonCodes.Projection);
            pipelines.StartCount(entity, StrategyWorkflowStage.TradeSelection).Should().Be(0);
        }
        finally
        {
            await publisher.StopAsync();
        }
    }

    [Fact]
    public async Task Market_condition_persistence_exception_leaves_observable_orphan_and_never_continues()
    {
        await using var factory = sourceFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(new IntrinsicTimeStrategyWorkflowOptions { Enabled = true });
                var repository = DispatchProxy.Create<
                    IEventSourceFunctionStateRepository<MarketConditionFunctionState,
                        ExecuteMarketConditionPipelineCommand>,
                    ThrowingMarketConditionStateRepositoryProxy>();
                OverrideSimpleInjector(services, repository);
            }));
        _ = factory.CreateClient();
        var supervisor = factory.Services.GetRequiredService<IActorSupervisor>();
        await using var pipelines = await DummyPipelineHarness.StartAsync(factory.Services, supervisor);
        var publisher = factory.Services.GetRequiredService<IActorProducer>();
        await publisher.StartAsync(new ActorMailboxId(ActorType.Realtime, "ItswMcPersistenceFailurePublisher"));
        try
        {
            var entity = Entity($"ES-ITSW-{Guid.NewGuid():N}-MCPERSIST", TimeFrameType.Daily);
            await PrepareRegimeDiscoveryAsync(factory.Services, [entity]);

            await PublishTriggerAsync(publisher, entity.ItiSignalEntityId);
            var started = await WaitForStatusAsync(entity, StrategyWorkflowStatus.Running);
            await WaitForTerminalAsync(entity, StrategyWorkflowStatus.Stopped,
                StrategyWorkflowOutcome.PipelineFailed);

            (await database.TradeDb.GetMarketConditionAsync(started.WorkflowId)).Should().NotBeNull(
                "projection precedes Function-state persistence and is intentionally detectable as an orphan");
            var state = await LoadStateAsync(factory.Services, entity);
            state.CurrentView!.MarketCondition.Failure!.ErrorData.Should().Be(MarketConditionReasonCodes.Persistence);
            pipelines.StartCount(entity, StrategyWorkflowStage.TradeSelection).Should().Be(0);

            var queryProducer = factory.Services.GetRequiredService<IActorProducer>();
            await queryProducer.StartAsync(new ActorMailboxId(ActorType.Query, "ItswMcOrphanQueryProbe"));
            try
            {
                var observation = await new IntrinsicTimeStrategyWorkflowQueryApi(queryProducer)
                    .GetObservationAsync(entity);
                observation.Success.Should().BeTrue();
                observation.Value!.MarketConditionTerminal.Should().NotBeNull();
                observation.Value.WorkflowAcceptedMarketConditionTerminal.Should().BeFalse();
                observation.Value.MarketConditionNotificationLossSuspected.Should().BeTrue();
                observation.Value.IsOperationalIssue.Should().BeTrue();
            }
            finally
            {
                await queryProducer.StopAsync();
            }
        }
        finally
        {
            await publisher.StopAsync();
        }
    }

    [Fact]
    public async Task Market_condition_no_trade_is_projected_terminal_and_never_dispatches_trade_selection()
    {
        await using var factory = sourceFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton(new IntrinsicTimeStrategyWorkflowOptions { Enabled = true })));
        _ = factory.CreateClient();
        var supervisor = factory.Services.GetRequiredService<IActorSupervisor>();
        await using var pipelines = await DummyPipelineHarness.StartAsync(factory.Services, supervisor);
        var publisher = factory.Services.GetRequiredService<IActorProducer>();
        await publisher.StartAsync(new ActorMailboxId(ActorType.Realtime, "ItswMcNoTradePublisher"));
        try
        {
            var entity = Entity($"ES-ITSW-{Guid.NewGuid():N}-MCNOTRADE", TimeFrameType.Daily);
            await PrepareRegimeDiscoveryAsync(factory.Services, [entity]);
            var cache = factory.Services.GetRequiredService<IMarketConditionSnapshotCache>();
            cache.Clear();
            var blocked = HealthyMarketConditionSource();
            cache.Upsert(1, "ES", TimeFrameType.Daily, blocked with
            {
                SessionState = blocked.SessionState with { Status = MarketSessionStatus.Closed, IsEntryWindow = false }
            });

            await PublishTriggerAsync(publisher, entity.ItiSignalEntityId);
            var started = await WaitForStatusAsync(entity, StrategyWorkflowStatus.Running);
            await WaitForTerminalAsync(entity, StrategyWorkflowStatus.Completed, StrategyWorkflowOutcome.NoTrade);

            var projected = await database.TradeDb.GetMarketConditionAsync(started.WorkflowId);
            projected.Should().NotBeNull();
            MessagePackSerializer.Deserialize<MarketConditionResult>(projected!.ResultPayload)
                .Tradeability.Should().Be(MarketTradeability.NotTradeable);
            var state = await LoadStateAsync(factory.Services, entity);
            state.CurrentView!.MarketCondition.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Completed);
            state.CurrentView.MarketCondition.ContinuationDecision.Should()
                .Be(StrategyWorkflowContinuationDecision.Stop);
            pipelines.StartCount(entity, StrategyWorkflowStage.TradeSelection).Should().Be(0);
        }
        finally
        {
            await publisher.StopAsync();
        }
    }

    [Fact]
    public async Task Market_condition_timeout_is_terminal_unprojected_and_never_dispatches_trade_selection()
    {
        await using var factory = sourceFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(new IntrinsicTimeStrategyWorkflowOptions { Enabled = true });
                services.RemoveAll<IMarketConditionSnapshotProvider>();
                services.AddSingleton<IMarketConditionSnapshotProvider>(new BlockingMarketConditionSnapshotProvider());
            }));
        _ = factory.CreateClient();
        var supervisor = factory.Services.GetRequiredService<IActorSupervisor>();
        await using var pipelines = await DummyPipelineHarness.StartAsync(factory.Services, supervisor);
        var publisher = factory.Services.GetRequiredService<IActorProducer>();
        await publisher.StartAsync(new ActorMailboxId(ActorType.Realtime, "ItswMcTimeoutPublisher"));
        try
        {
            var entity = Entity($"ES-ITSW-{Guid.NewGuid():N}-MCTIMEOUT", TimeFrameType.Daily);
            await PrepareRegimeDiscoveryAsync(factory.Services, [entity]);

            await PublishTriggerAsync(publisher, entity.ItiSignalEntityId);
            var started = await WaitForStatusAsync(entity, StrategyWorkflowStatus.Running);
            await WaitForTerminalAsync(entity, StrategyWorkflowStatus.Stopped, StrategyWorkflowOutcome.TimedOut);

            (await database.TradeDb.GetMarketConditionAsync(started.WorkflowId)).Should().BeNull();
            var state = await LoadStateAsync(factory.Services, entity);
            state.CurrentView!.MarketCondition.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.TimedOut);
            state.CurrentView.MarketCondition.Failure!.ErrorData.Should().Be(MarketConditionReasonCodes.Timeout);
            pipelines.StartCount(entity, StrategyWorkflowStage.TradeSelection).Should().Be(0);
        }
        finally
        {
            await publisher.StopAsync();
        }
    }

    [Fact]
    public async Task Market_condition_matching_retry_survives_host_restart_without_recapture_or_redispatch()
    {
        var firstProvider = new RecordingMarketConditionSnapshotProvider();
        ExecuteMarketConditionPipelineCommand command;
        Guid completedId;
        var entity = Entity($"ES-ITSW-{Guid.NewGuid():N}-MCRETRY", TimeFrameType.Daily);
        await using (var firstFactory = sourceFactory.WithWebHostBuilder(builder =>
                         builder.ConfigureServices(services =>
                         {
                             services.AddSingleton(new IntrinsicTimeStrategyWorkflowOptions { Enabled = true });
                             services.RemoveAll<IMarketConditionSnapshotProvider>();
                             services.AddSingleton<IMarketConditionSnapshotProvider>(firstProvider);
                         })))
        {
            _ = firstFactory.CreateClient();
            var supervisor = firstFactory.Services.GetRequiredService<IActorSupervisor>();
            await using var pipelines = await DummyPipelineHarness.StartAsync(firstFactory.Services, supervisor);
            var publisher = firstFactory.Services.GetRequiredService<IActorProducer>();
            await publisher.StartAsync(new ActorMailboxId(ActorType.Realtime, "ItswMcRetryPublisher"));
            try
            {
                await PrepareRegimeDiscoveryAsync(firstFactory.Services, [entity]);
                using var hold = pipelines.HoldAt(entity, StrategyWorkflowStage.TradeSelection);
                await PublishTriggerAsync(publisher, entity.ItiSignalEntityId);
                await WaitForStageAsync(firstFactory.Services, entity, StrategyWorkflowStage.TradeSelection, 3);
                command = await firstProvider.Command.Task.WaitAsync(ScenarioTimeout);

                var retry = await publisher.RequestFunctionAsync<ExecuteMarketConditionPipelineCommand,
                    MarketConditionExecutionEntityId,
                    FunctionResult<MarketConditionPipelineCompletedEvent, MarketConditionPipelineFailedEvent>>(
                    command.Subject, command, command.EntityId);
                retry.Success.Should().BeTrue();
                retry.Value!.IsCompleted.Should().BeTrue();
                completedId = retry.Value.Completed!.Id;
                firstProvider.Calls.Should().Be(1);
                pipelines.StartCount(entity, StrategyWorkflowStage.TradeSelection).Should().Be(1);
            }
            finally
            {
                await publisher.StopAsync();
            }
        }

        var restartedProvider = new RecordingMarketConditionSnapshotProvider();
        await using var restartedFactory = sourceFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(new IntrinsicTimeStrategyWorkflowOptions { Enabled = true });
                services.RemoveAll<IMarketConditionSnapshotProvider>();
                services.AddSingleton<IMarketConditionSnapshotProvider>(restartedProvider);
            }));
        _ = restartedFactory.CreateClient();
        var restartedPublisher = restartedFactory.Services.GetRequiredService<IActorProducer>();
        await restartedPublisher.StartAsync(new ActorMailboxId(ActorType.Function, "ItswMcRestartRetryPublisher"));
        try
        {
            var retry = await restartedPublisher.RequestFunctionAsync<ExecuteMarketConditionPipelineCommand,
                MarketConditionExecutionEntityId,
                FunctionResult<MarketConditionPipelineCompletedEvent, MarketConditionPipelineFailedEvent>>(
                command.Subject, command, command.EntityId);
            retry.Success.Should().BeTrue();
            retry.Value!.Completed!.Id.Should().Be(completedId);
            restartedProvider.Calls.Should().Be(0,
                "completed Function state must be reconstructed before any source capture after restart");
            var state = await LoadStateAsync(restartedFactory.Services, entity);
            state.CurrentView!.CurrentStage.Should().Be(StrategyWorkflowStage.TradeSelection);
            state.CurrentView.WorkflowRevision.Should().Be(3);
        }
        finally
        {
            await restartedPublisher.StopAsync();
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
        projectedState.CurrentStage.Should().Be(StrategyWorkflowStage.TradeSelection);
        projectedState.WorkflowRevision.Should().Be(history.WorkflowRevision);

        var marketCondition = await database.TradeDb.GetMarketConditionAsync(history.WorkflowId);
        marketCondition.Should().NotBeNull("the real Market Condition Function projects its completed result");
        marketCondition!.WorkflowEntityId.Should().Be(entityId.Format());
        marketCondition.InputWorkflowRevision.Should().Be(projectedState.MarketCondition.InputWorkflowRevision);
        marketCondition.SourceEventId.Should().Be(projectedState.MarketCondition.SourceEventId);
        marketCondition.ResultPayload.Should().NotBeEmpty();
        var marketConditionResult = MessagePackSerializer.Deserialize<MarketConditionResult>(
            marketCondition.ResultPayload);
        marketConditionResult.ResultId.Should().Be(marketCondition.SourceEventId);
        marketConditionResult.SnapshotSha256.Should().Be(marketCondition.SnapshotSha256);
        marketConditionResult.Tradeability.Should().Be(MarketTradeability.Tradeable);

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
            observation.Value.WorkflowAcceptedMarketConditionTerminal.Should().BeTrue();
            observation.Value.MarketConditionTerminal.Should().NotBeNull();
            observation.Value.MarketConditionTerminal!.WorkflowId.Should().Be(marketCondition.WorkflowId);
            observation.Value.MarketConditionTerminal.WorkflowEntityId.Should().Be(marketCondition.WorkflowEntityId);
            observation.Value.MarketConditionTerminal.InputWorkflowRevision
                .Should().Be(marketCondition.InputWorkflowRevision);
            observation.Value.MarketConditionTerminal.SourceEventId.Should().Be(marketCondition.SourceEventId);
            observation.Value.MarketConditionTerminal.SnapshotSha256.Should().Be(marketCondition.SnapshotSha256);
            observation.Value.MarketConditionTerminal.ResultPayload.Span
                .SequenceEqual(marketCondition.ResultPayload.Span).Should().BeTrue();
        }
        finally
        {
            await queryProducer.StopAsync();
        }

        projectedState.RegimeDiscovery.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Completed);
        projectedState.MarketCondition.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Completed);
        projectedState.TradeSelection.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Processing);
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
        var lastObserved = "no ScyllaDB workflow history row";
        do
        {
            var rows = await database.TradeDb.GetIntrinsicTimeStrategyWorkflowsByEntityAsync(
                entityId.Format(), DateTime.MaxValue, 10);
            if (rows.Count > 0)
            {
                lastObserved = $"{rows.First().Status}/{rows.First().Outcome} at " +
                    $"{rows.First().CurrentStage} revision {rows.First().WorkflowRevision}";
                if (rows.First().Outcome != StrategyWorkflowOutcome.None)
                {
                    var detail = await database.TradeDb.GetIntrinsicTimeStrategyWorkflowAsync(rows.First().WorkflowId);
                    if (detail is not null)
                    {
                        var view = MessagePackSerializer.Deserialize<IntrinsicTimeStrategyWorkflowView>(detail.StatePayload);
                        var failure = view.MarketCondition.Failure;
                        if (failure is not null)
                            lastObserved += $"; Market Condition failure: {failure.ErrorType}/{failure.ErrorData}: {failure.ErrorMessage}";
                    }
                }
            }
            var match = rows.FirstOrDefault(row => row.Status == status);
            if (match is not null)
                return match;
            await Task.Delay(100);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException(
            $"Workflow {entityId.Format()} did not reach {status} within {ScenarioTimeout}; " +
            $"last observed: {lastObserved}.");
    }

    async Task<IntrinsicTimeStrategyWorkflowHistoryReadModel> WaitForFirstWorkflowAsync(
        IntrinsicTimeStrategyWorkflowEntityId entityId)
    {
        var deadline = DateTime.UtcNow + ScenarioTimeout;
        do
        {
            var rows = await database.TradeDb.GetIntrinsicTimeStrategyWorkflowsByEntityAsync(
                entityId.Format(), DateTime.MaxValue, 10);
            if (rows.Count > 0)
                return rows.First();
            await Task.Delay(100);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException(
            $"Workflow {entityId.Format()} did not project its first durable history row within {ScenarioTimeout}.");
    }

    async Task<IntrinsicTimeStrategyWorkflowHistoryReadModel> WaitForStageAsync(
        IServiceProvider services,
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        StrategyWorkflowStage stage,
        long minimumRevision)
    {
        var deadline = DateTime.UtcNow + ScenarioTimeout;
        var lastObserved = "no ScyllaDB workflow history row";
        do
        {
            var rows = await database.TradeDb.GetIntrinsicTimeStrategyWorkflowsByEntityAsync(
                entityId.Format(), DateTime.MaxValue, 10);
            if (rows.Count > 0)
            {
                lastObserved = $"{rows.First().Status}/{rows.First().Outcome} at " +
                    $"{rows.First().CurrentStage} revision {rows.First().WorkflowRevision}";
                if (rows.First().Outcome != StrategyWorkflowOutcome.None)
                {
                    var detail = await database.TradeDb.GetIntrinsicTimeStrategyWorkflowAsync(rows.First().WorkflowId);
                    if (detail is not null)
                    {
                        var view = MessagePackSerializer.Deserialize<IntrinsicTimeStrategyWorkflowView>(detail.StatePayload);
                        var failure = view.MarketCondition.Failure;
                        if (failure is not null)
                            lastObserved += $"; Market Condition failure: {failure.ErrorType}/{failure.ErrorData}: {failure.ErrorMessage}";
                        var resolved = await services.GetRequiredService<IConfigurationDbContext>()
                            .GetMarketConditionAsync(view.MarketConditionParameterSet.ParameterSetId,
                                view.MarketConditionParameterSet.Version);
                        if (resolved is not null && !string.Equals(resolved.PayloadSha256,
                                MarketConditionParameterPayload.ComputeSha256(view.MarketConditionParameterSet),
                                StringComparison.OrdinalIgnoreCase))
                            lastObserved += $"; stored config: {resolved.PayloadJson}; frozen config: " +
                                MarketConditionParameterPayload.Serialize(view.MarketConditionParameterSet);
                    }
                }
            }
            var match = rows.FirstOrDefault(row =>
                row.CurrentStage == stage && row.WorkflowRevision >= minimumRevision);
            if (match is not null)
                return match;
            await Task.Delay(100);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException(
            $"Workflow {entityId.Format()} did not reach {stage} revision {minimumRevision} within {ScenarioTimeout}; " +
            $"last observed: {lastObserved}.");
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
                IntrinsicPrice = 6500d,
                IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend,
                IntrinsicTimeMode = IntrinsicTimeModeType.Trending,
                BandLevel = 1d,
                ReversalLevel = 0d
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
        await services.GetRequiredService<TradeSchemaDb>().CreateAllAsync();
        var configuration = services.GetRequiredService<IConfigurationDbContext>();
        var parameterSets = new Dictionary<TimeFrameType, RegimeDiscoveryParameterSet>();
        foreach (var horizon in values.Select(value => value.ItiSignalEntityId.TimePeriod).Distinct())
        {
            await RetirePublishedMarketConditionFixturesAsync(configuration, horizon);
            var parameterSet = RegimeDiscoveryParameterSet.CreateDefault(
                Guid.CreateVersion7(), Guid.CreateVersion7(), horizon);
            await configuration.InsertRegimeDiscoveryDraftAsync(
                parameterSet, "RD-16 integration qualification", "rd-16-integration");
            await configuration.PublishAsync(
                StrategyParameterSetKind.RegimeDiscovery,
                parameterSet.ParameterSetId,
                parameterSet.Version,
                DateTime.UtcNow.AddMinutes(-1));
            var marketCondition = MarketConditionParameterSet.CreateDefault(
                Guid.CreateVersion7(), parameterSet.StrategyParameterSetId, 1, horizon,
                strategyVersion: parameterSet.StrategyParameterSetVersion);
            await configuration.InsertMarketConditionDraftAsync(
                marketCondition, "MC integration qualification", "mc-integration");
            await configuration.PublishAsync(
                StrategyParameterSetKind.MarketCondition,
                marketCondition.ParameterSetId,
                marketCondition.Version,
                DateTime.UtcNow.AddMinutes(-1));
            parameterSets.Add(horizon, parameterSet);
        }

        var marketCache = services.GetRequiredService<IMarketConditionSnapshotCache>();
        marketCache.Clear();
        foreach (var horizon in values.Select(value => value.ItiSignalEntityId.TimePeriod).Distinct())
            marketCache.Upsert(1, "ES", horizon, HealthyMarketConditionSource());

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

    static async Task RetirePublishedMarketConditionFixturesAsync(
        IConfigurationDbContext configuration,
        TimeFrameType horizon)
    {
        await configuration.Use(
                $"{nameof(IntrinsicTimeStrategyWorkflowRuntimeIntegrationTests)}.{nameof(RetirePublishedMarketConditionFixturesAsync)}",
                """
                UPDATE reference_configuration.market_condition_parameter_set
                SET status = $1, retired_at_utc = $2
                WHERE status = $3
                  AND CAST(payload_json ->> 'FundId' AS integer) = $4
                  AND payload_json ->> 'InstrumentRoot' = $5
                  AND CAST(payload_json ->> 'TargetHorizon' AS smallint) = $6;
                """)
            .SetParameters(new RetirePublishedMarketConditionFixtures(
                (short)ConfigurationParameterSetStatus.Retired,
                DateTime.UtcNow,
                (short)ConfigurationParameterSetStatus.Published,
                1,
                "ES",
                (short)horizon))
            .ExecuteCommandAsync();
    }

    readonly record struct RetirePublishedMarketConditionFixtures(
        short RetiredStatus,
        DateTime RetiredAtUtc,
        short PublishedStatus,
        int FundId,
        string InstrumentRoot,
        short TargetHorizon) : IBindValue
    {
        public object Bind() => Values(
            Smallint(RetiredStatus), TimestampTz(RetiredAtUtc), Smallint(PublishedStatus),
            Integer(FundId), Text(InstrumentRoot), Smallint(TargetHorizon));
    }

    static MarketConditionSnapshot HealthyMarketConditionSource()
    {
        var now = DateTime.UtcNow;
        var source = new MarketSourceObservation
        {
            SourceId = "source", SourceTimestampUtc = now, ReceivedAtUtc = now, SequenceId = 1,
            Availability = MarketSourceAvailability.Available, Validity = MarketSourceValidity.Valid
        };
        return new MarketConditionSnapshot
        {
            MarketDataAsOfUtc = now,
            FuturesQuote = new MarketConditionFuturesQuote
            {
                BidPrice = 6500m, AskPrice = 6500.25m, BidSize = 20m, AskSize = 20m, LastPrice = 6500m,
                QuoteObservation = source with { SourceId = "futures-quote" },
                TradeObservation = source with { SourceId = "futures-trade" }
            },
            OptionChainQuality = new MarketConditionOptionChainQuality
            {
                CandidateContractCount = 24, ValidQuoteCount = 23, EligibleExpirationCount = 2,
                HasCalls = true, HasPuts = true, ValidQuoteCoverage = 0.96m,
                MedianRelativeSpread = 0.05m, P90RelativeSpread = 0.10m,
                MedianBidSize = 5m, MedianAskSize = 5m, UnderlyingMismatch = 0.0001m,
                Observation = source with { SourceId = "option-chain" }
            },
            SessionState = new MarketConditionSessionState
            {
                Status = MarketSessionStatus.Open, IsEntryWindow = true,
                ExchangeLocalTime = new TimeSpan(12, 0, 0), ExchangeLocalWeekday = DayOfWeek.Tuesday,
                Observation = source with { SourceId = "session" }
            },
            EventRiskState = new MarketConditionEventRiskState
            {
                Status = MarketEventRiskStatus.Clear,
                Observation = source with { SourceId = "event-risk" }
            },
            VolatilityShockState = new MarketConditionVolatilityShockState
            {
                Observation = source with { SourceId = "volatility" }
            },
            OperationalHealth =
            [
                Health("PrimaryFuturesFeed", source), Health("FuturesOptionFeed", source),
                Health("LatestValueCache", source), Health("IbkrSession", source)
            ],
            WorkflowEligibility = new MarketConditionWorkflowEligibilityState
            {
                EntriesEnabled = true, RegimeProducedAtUtc = now, TriggerProducedAtUtc = now
            },
            DataQualityItems = [source with { SourceId = "quality" }]
        };
    }

    static MarketConditionOperationalHealthItem Health(string id, MarketSourceObservation source)
        => new()
        {
            SourceId = id,
            Status = MarketOperationalStatus.Healthy,
            Observation = source with { SourceId = id }
        };

    sealed class ThrowingMarketConditionProjector : IFunctionProjector<MarketConditionPipelineCompletedEvent>
    {
        public ValueTask ProjectAsync(MarketConditionPipelineCompletedEvent completedEvent,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new InvalidOperationException("Injected Market Condition projection failure."));
    }

    sealed class BlockingMarketConditionSnapshotProvider : IMarketConditionSnapshotProvider
    {
        public async Task<MarketConditionSnapshotCaptureResult> CaptureAsync(
            ExecuteMarketConditionPipelineCommand command, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The cancellation-bound delay unexpectedly completed.");
        }
    }

    sealed class RecordingMarketConditionSnapshotProvider : IMarketConditionSnapshotProvider
    {
        public TaskCompletionSource<ExecuteMarketConditionPipelineCommand> Command { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }

        public Task<MarketConditionSnapshotCaptureResult> CaptureAsync(
            ExecuteMarketConditionPipelineCommand command, CancellationToken cancellationToken = default)
        {
            Calls++;
            Command.TrySetResult(command);
            var source = HealthyMarketConditionSource();
            var at = DateTime.UtcNow;
            MarketSourceObservation At(MarketSourceObservation value) => value with
            {
                SourceTimestampUtc = at, ReceivedAtUtc = at, AgeSeconds = 0m
            };
            var snapshot = MarketConditionSnapshotHash.Seal(source with
            {
                SnapshotId = Guid.CreateVersion7(), WorkflowId = command.WorkflowId,
                EntityId = command.WorkflowEntityId, FundId = command.FundId,
                InstrumentRoot = command.InstrumentRoot, TargetHorizon = command.TargetHorizon,
                EvaluationTimestampUtc = at, MarketDataAsOfUtc = at,
                FuturesQuote = source.FuturesQuote with
                {
                    QuoteObservation = At(source.FuturesQuote.QuoteObservation),
                    TradeObservation = At(source.FuturesQuote.TradeObservation)
                },
                OptionChainQuality = source.OptionChainQuality with
                    { Observation = At(source.OptionChainQuality.Observation) },
                SessionState = source.SessionState with { Observation = At(source.SessionState.Observation) },
                EventRiskState = source.EventRiskState with { Observation = At(source.EventRiskState.Observation) },
                VolatilityShockState = source.VolatilityShockState with
                    { Observation = At(source.VolatilityShockState.Observation) },
                OperationalHealth = source.OperationalHealth.Select(x => x with
                    { Observation = At(x.Observation) }).ToArray(),
                WorkflowEligibility = new MarketConditionWorkflowEligibilityState
                {
                    EntriesEnabled = true,
                    RegimeProducedAtUtc = command.WorkflowView.RegimeDiscovery.CompletedAtUtc ?? at,
                    TriggerProducedAtUtc = command.TriggerEvent.CreatedOn
                },
                DataQualityItems = source.DataQualityItems.Select(At).ToArray()
            });
            return Task.FromResult(new MarketConditionSnapshotCaptureResult
            {
                Outcome = MarketConditionCaptureOutcome.Success, Snapshot = snapshot
            });
        }
    }

    class ThrowingMarketConditionStateRepositoryProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            nameof(IEventSourceFunctionStateRepository<MarketConditionFunctionState,
                ExecuteMarketConditionPipelineCommand>.LoadStateAsync) =>
                ValueTask.FromResult(new MarketConditionFunctionState()),
            nameof(IEventSourceFunctionStateRepository<MarketConditionFunctionState,
                ExecuteMarketConditionPipelineCommand>.SaveCompletedStateAsync) =>
                ValueTask.FromException(new InvalidOperationException(
                    "Injected Market Condition persistence failure.")),
            _ => throw new NotSupportedException(targetMethod?.Name)
        };
    }

    static void OverrideSimpleInjector<TService>(IServiceCollection services, TService instance)
        where TService : class
    {
        var container = services
            .Single(descriptor => descriptor.ServiceType == typeof(SimpleInjector.Container))
            .ImplementationInstance.Should().BeOfType<SimpleInjector.Container>().Subject;
        container.Options.AllowOverridingRegistrations = true;
        container.RegisterInstance(instance);
        container.Options.AllowOverridingRegistrations = false;
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
        public static PipelineStartInput From(ExecuteMarketConditionPipelineCommand command)
            => New(command.CommandId, command.WorkflowEntityId, command.WorkflowId,
                command.InputWorkflowRevision, command.CorrelationId);
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
        StrategyWorkflowStage.MarketCondition => ExecuteMarketConditionPipelineCommand.Actor,
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
