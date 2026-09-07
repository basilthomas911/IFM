using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RegimeDiscovery;

public sealed class RegimeDiscoveryFunctionValidationTests
{
    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> Map =
        (IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>>)typeof(RegimeDiscoveryFunctionActor)
            .GetField("_validationMap", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;

    [Theory]
    [InlineData(TimeFrameType.Daily)]
    [InlineData(TimeFrameType.Weekly)]
    [InlineData(TimeFrameType.Monthly)]
    public void Valid_request_passes_actor_validation(TimeFrameType horizon)
    {
        var command = ValidCommand(horizon);
        Validate(command).Should().BeEmpty();
        InvokeValidation(Context(), command).Should().BeNull();
    }

    [Fact]
    public void Independent_errors_are_aggregated_by_the_base_helper()
    {
        var command = ValidCommand() with
        {
            CommandId = Guid.Empty, InputWorkflowRevision = 0,
            CorrelationId = Guid.Empty, CausationId = Guid.Empty,
            RequestedAtUtc = default, ExpiresAtUtc = default,
            ParameterPayloadSha256 = "bad", TargetHorizon = TimeFrameType.None
        };
        var errors = Validate(command);
        errors.Count.Should().BeGreaterThanOrEqualTo(8);
        var exception = InvokeValidation(Context(), command).Should().BeOfType<CommandValidationException>().Subject;
        exception.ErrorCode.Should().Be(command.ErrorCode);
        foreach (var error in errors) exception.Message.Should().Contain(error.ErrorMessage);
    }

    [Fact]
    public void Null_payloads_accumulate_errors_without_null_reference_exceptions()
    {
        var command = ValidCommand() with { WorkflowView = null!, TriggerEvent = null!, ParameterSet = null! };
        Validate(command).Select(error => error.ErrorMessage)
            .Should().Contain(["WorkflowView is required.", "TriggerEvent is required.", "ParameterSet is required."]);
        InvokeValidation(Context(), command).Should().BeOfType<CommandValidationException>();
    }

    [Fact]
    public void Null_parameter_children_and_timeframe_entries_are_validation_errors()
    {
        var parameters = ValidCommand().ParameterSet with
        {
            Horizon = null!, Trend = null!, Volatility = null!, MarketStructure = null!,
            Fusion = null!, Freshness = null!, DataQuality = null!
        };
        new RegimeDiscoveryParameterSetValidationRules().Execute(parameters).Should().HaveCount(7);
        parameters = ValidCommand().ParameterSet;
        new RegimeDiscoveryParameterSetValidationRules().Execute(parameters with
        {
            Horizon = parameters.Horizon with { TimeFrames = null! }
        }).Should().NotBeEmpty();
        new RegimeDiscoveryParameterSetValidationRules().Execute(parameters with
        {
            Horizon = parameters.Horizon with { TimeFrames = [null!, new() { Weight = -1 }] }
        }).Should().HaveCount(4);
    }

    [Fact]
    public void Inconsistent_workflow_timeframe_deadline_and_hash_are_rejected()
    {
        var valid = ValidCommand();
        var command = valid with
        {
            WorkflowView = valid.WorkflowView with { WorkflowRevision = 2 },
            ExpiresAtUtc = valid.RequestedAtUtc,
            TargetHorizon = TimeFrameType.Weekly,
            ParameterPayloadSha256 = new string('0', 64)
        };
        var errors = Validate(command).Select(error => error.ErrorMessage).ToArray();
        errors.Should().Contain(error => error.Contains("identity and revision"));
        errors.Should().Contain(error => error.Contains("later than"));
        errors.Should().Contain(error => error.Contains("parameter-set target"));
        errors.Should().Contain(error => error.Contains("trigger ITI"));
        errors.Should().Contain(error => error.Contains("canonical parameter"));
    }

    [Fact]
    public void Cancellation_precedes_validation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        InvokeValidation(Context(), ValidCommand() with { CommandId = Guid.Empty }, source.Token)
            .Should().BeOfType<OperationCanceledException>();
    }

    [Fact]
    public void Malformed_execution_and_trigger_identities_accumulate_errors()
    {
        var command = ValidCommand();
        command = command with
        {
            EntityId = default,
            WorkflowView = command.WorkflowView with { EntityId = default, WorkflowId = default },
            TriggerEvent = command.TriggerEvent with { EntityId = null! }
        };
        Validate(command).Count.Should().BeGreaterThan(3);
        InvokeValidation(Context(), command).Should().BeOfType<CommandValidationException>();
    }

