using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.EventProjector;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Projection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime;

/// <summary>Qualifies ITSW-7 through ITSW-11 structural boundaries.</summary>
public sealed class IntrinsicTimeStrategyWorkflowGateQualificationTests
{
    /// <summary>Confirms cache writes are monotonic by workflow revision.</summary>
    [Fact]
    public void Projection_cache_rejects_older_revision()
    {
        var cache = new IntrinsicTimeStrategyWorkflowProjectionCache();
        var newer = Active(4);
        cache.Set(newer);
        cache.Set(Active(3));

        cache.TryGet(newer.WorkflowEntityId, out var actual).Should().BeTrue();
        actual!.WorkflowRevision.Should().Be(4);
    }

    /// <summary>Confirms live automatic triggering defaults to disabled.</summary>
    [Fact]
    public void Live_workflow_feature_defaults_to_disabled()
        => new IntrinsicTimeStrategyWorkflowOptions().Enabled.Should().BeFalse();

    /// <summary>Confirms ITSW-9 introduces no durable workflow Event actor.</summary>
    [Fact]
    public void Workflow_assembly_contains_no_durable_event_actor()
    {
        var eventActors = typeof(IntrinsicTimeStrategyWorkflowRealtimeActor).Assembly.GetTypes()
            .Where(type => type.Namespace?.Contains("Strategy.Workflow.IntrinsicTime", StringComparison.Ordinal) == true)
            .Where(type => type.Name.EndsWith("EventActor", StringComparison.Ordinal))
            .ToArray();

        eventActors.Should().BeEmpty();
    }

    /// <summary>Confirms only the ITI trigger uses global realtime route registration.</summary>
    [Fact]
    public void Realtime_actor_declares_only_the_external_trigger_route()
    {
        var route = (ActorTypeId)typeof(IntrinsicTimeStrategyWorkflowRealtimeActor)
            .GetField("TriggerRoute", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        route.ActorType.Should().Be(ActorType.Realtime);
        route.Verb.Should().Be("Generated");
        typeof(IntrinsicTimeStrategyWorkflowRealtimeActor)
            .GetField("ExternalRoutes", BindingFlags.NonPublic | BindingFlags.Static).Should().BeNull();
    }

    /// <summary>Confirms the workflow projector accepts only complete authoritative snapshots.</summary>
    [Fact]
    public void Workflow_projector_projects_only_state_update_snapshots()
    {
        var projector = (IntrinsicTimeStrategyWorkflowEventProjector)RuntimeHelpers.GetUninitializedObject(
            typeof(IntrinsicTimeStrategyWorkflowEventProjector));

        projector.ProjectedEventTypes.Should().Equal(typeof(WorkflowStrategyStateUpdatedEvent));
    }

    /// <summary>Confirms workflow admission is an Execute command in every exact dispatch map.</summary>
    [Fact]
    public void Workflow_command_maps_use_execute_admission_contract()
    {
        ReadMap(typeof(IntrinsicTimeStrategyWorkflowCommandActor), "_parseMap")
            .Contains(ExecuteIntrinsicTimeStrategyWorkflowCommand.Verb).Should().BeTrue();
        ReadMap(typeof(IntrinsicTimeStrategyWorkflowCommandActor), "_validationMap")
            .Contains(typeof(ExecuteIntrinsicTimeStrategyWorkflowCommand)).Should().BeTrue();
        ReadMap(typeof(IntrinsicTimeStrategyWorkflowCommandActor), "_receiveMap")
            .Contains(typeof(ExecuteIntrinsicTimeStrategyWorkflowCommand)).Should().BeTrue();
    }

    /// <summary>Confirms every receive entry has one command-named extension-handler class.</summary>
    [Fact]
    public void Workflow_receive_map_is_implemented_by_typed_command_extensions()
    {
        var receiveTypes = ReadMap(typeof(IntrinsicTimeStrategyWorkflowCommandActor), "_receiveMap")
            .Keys.Cast<Type>();
        var handlers = typeof(ExecuteIntrinsicTimeStrategyWorkflow).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(ExecuteIntrinsicTimeStrategyWorkflow).Namespace)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(method => method.Name == "Execute" && method.IsDefined(typeof(ExtensionAttribute), false))
            .ToArray();

        handlers.Select(method => method.GetParameters()[0].ParameterType)
            .Should().BeEquivalentTo(receiveTypes);
        foreach (var commandType in receiveTypes)
        {
            var expectedHandlerName = commandType.Name[..^"Command".Length];
            handlers.Should().ContainSingle(method =>
                method.GetParameters()[0].ParameterType == commandType &&
                method.DeclaringType!.Name == expectedHandlerName);
        }
        typeof(IntrinsicTimeStrategyWorkflowCommandActor)
            .GetMethod("ProcessWorkflowCommand", BindingFlags.Instance | BindingFlags.NonPublic)
            .Should().BeNull();
        typeof(ExecuteIntrinsicTimeStrategyWorkflow).Assembly
            .GetType("TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Extensions." +
                     "IntrinsicTimeStrategyWorkflowTransitions")
            .Should().BeNull();
    }

