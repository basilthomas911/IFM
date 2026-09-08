using NSubstitute;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Actor;
using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.State;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RegimeDiscovery;

public sealed class RegimeDiscoveryFunctionExecutionTests
{
    static readonly DateTime Now = new(2026, 8, 27, 16, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Successful_calculation_returns_completed_candidate_without_mutating_state()
    {
        var command = Command(Now.AddMinutes(2));
        var result = await Execute(command, new MutableTimeProvider(Now),
            _ => Task.FromResult<RegimeDiscoveryExecutionOutcome>(Completed(command)));

        result.IsCompleted.Should().BeTrue();
        result.Completed!.Subject.ActorType.Should().Be(ActorType.Function);
        result.Completed.ParameterPayloadSha256.Should().Be(command.ParameterPayloadSha256);
        result.Completed.SignalSnapshotId.Should().NotBeEmpty();
        result.Completed.Result.SchemaVersion.Should().Be(RegimeDiscoveryResult.CurrentSchemaVersion);
    }

    [Fact]
    public async Task Expected_calculation_failure_returns_failed_and_no_completed_value()
    {
        var command = Command(Now.AddMinutes(2));
        var result = await Execute(command, new MutableTimeProvider(Now),
            _ => Task.FromResult<RegimeDiscoveryExecutionOutcome>(
                new RegimeDiscoveryExecutionFailed(Now, "missing data", "DataQuality", 23102, [], Guid.Empty)));

        result.IsFailed.Should().BeTrue();
        result.Failed!.Subject.ActorType.Should().Be(ActorType.Function);
        result.Failed.ErrorCode.Should().Be(23102);
        result.Completed.Should().BeNull();
    }

    [Fact]
    public async Task Expired_request_returns_timeout_without_invoking_worker()
    {
        var command = Command(Now);
        var invoked = false;
        var result = await Execute(command, new MutableTimeProvider(Now), _ =>
        {
            invoked = true;
            return Task.FromResult<RegimeDiscoveryExecutionOutcome>(Completed(command));
        });

        invoked.Should().BeFalse();
        result.Failed!.ErrorCode.Should().Be(23103);
    }

    [Fact]
    public async Task Timer_winner_returns_timeout_and_late_worker_cannot_change_result()
    {
        var command = Command(Now.AddMinutes(2));
        var worker = new TaskCompletionSource<RegimeDiscoveryExecutionOutcome>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = new MutableTimeProvider(Now);
        var execution = Execute(command, clock, _ => worker.Task);
        await clock.TimerReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromMinutes(2));
        clock.FireTimer();
        var result = await execution;
        worker.SetResult(Completed(command));
        await worker.Task;

        result.Failed!.ErrorCode.Should().Be(23103);
        result.Completed.Should().BeNull();
    }

    [Fact]
    public async Task Completion_at_exact_deadline_returns_only_a_timeout_failure()
    {
        var command = Command(Now.AddMinutes(2));
        var clock = new MutableTimeProvider(Now);
        var result = await Execute(command, clock, _ =>
        {
            clock.Advance(TimeSpan.FromMinutes(2));
            return Task.FromResult<RegimeDiscoveryExecutionOutcome>(Completed(command));
        });
        result.IsFailed.Should().BeTrue();
        result.Failed!.ErrorCode.Should().Be(23103);
        result.Failed.ErrorData.Should().Contain("RegimeDiscoveryExecutionTimedOut");
    }

    [Fact]
    public void Function_state_accepts_only_completed_event_and_replays_exact_result()
    {
        var command = Command(Now.AddMinutes(2));
        var completed = Execute(command, new MutableTimeProvider(Now),
                _ => Task.FromResult<RegimeDiscoveryExecutionOutcome>(Completed(command)))
            .GetAwaiter().GetResult().Completed!;
        var state = new RegimeDiscoveryFunctionState();

        state.TryComplete(completed, command).Should().BeTrue();
        state.Events.Should().ContainSingle().Which.Should().BeSameAs(completed);
        state.IsCompleted.Should().BeTrue();
        state.CompletedEvent.Should().BeSameAs(completed);
        state.Matches(command).Should().BeTrue();
        state.TryComplete(completed, command).Should().BeFalse();
    }

