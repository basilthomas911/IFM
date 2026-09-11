using FluentAssertions;
using MessagePack;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RegimeDiscovery;

public sealed class RegimeDiscoveryFunctionEventMapTests
{
    [Fact]
    public void Completion_mapping_preserves_execution_metadata_and_serialized_result()
    {
        var command = RegimeDiscoveryFunctionValidationTests.ValidCommand();
        var result = Result(command);
        var snapshotId = Guid.NewGuid();
        var terminal = RegimeDiscoveryFunctionActor.MapEvent(
            new(typeof(RegimeDiscoveryPipelineCompletedEvent), command,
                new RegimeDiscoveryExecutionCompleted(result, snapshotId, 9)), TimeProvider.System);
        var completed = terminal.Completed!;
        completed.CommandId.Should().Be(command.CommandId);
        completed.WorkflowId.Should().Be(command.WorkflowId);
        completed.InputWorkflowRevision.Should().Be(command.InputWorkflowRevision);
        completed.CorrelationId.Should().Be(command.CorrelationId);
        completed.CausationId.Should().Be(command.CausationId);
        completed.ParameterPayloadSha256.Should().Be(command.ParameterPayloadSha256);
        completed.SignalSnapshotId.Should().Be(snapshotId);
        completed.Subject.EntityId.Should().Be(command.EntityId.Format());
        completed.Result.ResultId.Should().Be(result.ResultId);
        completed.Result.SchemaVersion.Should().Be(RegimeDiscoveryResult.CurrentSchemaVersion);
        completed.Result.ReadRegimeResult().Should().BeEquivalentTo(result);
        completed.Result.Payload.IsEmpty.Should().BeTrue();
        completed.Result.RegimeResult.Should().NotBeNull();
    }

    [Theory]
    [InlineData(FunctionFailureStage.Parsing)]
    [InlineData(FunctionFailureStage.Validation)]
    [InlineData(FunctionFailureStage.Loading)]
    [InlineData(FunctionFailureStage.Execution)]
    [InlineData(FunctionFailureStage.Projection)]
    [InlineData(FunctionFailureStage.Persistence)]
    public void Lifecycle_exceptions_map_to_typed_failure_with_stage(FunctionFailureStage stage)
    {
        var command = RegimeDiscoveryFunctionValidationTests.ValidCommand();
        var terminal = RegimeDiscoveryFunctionActor.MapEvent(
            new(typeof(RegimeDiscoveryPipelineFailedEvent), command,
                Exception: new InvalidOperationException("dependency unavailable"), Stage: stage), TimeProvider.System);
        terminal.IsFailed.Should().BeTrue();
        terminal.Failed!.ErrorData.Should().Be(
            $"{stage}:Exception.Type=System.InvalidOperationException;Exception.Message=dependency unavailable;Exception.HResult=0x80131509");
        terminal.Failed.CommandId.Should().Be(command.CommandId);
        terminal.Failed.ErrorCode.Should().Be(command.ErrorCode);
    }

    [Fact]
    public void Parsing_failure_without_request_and_transport_failure_use_the_failed_map()
    {
        var missing = RegimeDiscoveryFunctionActor.MapEvent(
            new(typeof(RegimeDiscoveryPipelineFailedEvent), null,
                Exception: new FormatException(), Stage: FunctionFailureStage.Parsing), TimeProvider.System);
        missing.Failed!.ErrorData.Should().Be("Parsing");
        missing.Failed.Subject.EntityId.Should().BeEmpty();
        var command = RegimeDiscoveryFunctionValidationTests.ValidCommand();
        var transport = RegimeDiscoveryFunctionActor.MapEvent(
            new(typeof(RegimeDiscoveryPipelineFailedEvent), command,
                new RegimeDiscoveryExecutionFailed(DateTime.UtcNow, "request timed out", "FunctionRequest",
                    23001, [], Guid.Empty, "TimeoutException")), TimeProvider.System);
        transport.Failed!.ErrorData.Should().Be("FunctionRequest:TimeoutException");
        transport.Failed.ErrorMessage.Should().Be("request timed out");
    }

