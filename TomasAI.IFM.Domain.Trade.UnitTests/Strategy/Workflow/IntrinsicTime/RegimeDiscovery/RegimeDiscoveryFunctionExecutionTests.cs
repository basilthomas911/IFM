using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Extensions;
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
        var timer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var execution = ExecuteRegimeDiscoveryPipeline.ExecuteAtomicAsync(
            command, new MutableTimeProvider(Now), _ => worker.Task, (_, _) => timer.Task);
        timer.SetResult();
        var result = await execution;
        worker.SetResult(Completed(command));

        result.Failed!.ErrorCode.Should().Be(23103);
        result.Completed.Should().BeNull();
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

    static Task<TomasAI.IFM.Shared.EventSourcing.FunctionResult<
        RegimeDiscoveryPipelineCompletedEvent,
        RegimeDiscoveryPipelineFailedEvent>> Execute(
        ExecuteRegimeDiscoveryPipelineCommand command,
        TimeProvider clock,
        Func<CancellationToken, Task<RegimeDiscoveryExecutionOutcome>> worker)
        => ExecuteRegimeDiscoveryPipeline.ExecuteAtomicAsync(
            command,
            clock,
            worker,
            (_, cancellationToken) => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));

    static RegimeDiscoveryExecutionCompleted Completed(ExecuteRegimeDiscoveryPipelineCommand command)
        => new(new RegimeDiscoveryResult
        {
            ResultId = command.CommandId,
            WorkflowId = command.WorkflowId,
            EntityId = command.WorkflowEntityId,
            ProducedAtUtc = Now.AddSeconds(1),
            MarketDataAsOfUtc = Now,
            Fusion = new MarketRegimeFusionResult { IsComplete = true }
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
    }
}