    static async Task<FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>> Execute(
        ExecuteRegimeDiscoveryPipelineCommand command, TimeProvider clock,
        Func<CancellationToken, Task<RegimeDiscoveryExecutionOutcome>> worker)
    {
        var actor = new DeadlineTestActor(clock, worker);
        var message = Substitute.For<IActorMessage>();
        message.Subject.Returns(command.Subject);
        message.AsCommand<ExecuteRegimeDiscoveryPipelineCommand>().Returns(command);
        ServiceResult<FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>>? reply = null;
        message.ReplyAsync(Arg.Do<ServiceResult<FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>>>(value => reply = value)).Returns(ValueTask.CompletedTask);
        await actor.HandleMessageAsync(message);
        return reply!.Value!;
    }

    // Tests exercise the shared lifecycle timer with the production policy and terminal maps.
    sealed class DeadlineTestActor : BaseEventSourceFunctionActor<DeadlineTestActor, ExecuteRegimeDiscoveryPipelineCommand,
        RegimeDiscoveryExecutionEntityId, IntrinsicTimeStrategyWorkflowEntityId, RegimeDiscoveryFunctionState,
        RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>
    {
        readonly IRegimeDiscoveryFunctionContext domain = Substitute.For<IRegimeDiscoveryFunctionContext>();
        readonly Func<CancellationToken, Task<RegimeDiscoveryExecutionOutcome>> worker;
        static readonly IReadOnlyDictionary<Type, Func<ExecuteRegimeDiscoveryPipelineCommand, FunctionFailureStage, IRegimeDiscoveryFunctionContext, FunctionExecutionPolicy>> _executionPolicyMap =
            new Dictionary<Type, Func<ExecuteRegimeDiscoveryPipelineCommand, FunctionFailureStage, IRegimeDiscoveryFunctionContext, FunctionExecutionPolicy>>
            { [typeof(ExecuteRegimeDiscoveryPipelineCommand)] = (command, stage, context) => command.ResolveExecutionPolicy(stage, context) };
        public DeadlineTestActor(TimeProvider clock, Func<CancellationToken, Task<RegimeDiscoveryExecutionOutcome>> execute)
            : base(ContextForTest(), Repository(), null, NullLogger<DeadlineTestActor>.Instance)
        { domain.TimeProvider.Returns(clock); worker = execute; }
        static IFunctionActorContext<DeadlineTestActor> ContextForTest() => new DeadlineTestContext();
        sealed class DeadlineTestContext : IFunctionActorContext<DeadlineTestActor>
        {
            public ActorMailboxId ActorId { get; } = new(ActorType.Function, RegimeDiscoveryFunctionActor.ActorName);
            public IContainerInstance Container => throw new NotSupportedException();
        }
        static IEventSourceFunctionStateRepository<RegimeDiscoveryFunctionState, ExecuteRegimeDiscoveryPipelineCommand> Repository()
        {
            var repository = Substitute.For<IEventSourceFunctionStateRepository<RegimeDiscoveryFunctionState, ExecuteRegimeDiscoveryPipelineCommand>>();
            repository.LoadStateAsync(Arg.Any<ExecuteRegimeDiscoveryPipelineCommand>(), Arg.Any<CancellationToken>()).Returns(_ => ValueTask.FromResult(new RegimeDiscoveryFunctionState()));
            return repository;
        }
        protected override FunctionExecutionPolicy ResolveExecutionPolicy(ExecuteRegimeDiscoveryPipelineCommand request, FunctionFailureStage stage)
            => DispatchMappedExecutionPolicy(request, stage, domain, _executionPolicyMap);
        protected override ExecuteRegimeDiscoveryPipelineCommand ParseMessage(IFunctionActorContext<DeadlineTestActor> context, IActorMessage message)
            => message.AsCommand<ExecuteRegimeDiscoveryPipelineCommand>()!;
        protected override FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent> HandleFunctionEvent(
            IFunctionActorContext<DeadlineTestActor> context, FunctionEventContext<ExecuteRegimeDiscoveryPipelineCommand> input)
            => RegimeDiscoveryFunctionActor.MapEvent(input, domain.TimeProvider);
        protected override async ValueTask<FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>> ExecuteFunctionAsync(
            IFunctionActorContext<DeadlineTestActor> context, RegimeDiscoveryFunctionState state, ExecuteRegimeDiscoveryPipelineCommand request, CancellationToken token)
        {
            var outcome = await worker(token);
            return RegimeDiscoveryFunctionActor.MapEvent(new(outcome is RegimeDiscoveryExecutionCompleted
                ? typeof(RegimeDiscoveryPipelineCompletedEvent) : typeof(RegimeDiscoveryPipelineFailedEvent), request, outcome), domain.TimeProvider);
        }
    }

