using System.Reflection;
using System.Collections;
using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RegimeDiscovery;

/// <summary>Qualifies the RD-10 Command actor dispatch and durable terminal reducer.</summary>
public sealed class RegimeDiscoveryCommandArchitectureTests
{
    /// <summary>Confirms the actor owns the mandatory parse, validation, and receive maps.</summary>
    [Fact]
    public void Command_actor_has_all_three_dispatch_maps()
    {
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var fields = typeof(RegimeDiscoveryCommandActor).GetFields(flags).Select(value => value.Name).ToArray();

        fields.Should().Contain(["_parseMap", "_validationMap", "_receiveMap"]);
        ReadMap("_parseMap").Contains(ExecuteRegimeDiscoveryPipelineCommand.Verb).Should().BeTrue();
        ReadMap("_validationMap").Contains(typeof(ExecuteRegimeDiscoveryPipelineCommand)).Should().BeTrue();
        ReadMap("_receiveMap").Contains(typeof(ExecuteRegimeDiscoveryPipelineCommand)).Should().BeTrue();
    }

    /// <summary>Confirms replay reconstructs the complete successful terminal state.</summary>
    [Fact]
    public void Completed_event_reconstructs_terminal_state()
    {
        var input = RegimeDiscoveryCalculationModelTests.CreateInput();
        var state = new RegimeDiscoveryCommandState();
        var domainEvent = new RegimeDiscoveryCalculationCompletedEvent
        {
            EntityId = input.EntityId,
            WorkflowId = input.WorkflowId,
            InputWorkflowRevision = 7,
            CommandId = input.ResultId,
            EventId = 41,
            ParameterPayloadSha256 = new string('A', 64),
            SignalSnapshotId = input.Snapshot.SnapshotId,
            Result = new TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model.RegimeDiscoveryResult
            {
                ResultId = input.ResultId,
                WorkflowId = input.WorkflowId,
                EntityId = input.EntityId,
                SignalSnapshotId = input.Snapshot.SnapshotId
            },
            ResultPayloadSha256 = new string('B', 64)
        };

        state.Apply(domainEvent, addEvent: false).Should().BeTrue();

        state.Status.Should().Be(RegimeDiscoveryCommandStatus.Completed);
        state.IsTerminal.Should().BeTrue();
        state.LastPersistedEventId.Should().Be(41);
        state.ResultPayloadSha256.Should().Be(new string('B', 64));
    }

    /// <summary>Confirms replay reconstructs the standard durable failure and structured reasons.</summary>
    [Fact]
    public void Failed_event_reconstructs_terminal_state()
    {
        var input = RegimeDiscoveryCalculationModelTests.CreateInput();
        var state = new RegimeDiscoveryCommandState();
        var domainEvent = new RegimeDiscoveryCalculationFailedEvent
        {
            EntityId = input.EntityId,
            WorkflowId = input.WorkflowId,
            InputWorkflowRevision = 7,
            CommandId = input.ResultId,
            EventId = 42,
            ParameterPayloadSha256 = new string('A', 64),
            SignalSnapshotId = input.Snapshot.SnapshotId,
            Failure = new() { ErrorCode = 23102, ErrorMessage = "missing required signal" },
            Reasons = [new() { Code = "RD.DATA.REQUIRED_MISSING" }]
        };

        state.Apply(domainEvent, addEvent: false).Should().BeTrue();

        state.Status.Should().Be(RegimeDiscoveryCommandStatus.Failed);
        state.Failure!.ErrorCode.Should().Be(23102);
        state.Reasons.Should().ContainSingle(value => value.Code == "RD.DATA.REQUIRED_MISSING");
    }