    [Fact]
    public void Unmapped_event_type_and_incompatible_outcome_fail_closed()
    {
        var command = RegimeDiscoveryFunctionValidationTests.ValidCommand();
        Action unmapped = () => RegimeDiscoveryFunctionActor.MapEvent(new(typeof(IEvent), command), TimeProvider.System);
        unmapped.Should().Throw<InvalidOperationException>().WithMessage("*exact event type*");
        Action wrongOutcome = () => RegimeDiscoveryFunctionActor.MapEvent(
            new(typeof(RegimeDiscoveryPipelineCompletedEvent), command,
                new RegimeDiscoveryExecutionFailed(DateTime.UtcNow, "failed", "test", 1, [], Guid.Empty)), TimeProvider.System);
        wrongOutcome.Should().Throw<InvalidOperationException>().WithMessage("*successful Regime Discovery outcome*");
    }

    [Theory]
    [InlineData("complete", true, "load,calculate,project,save", "")]
    [InlineData("incomplete", false, "load,calculate", "RegimeDiscoveryCalculation")]
    [InlineData("Loading", false, "load", "Loading")]
    [InlineData("Execution", false, "load,calculate", "Execution")]
    [InlineData("Projection", false, "load,calculate,project", "Projection")]
    [InlineData("Persistence", false, "load,calculate,project,save", "Persistence")]
    [InlineData("conflict", false, "load", "FunctionConflict")]
    public async Task Real_actor_routes_terminal_events_and_preserves_lifecycle_order(
        string scenario, bool success, string expectedCalls, string failureStage)
    {
        var context = RegimeDiscoveryFunctionValidationTests.Context();
        var command = RegimeDiscoveryFunctionValidationTests.ValidCommand();
        var calls = new List<string>();
        var state = new RegimeDiscoveryFunctionState();
        if (scenario == "conflict")
            state.TryComplete(new RegimeDiscoveryPipelineCompletedEvent
            {
                WorkflowId = command.WorkflowId, InputWorkflowRevision = command.InputWorkflowRevision,
                ParameterPayloadSha256 = "different-hash"
            }, command).Should().BeTrue();
        context.StateRepository.LoadStateAsync(command, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            calls.Add("load");
            if (scenario == "Loading") throw new InvalidOperationException("load failed");
            return ValueTask.FromResult(state);
        });
        context.CalculationModel.CalculateAsync(Arg.Any<RegimeDiscoveryCalculationInput>(),
            Arg.Any<RegimeDiscoveryExecutionMode>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            calls.Add("calculate");
            if (scenario == "Execution") throw new InvalidOperationException("calculation failed");
            return Task.FromResult(Result(command) with { Decision = new() { IsComplete = scenario != "incomplete" } });
        });
        context.FunctionProjector.ProjectAsync(Arg.Any<RegimeDiscoveryPipelineCompletedEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls.Add("project");
                if (scenario == "Projection") throw new InvalidOperationException("projection failed");
                return ValueTask.CompletedTask;
            });
        context.StateRepository.SaveCompletedStateAsync(context, state, command, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            calls.Add("save");
            if (scenario == "Persistence") throw new InvalidOperationException("save failed");
            return ValueTask.CompletedTask;
        });
        var message = Substitute.For<IActorMessage>();
        message.Subject.Returns(command.Subject);
        message.AsCommand<ExecuteRegimeDiscoveryPipelineCommand>().Returns(command);
        ServiceResult<FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>>? reply = null;
        message.When(x => x.ReplyAsync(Arg.Any<ServiceResult<FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>>>()))
            .Do(call => reply = call.Arg<ServiceResult<FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>>>());

        await new RegimeDiscoveryFunctionActor(context).HandleMessageAsync(message);

        calls.Should().Equal(expectedCalls.Split(','));
        reply.Should().NotBeNull();
        reply!.Success.Should().Be(success);
        if (!success) reply.Value!.Failed!.ErrorData.Should().StartWith(failureStage);
    }

    static RegimeDiscoveryResult Result(ExecuteRegimeDiscoveryPipelineCommand command) => new()
    {
        ResultId = command.CommandId, WorkflowId = command.WorkflowId, EntityId = command.WorkflowEntityId,
        ProducedAtUtc = DateTime.UtcNow, MarketDataAsOfUtc = DateTime.UtcNow,
        Decision = new() { IsComplete = true }
    };
}