    [Fact]
    public async Task Valid_request_replays_completion_through_real_actor_ingress()
    {
        var context = Context();
        var command = ValidCommand();
        var completed = new RegimeDiscoveryPipelineCompletedEvent
        {
            Id = Guid.NewGuid(), EntityId = command.WorkflowEntityId, WorkflowId = command.WorkflowId,
            InputWorkflowRevision = command.InputWorkflowRevision, CommandId = command.CommandId,
            ParameterPayloadSha256 = command.ParameterPayloadSha256
        };
        var state = new RegimeDiscoveryFunctionState();
        state.TryComplete(completed, command).Should().BeTrue();
        context.StateRepository.LoadStateAsync(command, Arg.Any<CancellationToken>()).Returns(state);
        var message = Substitute.For<IActorMessage>();
        message.Subject.Returns(command.Subject);
        message.AsCommand<ExecuteRegimeDiscoveryPipelineCommand>().Returns(command);

        await new RegimeDiscoveryFunctionActor(context).HandleMessageAsync(message);

        await message.Received(1).ReplyAsync(Arg.Is<ServiceResult<FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>>>(
            reply => reply.Success && reply.Value!.Completed == completed));
        context.FunctionProjector.ReceivedCalls().Should().BeEmpty();
        context.SnapshotProvider.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData("payload")]
    [InlineData("route")]
    [InlineData("identity")]
    [InlineData("header")]
    public async Task Invalid_ingress_returns_failure_without_loading_or_projecting(string invalidField)
    {
        var context = Context();
        var command = ValidCommand() with { ParameterSet = null! };
        if (invalidField == "route") command = command with { Subject = command.Subject with { EntityId = "wrong-route" } };
        if (invalidField == "identity") command = command with { EntityId = default };
        var message = Substitute.For<IActorMessage>();
        message.Subject.Returns(command.Subject);
        message.AsCommand<ExecuteRegimeDiscoveryPipelineCommand>().Returns(command);
        if (invalidField == "header") message.Subject.Returns(command.Subject with { ActorType = ActorType.Command });
        ServiceResult<FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>>? reply = null;
        message.When(x => x.ReplyAsync(Arg.Any<ServiceResult<FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>>>()))
            .Do(call => reply = call.Arg<ServiceResult<FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>>>());

        await new RegimeDiscoveryFunctionActor(context).HandleMessageAsync(message);

        reply.Should().NotBeNull();
        reply!.Success.Should().BeFalse();
        reply.Value!.Failed.Should().NotBeNull();
        context.StateRepository.ReceivedCalls().Should().BeEmpty();
        context.FunctionProjector.ReceivedCalls().Should().BeEmpty();
        context.SnapshotProvider.ReceivedCalls().Should().BeEmpty();
    }

    static List<ValidationError> Validate(ExecuteRegimeDiscoveryPipelineCommand command) => Map[command.GetType()](command);

    static Exception? InvokeValidation(IRegimeDiscoveryFunctionContext context,
        ExecuteRegimeDiscoveryPipelineCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = (ValueTask)typeof(RegimeDiscoveryFunctionActor)
                .GetMethod("ValidateAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(new RegimeDiscoveryFunctionActor(context),
                    [context, command.Subject.ThreadId, command, cancellationToken])!;
            result.GetAwaiter().GetResult();
            return null;
        }
        catch (TargetInvocationException exception) { return exception.InnerException; }
    }

    internal static IRegimeDiscoveryFunctionContext Context()
    {
        var context = Substitute.For<IRegimeDiscoveryFunctionContext>();
        context.ActorId.Returns(new ActorMailboxId(ActorType.Function, RegimeDiscoveryFunctionActor.ActorName));
        context.Logger.Returns(NullLogger<RegimeDiscoveryFunctionActor>.Instance);
        context.TimeProvider.Returns(TimeProvider.System);
        context.StateRepository.Returns(Substitute.For<IEventSourceFunctionStateRepository<RegimeDiscoveryFunctionState, ExecuteRegimeDiscoveryPipelineCommand>>());
        context.FunctionProjector.Returns(Substitute.For<IFunctionProjector<RegimeDiscoveryPipelineCompletedEvent>>());
        return context;
    }

    internal static ExecuteRegimeDiscoveryPipelineCommand ValidCommand(TimeFrameType horizon = TimeFrameType.Daily)
    {
        var command = RegimeDiscoveryFunctionExecutionTests.Command(DateTime.UtcNow.AddMinutes(2));
        var parameters = RegimeDiscoveryParameterSet.CreateDefault(Guid.NewGuid(), Guid.NewGuid(), horizon);
        var workflow = command.WorkflowEntityId with
        {
            ItiSignalEntityId = command.WorkflowEntityId.ItiSignalEntityId with { TimePeriod = horizon }
        };
        var entity = command.EntityId with { WorkflowEntityId = workflow };
        return command with
        {
            EntityId = entity, Subject = command.Subject with { EntityId = entity.Format() },
            WorkflowView = command.WorkflowView with { EntityId = workflow },
            TriggerEvent = command.TriggerEvent with { EntityId = workflow.ItiSignalEntityId },
            CorrelationId = Guid.NewGuid(), CausationId = Guid.NewGuid(),
            ParameterSet = parameters, ParameterPayloadSha256 = RegimeDiscoveryParameterPayload.ComputeSha256(parameters),
            TargetHorizon = horizon
        };
    }
}