    /// <summary>Confirms public Regime terminal contracts belong only to the new stateless realtime translator.</summary>
    [Fact]
    public void Regime_realtime_actor_owns_exactly_completed_and_failed_routes()
    {
        RegimeDiscoveryPipelineCompletedEvent.Actor.Should().Be(RegimeDiscoveryPipelineRealtimeActor.ActorName);
        RegimeDiscoveryPipelineFailedEvent.Actor.Should().Be(RegimeDiscoveryPipelineRealtimeActor.ActorName);
        var routeFields = typeof(RegimeDiscoveryPipelineRealtimeActor)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(field => field.FieldType == typeof(ActorTypeId))
            .Select(field => field.Name)
            .ToArray();
        routeFields.Should().BeEquivalentTo("CompletedRoute", "FailedRoute");
        typeof(RegimeDiscoveryPipelineRealtimeActor).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Should().NotContain(field => field.FieldType == typeof(RegimeDiscoveryCommandState) ||
                                          typeof(Timer).IsAssignableFrom(field.FieldType));
    }

    /// <summary>Confirms duplicate public terminals map to stable guarded Workflow command identities.</summary>
    [Fact]
    public void Terminal_translation_is_deterministic_and_preserves_workflow_guards()
    {
        var input = RegimeDiscoveryCalculationModelTests.CreateInput();
        var entityId = input.EntityId;
        var sourceId = Guid.Parse("0198E212-3C00-7000-8000-000000000601");
        var completed = new RegimeDiscoveryPipelineCompletedEvent
        {
            Subject = new ActorSubject(ActorType.Realtime, RegimeDiscoveryPipelineCompletedEvent.Actor,
                RegimeDiscoveryPipelineCompletedEvent.Verb, entityId.Format()),
            Id = sourceId,
            EntityId = entityId,
            WorkflowId = input.WorkflowId,
            InputWorkflowRevision = 7,
            CorrelationId = Guid.NewGuid(),
            PipelineStage = StrategyWorkflowStage.RegimeDiscovery,
            Result = new StrategyStageResultEnvelope { ResultId = Guid.NewGuid() },
            CompletedAtUtc = DateTime.UtcNow
        };

        var first = RegimeDiscoveryPipelineRealtimeActor.CreateCompleteCommand(completed);
        var duplicate = RegimeDiscoveryPipelineRealtimeActor.CreateCompleteCommand(completed);

        first.CommandId.Should().Be(duplicate.CommandId);
        first.Subject.Name.Should().Be(CompleteRegimeDiscoveryCommand.Actor);
        first.Subject.Verb.Should().Be(CompleteRegimeDiscoveryCommand.Verb);
        first.EntityId.Should().Be(entityId);
        first.WorkflowId.Should().Be(input.WorkflowId);
        first.InputWorkflowRevision.Should().Be(7);
        first.SourceEventId.Should().Be(sourceId);
        first.CausationId.Should().Be(sourceId);
    }

    /// <summary>Confirms timeout failure translation retains the classification required by Workflow precedence.</summary>
    [Fact]
    public void Timeout_public_failure_maps_to_timeout_classified_workflow_failure()
    {
        var input = RegimeDiscoveryCalculationModelTests.CreateInput();
        var failed = new RegimeDiscoveryPipelineFailedEvent
        {
            Id = Guid.NewGuid(),
            EntityId = input.EntityId,
            WorkflowId = input.WorkflowId,
            InputWorkflowRevision = 3,
            ErrorCode = RegimeDiscoveryCalculationFailedEvent.TimeoutErrorCode,
            ErrorMessage = "deadline",
            ErrorDate = DateTime.UtcNow,
            PipelineStage = StrategyWorkflowStage.RegimeDiscovery
        };

        var command = RegimeDiscoveryPipelineRealtimeActor.CreateFailCommand(failed);

        command.Subject.Name.Should().Be(FailRegimeDiscoveryCommand.Actor);
        command.Subject.Verb.Should().Be(FailRegimeDiscoveryCommand.Verb);
        command.Failure.ErrorType.Should().Be("Timeout");
        command.Failure.ErrorCode.Should().Be(RegimeDiscoveryCalculationFailedEvent.TimeoutErrorCode);
    }

    static IDictionary ReadMap(string fieldName) => (IDictionary)typeof(RegimeDiscoveryCommandActor)
        .GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
}