    /// <summary>Confirms every executable workflow stage has exactly one committed-state handler.</summary>
    [Fact]
    public void Workflow_pipeline_execution_map_covers_every_stage()
    {
        var map = ReadMap(typeof(IntrinsicTimeStrategyWorkflowRealtimeActor), "_pipelineExecutionMap");

        map.Keys.Cast<StrategyWorkflowStage>().Should().Equal(
            StrategyWorkflowStage.RegimeDiscovery,
            StrategyWorkflowStage.MarketCondition,
            StrategyWorkflowStage.TradeSelection,
            StrategyWorkflowStage.OrderComposition,
            StrategyWorkflowStage.RiskManagement);
    }

    /// <summary>Confirms only Started/Regime snapshots produce deterministic Regime execution commands.</summary>
    [Fact]
    public void Regime_execute_requires_committed_started_regime_snapshot_and_is_deterministic()
    {
        var started = IntrinsicTimeStrategyWorkflowCommandStateTests.CreateStartedSnapshotForQualification();

        var first = IntrinsicTimeStrategyWorkflowRealtimeActor.CreateRegimeExecute(started);
        var duplicate = IntrinsicTimeStrategyWorkflowRealtimeActor.CreateRegimeExecute(started);
        var terminal = IntrinsicTimeStrategyWorkflowRealtimeActor.CreateRegimeExecute(started with
        {
            State = started.State with { Status = WorkflowStrategyMachineStatus.Failed }
        });
        var laterStage = IntrinsicTimeStrategyWorkflowRealtimeActor.CreateRegimeExecute(started with
        {
            State = started.State with { CurrentStage = StrategyWorkflowStage.MarketCondition }
        });

        first.Should().NotBeNull();
        duplicate!.CommandId.Should().Be(first!.CommandId);
        duplicate.EntityId.Should().Be(first.EntityId);
        first.WorkflowView.Should().BeEquivalentTo(started.State);
        first.ExpiresAtUtc.Should().Be(started.State.ExpiresAtUtc);
        terminal.Should().BeNull();
        laterStage.Should().BeNull();
    }

    /// <summary>Confirms only committed Started/MarketCondition snapshots produce deterministic Function requests.</summary>
    [Fact]
    public void Market_condition_execute_requires_committed_stage_and_freezes_configuration()
    {
        var started = IntrinsicTimeStrategyWorkflowCommandStateTests.CreateStartedSnapshotForQualification();
        var marketCondition = started with
        {
            State = started.State with
            {
                CurrentStage = StrategyWorkflowStage.MarketCondition,
                WorkflowRevision = 2,
                UpdatedAtUtc = started.State.UpdatedAtUtc.AddSeconds(1)
            }
        };

        var first = IntrinsicTimeStrategyWorkflowRealtimeActor.CreateMarketConditionExecute(marketCondition);
        var duplicate = IntrinsicTimeStrategyWorkflowRealtimeActor.CreateMarketConditionExecute(marketCondition);
        var terminal = IntrinsicTimeStrategyWorkflowRealtimeActor.CreateMarketConditionExecute(marketCondition with
        {
            State = marketCondition.State with { Status = WorkflowStrategyMachineStatus.Completed }
        });

        first.Should().NotBeNull();
        duplicate!.CommandId.Should().Be(first!.CommandId);
        first.EntityId.WorkflowEntityId.Should().Be(marketCondition.EntityId);
        first.EntityId.WorkflowId.Should().Be(marketCondition.WorkflowId);
        first.ParameterSet.Should().BeEquivalentTo(marketCondition.State.MarketConditionParameterSet);
        first.ParameterPayloadSha256.Should().Be(marketCondition.State.MarketConditionParameterPayloadSha256);
        (first.ExpiresAtUtc <= marketCondition.State.ExpiresAtUtc).Should().BeTrue();
        terminal.Should().BeNull();
    }