    static RegimeDiscoveryExecutionCompleted Completed(ExecuteRegimeDiscoveryPipelineCommand command)
        => new(new RegimeDiscoveryResult
        {
            ResultId = command.CommandId,
            WorkflowId = command.WorkflowId,
            EntityId = command.WorkflowEntityId,
            ProducedAtUtc = Now.AddSeconds(1),
            MarketDataAsOfUtc = Now,
            Decision = new RegimeDiscoveryDecision { IsComplete = true }
        }, Guid.Parse("0198E212-3C00-7000-8000-000000000511"), 9);

    internal static ExecuteRegimeDiscoveryPipelineCommand Command(DateTime expiresAtUtc)
    {
        var workflowEntity = IntrinsicTimeStrategyWorkflowEntityId.Create(new FuturesItiSignalEntityId(
            "ES-202612", new DateOnly(2026, 8, 27), TimeFrameType.Daily));
        var workflowId = new StrategyWorkflowId(Guid.Parse("0198E212-3C00-7000-8000-000000000512"));
        var executionId = RegimeDiscoveryExecutionEntityId.Create(workflowEntity, workflowId);
        return new ExecuteRegimeDiscoveryPipelineCommand
        {
            CommandId = Guid.Parse("0198E212-3C00-7000-8000-000000000513"),
            Subject = new ActorSubject(ActorType.Function, ExecuteRegimeDiscoveryPipelineCommand.Actor,
                ExecuteRegimeDiscoveryPipelineCommand.Verb, executionId.Format()),
            EntityId = executionId,
            InputWorkflowRevision = 1,
            WorkflowView = new IntrinsicTimeStrategyWorkflowView
            {
                EntityId = workflowEntity,
                WorkflowId = workflowId,
                WorkflowRevision = 1,
                Status = WorkflowStrategyMachineStatus.Started,
                CurrentStage = StrategyWorkflowStage.RegimeDiscovery,
                ExpiresAtUtc = expiresAtUtc
            },
            TriggerEvent = new FuturesItiSignalGeneratedEvent { EntityId = workflowEntity.ItiSignalEntityId },
            RequestedAtUtc = Now,
            ExpiresAtUtc = expiresAtUtc,
            TargetHorizon = TimeFrameType.Daily,
            ParameterPayloadSha256 = new string('A', 64)
        };
    }

    sealed class MutableTimeProvider(DateTime value) : TimeProvider
    {
        DateTimeOffset _now = new(value);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan elapsed) => _now += elapsed;
        public TaskCompletionSource TimerReady { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TimerCallback? callback; object? state;
        public override ITimer CreateTimer(TimerCallback action, object? callbackState, TimeSpan dueTime, TimeSpan period)
        { callback = action; state = callbackState; TimerReady.TrySetResult(); return new TestTimer(); }
        public void FireTimer() => callback!(state);
        sealed class TestTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
