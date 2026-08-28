using System.Collections;
using System.Reflection;
using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RegimeDiscovery;

public sealed class RegimeDiscoveryFunctionArchitectureTests
{
    [Fact]
    public void Function_actor_has_parse_validation_and_exact_type_receive_maps()
    {
        var fields = typeof(RegimeDiscoveryFunctionActor)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Select(value => value.Name)
            .ToArray();

        fields.Should().Contain(["_parseMap", "_validationMap", "_receiveMap"]);
        ReadMap("_parseMap").Contains(ExecuteRegimeDiscoveryPipelineCommand.Verb).Should().BeTrue();
        ReadMap("_validationMap").Contains(typeof(ExecuteRegimeDiscoveryPipelineCommand)).Should().BeTrue();
        ReadMap("_receiveMap").Contains(typeof(ExecuteRegimeDiscoveryPipelineCommand)).Should().BeTrue();
        RegimeDiscoveryFunctionActor.ActorName.Should().Be(ExecuteRegimeDiscoveryPipelineCommand.Actor);
    }

    [Fact]
    public void Function_and_terminal_contracts_use_the_Function_boundary()
    {
        ExecuteRegimeDiscoveryPipelineCommand.Actor.Should().Be("RegimeDiscoveryPipelineFunction");
        RegimeDiscoveryPipelineCompletedEvent.Actor.Should().Be(ExecuteRegimeDiscoveryPipelineCommand.Actor);
        RegimeDiscoveryPipelineFailedEvent.Actor.Should().Be(ExecuteRegimeDiscoveryPipelineCommand.Actor);
        ActorType.Function.GetDeliveryType().Should().Be(ActorDeliveryType.NatsCore);
    }

    [Fact]
    public void Terminal_translation_is_deterministic_and_preserves_workflow_guards()
    {
        var command = RegimeDiscoveryFunctionExecutionTests.Command(DateTime.UtcNow.AddMinutes(2));
        var sourceId = Guid.Parse("0198E212-3C00-7000-8000-000000000601");
        var completed = new RegimeDiscoveryPipelineCompletedEvent
        {
            Id = sourceId,
            EntityId = command.WorkflowEntityId,
            WorkflowId = command.WorkflowId,
            InputWorkflowRevision = command.InputWorkflowRevision,
            CorrelationId = Guid.NewGuid(),
            PipelineStage = StrategyWorkflowStage.RegimeDiscovery,
            Result = new StrategyStageResultEnvelope { ResultId = Guid.NewGuid() },
            CompletedAtUtc = DateTime.UtcNow
        };

        var first = IntrinsicTimeStrategyWorkflowRealtimeActor.CreateCompleteCommand(completed);
        var duplicate = IntrinsicTimeStrategyWorkflowRealtimeActor.CreateCompleteCommand(completed);

        first.CommandId.Should().Be(duplicate.CommandId);
        first.Subject.Name.Should().Be(CompleteRegimeDiscoveryCommand.Actor);
        first.WorkflowId.Should().Be(command.WorkflowId);
        first.InputWorkflowRevision.Should().Be(command.InputWorkflowRevision);
        first.SourceEventId.Should().Be(sourceId);
    }

    [Fact]
    public void Timeout_failure_maps_to_timeout_classified_workflow_failure()
    {
        var request = RegimeDiscoveryFunctionExecutionTests.Command(DateTime.UtcNow.AddMinutes(2));
        var failed = new RegimeDiscoveryPipelineFailedEvent
        {
            Id = Guid.NewGuid(),
            EntityId = request.WorkflowEntityId,
            WorkflowId = request.WorkflowId,
            InputWorkflowRevision = request.InputWorkflowRevision,
            ErrorCode = 23103,
            ErrorMessage = "deadline",
            ErrorDate = DateTime.UtcNow,
            PipelineStage = StrategyWorkflowStage.RegimeDiscovery
        };

        var command = IntrinsicTimeStrategyWorkflowRealtimeActor.CreateFailCommand(failed);

        command.Subject.Name.Should().Be(FailRegimeDiscoveryCommand.Actor);
        command.Failure.ErrorType.Should().Be("Timeout");
        command.Failure.ErrorCode.Should().Be(23103);
    }

    static IDictionary ReadMap(string fieldName) => (IDictionary)typeof(RegimeDiscoveryFunctionActor)
        .GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
}