    /// <summary>Confirms the typed query contract survives the default MessagePack transport boundary.</summary>
    [Fact]
    public void Workflow_query_contract_round_trips_through_messagepack()
    {
        var expected = new GetActiveIntrinsicTimeStrategyWorkflowQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetActiveIntrinsicTimeStrategyWorkflowQuery.Actor,
                GetActiveIntrinsicTimeStrategyWorkflowQuery.Verb, "IntrinsicTimeStrategy.ES.20260825.Daily"),
            EntityId = new ActorEntityId("IntrinsicTimeStrategy.ES.20260825.Daily"),
            WorkflowEntityId = "IntrinsicTimeStrategy.ES.20260825.Daily",
            MinimumWorkflowRevision = 7
        };

        var actual = MessagePackSerializer.Deserialize<GetActiveIntrinsicTimeStrategyWorkflowQuery>(
            MessagePackSerializer.Serialize(expected));

        actual.Subject.Should().Be(expected.Subject);
        actual.EntityId.Format().Should().Be(expected.EntityId.Format());
        actual.WorkflowEntityId.Should().Be(expected.WorkflowEntityId);
        actual.MinimumWorkflowRevision.Should().Be(7);
    }

    /// <summary>Confirms the operational query retains its stable workflow entity across transport.</summary>
    [Fact]
    public void Workflow_observation_query_round_trips_through_messagepack()
    {
        var entity = IntrinsicTimeStrategyWorkflowCommandStateTests
            .CreateStartedSnapshotForQualification().EntityId;
        var expected = new GetIntrinsicTimeStrategyWorkflowObservationQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetIntrinsicTimeStrategyWorkflowObservationQuery.Actor,
                GetIntrinsicTimeStrategyWorkflowObservationQuery.Verb, entity.Format()),
            EntityId = new ActorEntityId(entity.Format()),
            WorkflowEntity = entity
        };

        var actual = MessagePackSerializer.Deserialize<GetIntrinsicTimeStrategyWorkflowObservationQuery>(
            MessagePackSerializer.Serialize(expected));

        actual.Subject.Should().Be(expected.Subject);
        actual.EntityId.Format().Should().Be(entity.Format());
        actual.WorkflowEntity.Should().Be(entity);
    }

    /// <summary>Confirms deadline observation is derived without changing authoritative state.</summary>
    [Fact]
    public void Operational_view_derives_running_and_expired_without_mutation()
    {
        var snapshot = IntrinsicTimeStrategyWorkflowCommandStateTests.CreateStartedSnapshotForQualification().State;
        var original = MessagePackSerializer.Serialize(snapshot);

        var running = IntrinsicTimeStrategyWorkflowQueryActor.CreateObservation(
            snapshot.EntityId.Format(), snapshot, null, snapshot.ExpiresAtUtc.AddTicks(-1));
        var expired = IntrinsicTimeStrategyWorkflowQueryActor.CreateObservation(
            snapshot.EntityId.Format(), snapshot, null, snapshot.ExpiresAtUtc);

        running.OperationalStatus.Should().Be(IntrinsicTimeStrategyWorkflowOperationalStatus.Running);
        expired.OperationalStatus.Should().Be(IntrinsicTimeStrategyWorkflowOperationalStatus.ExpiredNotClosed);
        expired.IsOperationalIssue.Should().BeTrue();
        MessagePackSerializer.Serialize(snapshot).Should().Equal(original);
    }

    /// <summary>Confirms all terminal operations states and migration blocking are distinguishable.</summary>
    [Theory]
    [InlineData(WorkflowStrategyMachineStatus.Failed, IntrinsicTimeStrategyWorkflowOperationalStatus.Failed)]
    [InlineData(WorkflowStrategyMachineStatus.TimedOut, IntrinsicTimeStrategyWorkflowOperationalStatus.TimedOut)]
    [InlineData(WorkflowStrategyMachineStatus.Completed, IntrinsicTimeStrategyWorkflowOperationalStatus.Completed)]
    public void Operational_view_distinguishes_terminal_states(
        WorkflowStrategyMachineStatus machine,
        IntrinsicTimeStrategyWorkflowOperationalStatus operational)
        => IntrinsicTimeStrategyWorkflowQueryActor.Classify(machine, expired: false).Should().Be(operational);

    /// <summary>Confirms Regime terminal notification acceptance is correlated by workflow, revision, and source.</summary>
    [Fact]
    public void Operational_view_correlates_regime_terminal_to_workflow_snapshot()
    {
        var snapshot = IntrinsicTimeStrategyWorkflowCommandStateTests.CreateStartedSnapshotForQualification().State;
        var source = Guid.NewGuid();
        snapshot = snapshot with
        {
            RegimeDiscovery = snapshot.RegimeDiscovery with { SourceEventId = source }
        };
        var regime = new RegimeDiscoveryReadModel
        {
            WorkflowId = snapshot.WorkflowId,
            WorkflowEntityId = snapshot.EntityId.Format(),
            InputWorkflowRevision = snapshot.RegimeDiscovery.InputWorkflowRevision,
            SourceEventId = source,
            Status = "Completed"
        };

        var accepted = IntrinsicTimeStrategyWorkflowQueryActor.CreateObservation(
            snapshot.EntityId.Format(), snapshot, regime, snapshot.StartedAtUtc);
        var lost = IntrinsicTimeStrategyWorkflowQueryActor.CreateObservation(
            snapshot.EntityId.Format(), snapshot,
            regime with { SourceEventId = Guid.NewGuid() }, snapshot.ExpiresAtUtc);

        accepted.WorkflowAcceptedRegimeTerminal.Should().BeTrue();
        lost.WorkflowAcceptedRegimeTerminal.Should().BeFalse();
        lost.NotificationLossSuspected.Should().BeTrue();
    }

    /// <summary>Confirms the observation contract exposes and correlates the complete Market Condition projection.</summary>
    [Fact]
    public void Operational_view_exposes_market_condition_terminal_and_detects_orphans()
    {
        var snapshot = IntrinsicTimeStrategyWorkflowCommandStateTests.CreateStartedSnapshotForQualification().State;
        var source = Guid.NewGuid();
        snapshot = snapshot with
        {
            MarketCondition = snapshot.MarketCondition with
            {
                InputWorkflowRevision = snapshot.WorkflowRevision,
                SourceEventId = source
            }
        };
        var marketCondition = new MarketConditionReadModel
        {
            WorkflowId = snapshot.WorkflowId,
            WorkflowEntityId = snapshot.EntityId.Format(),
            InputWorkflowRevision = snapshot.WorkflowRevision,
            SourceEventId = source,
            Tradeability = MarketTradeability.NotTradeable,
            ConditionType = MarketConditionType.NoOpportunity,
            Direction = MarketConditionDirection.Bullish,
            Phase = MarketConditionPhase.Confirmed,
            Strength = 54m,
            Confidence = 0.64m,
            PrimaryReasonCode = MarketConditionReasonCodes.Strength,
            SummaryText = "Daily ES condition is NotTradeable"
        };

        var accepted = IntrinsicTimeStrategyWorkflowQueryActor.CreateObservation(
            snapshot.EntityId.Format(), snapshot, null, snapshot.StartedAtUtc, marketCondition);
        var orphan = IntrinsicTimeStrategyWorkflowQueryActor.CreateObservation(
            snapshot.EntityId.Format(), snapshot, null, snapshot.ExpiresAtUtc,
            marketCondition with { SourceEventId = Guid.NewGuid() });

        accepted.MarketConditionTerminal.Should().BeEquivalentTo(marketCondition);
        accepted.WorkflowAcceptedMarketConditionTerminal.Should().BeTrue();
        orphan.WorkflowAcceptedMarketConditionTerminal.Should().BeFalse();
        orphan.MarketConditionNotificationLossSuspected.Should().BeTrue();
        orphan.Diagnostic.Should().Be("MarketConditionTerminalNotAccepted");
    }

    /// <summary>Confirms migration-blocked streams have a distinct operational issue status.</summary>
    [Fact]
    public void Operational_view_distinguishes_migration_blocked_stream()
    {
        var result = IntrinsicTimeStrategyWorkflowQueryActor.MigrationBlocked(
            "entity", DateTime.UnixEpoch, "legacy stream");

        result.OperationalStatus.Should().Be(IntrinsicTimeStrategyWorkflowOperationalStatus.MigrationBlocked);
        result.IsOperationalIssue.Should().BeTrue();
        result.Diagnostic.Should().Contain("legacy");
    }

    static ActiveIntrinsicTimeStrategyWorkflowReadModel Active(long revision)
        => new("IntrinsicTimeStrategy.ES.20260825.Daily", default, "ES", new DateOnly(2026, 8, 25),
            Domain.MarketData.Analytics.Shared.TimeFrameType.Daily,
            Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.StrategyWorkflowStage.RegimeDiscovery,
            revision, revision, 1, ReadOnlyMemory<byte>.Empty,
            new DateTime(2026, 8, 25, 18, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 25, 18, 1, 0, DateTimeKind.Utc));

    static System.Collections.IDictionary ReadMap(Type owner, string field)
        => (System.Collections.IDictionary)owner
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
}
