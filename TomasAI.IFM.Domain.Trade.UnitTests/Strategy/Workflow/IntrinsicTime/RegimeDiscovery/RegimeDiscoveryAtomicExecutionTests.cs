using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.State;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RegimeDiscovery;

/// <summary>Qualifies RD-19F's single-owner atomic Regime execution boundary without wall-clock sleeps.</summary>
public sealed class RegimeDiscoveryAtomicExecutionTests
{
    static readonly DateTime Now = new(2026, 8, 27, 16, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Successful_work_commits_exactly_one_completed_event()
    {
        var state = new RegimeDiscoveryCommandState();
        var command = Command(Now.AddMinutes(2));

        await Execute(command, state, new MutableTimeProvider(Now),
            _ => Task.FromResult<RegimeDiscoveryExecutionOutcome>(Completed(command)));

        state.Events.Should().ContainSingle().Which.Should()
            .BeOfType<RegimeDiscoveryCalculationCompletedEvent>();
        state.Status.Should().Be(RegimeDiscoveryCommandStatus.Completed);
    }

    [Fact]
    public async Task Expected_domain_failure_commits_exactly_one_failed_event()
    {
        var state = new RegimeDiscoveryCommandState();
        var command = Command(Now.AddMinutes(2));

        await Execute(command, state, new MutableTimeProvider(Now),
            _ => Task.FromResult<RegimeDiscoveryExecutionOutcome>(Failure(Now, 23102, "DataQuality")));

        var failed = state.Events.Should().ContainSingle().Which
            .Should().BeOfType<RegimeDiscoveryCalculationFailedEvent>().Subject;
        failed.Failure.ErrorType.Should().Be("DataQuality");
        state.Status.Should().Be(RegimeDiscoveryCommandStatus.Failed);
    }

    [Fact]
    public async Task Expired_before_execute_skips_all_work_and_commits_timeout()
    {
        var state = new RegimeDiscoveryCommandState();
        var command = Command(Now);
        var invoked = false;

        await Execute(command, state, new MutableTimeProvider(Now), _ =>
        {
            invoked = true;
            return Task.FromResult<RegimeDiscoveryExecutionOutcome>(Completed(command));
        });

        invoked.Should().BeFalse();
        AssertTimeout(state);
    }

    [Fact]
    public async Task Timeout_during_snapshot_or_calculation_commits_once_and_late_worker_cannot_overwrite()
    {
        var state = new RegimeDiscoveryCommandState();
        var command = Command(Now.AddMinutes(2));
        var worker = new TaskCompletionSource<RegimeDiscoveryExecutionOutcome>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var timer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var execution = ExecuteRegimeDiscoveryPipeline.ExecuteAtomicAsync(
            command, state, new MutableTimeProvider(Now), _ => worker.Task, (_, _) => timer.Task);
        timer.SetResult();
        await execution;

        AssertTimeout(state);
        worker.SetResult(Completed(command));
        state.Events.Should().ContainSingle();
        state.Status.Should().Be(RegimeDiscoveryCommandStatus.Failed);
    }

    [Fact]
    public async Task Completion_at_exact_deadline_loses_to_timeout_even_when_worker_task_wins_first()
    {
        var state = new RegimeDiscoveryCommandState();
        var clock = new MutableTimeProvider(Now);
        var command = Command(Now.AddMinutes(2));

        await Execute(command, state, clock, _ =>
        {
            clock.Set(command.ExpiresAtUtc);
            return Task.FromResult<RegimeDiscoveryExecutionOutcome>(Completed(command));
        });

        AssertTimeout(state);
    }

    [Fact]
    public async Task Unexpected_exception_appends_no_terminal_event()
    {
        var state = new RegimeDiscoveryCommandState();
        var command = Command(Now.AddMinutes(2));

        var execute = async () => await Execute(command, state, new MutableTimeProvider(Now),
            _ => Task.FromException<RegimeDiscoveryExecutionOutcome>(new InvalidOperationException("boom")));

        await execute.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        state.Events.Should().BeEmpty();
        state.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public async Task Matching_duplicate_is_idempotent_and_conflicting_revision_is_rejected_without_work()
    {
        var command = Command(Now.AddMinutes(2));
        var original = new RegimeDiscoveryCommandState();
        await Execute(command, original, new MutableTimeProvider(Now),
            _ => Task.FromResult<RegimeDiscoveryExecutionOutcome>(Completed(command)));
        var persisted = original.Events.Cast<RegimeDiscoveryCalculationCompletedEvent>().Single() with { EventId = 1 };
        var loaded = new RegimeDiscoveryCommandState();
        loaded.Apply(persisted, addEvent: false).Should().BeTrue();
        var workerCalls = 0;

        await Execute(command, loaded, new MutableTimeProvider(Now), _ =>
        {
            workerCalls++;
            return Task.FromResult<RegimeDiscoveryExecutionOutcome>(Completed(command));
        });
        await Execute(command with { InputWorkflowRevision = 2 }, loaded, new MutableTimeProvider(Now), _ =>
        {
            workerCalls++;
            return Task.FromResult<RegimeDiscoveryExecutionOutcome>(Completed(command));
        });
        await Execute(command with { ParameterPayloadSha256 = new string('B', 64) }, loaded,
            new MutableTimeProvider(Now), _ =>
            {
                workerCalls++;
                return Task.FromResult<RegimeDiscoveryExecutionOutcome>(Completed(command));
            });

        workerCalls.Should().Be(0);
        loaded.Events.Should().BeEmpty();
        loaded.Status.Should().Be(RegimeDiscoveryCommandStatus.Completed);
    }

    [Fact]
    public async Task Separate_workflow_execution_identities_do_not_block_each_other()
    {
        var first = Command(Now.AddMinutes(2));
        var secondWorkflowId = new StrategyWorkflowId(
            Guid.Parse("0198E212-3C00-7000-8000-000000000514"));
        var secondEntity = RegimeDiscoveryExecutionEntityId.Create(first.WorkflowEntityId, secondWorkflowId);
        var second = first with
        {
            CommandId = Guid.Parse("0198E212-3C00-7000-8000-000000000515"),
            Subject = new ActorSubject(ActorType.Command, ExecuteRegimeDiscoveryPipelineCommand.Actor,
                ExecuteRegimeDiscoveryPipelineCommand.Verb, secondEntity.Format()),
            EntityId = secondEntity,
            WorkflowView = first.WorkflowView with { WorkflowId = secondWorkflowId }
        };
        var firstState = new RegimeDiscoveryCommandState();
        var secondState = new RegimeDiscoveryCommandState();

        await Task.WhenAll(
            Execute(first, firstState, new MutableTimeProvider(Now),
                _ => Task.FromResult<RegimeDiscoveryExecutionOutcome>(Completed(first))),
            Execute(second, secondState, new MutableTimeProvider(Now),
                _ => Task.FromResult<RegimeDiscoveryExecutionOutcome>(Completed(second))));

        firstState.Status.Should().Be(RegimeDiscoveryCommandStatus.Completed);
        secondState.Status.Should().Be(RegimeDiscoveryCommandStatus.Completed);
        first.EntityId.Should().NotBe(second.EntityId);
        first.StreamId.Should().NotBe(second.StreamId);
    }

    static async Task Execute(
        ExecuteRegimeDiscoveryPipelineCommand command,
        RegimeDiscoveryCommandState state,
        TimeProvider clock,
        Func<CancellationToken, Task<RegimeDiscoveryExecutionOutcome>> worker)
        => await ExecuteRegimeDiscoveryPipeline.ExecuteAtomicAsync(
            command,
            state,
            clock,
            worker,
            (_, cancellationToken) => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));

    static void AssertTimeout(RegimeDiscoveryCommandState state)
    {
        var failed = state.Events.Should().ContainSingle().Which
            .Should().BeOfType<RegimeDiscoveryCalculationFailedEvent>().Subject;
        failed.Failure.ErrorCode.Should().Be(RegimeDiscoveryCalculationFailedEvent.TimeoutErrorCode);
        failed.Failure.ErrorType.Should().Be("Timeout");
    }

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

    static RegimeDiscoveryExecutionFailed Failure(DateTime when, int code, string type)
        => new(when, "expected failure", type, code, [], Guid.Empty);

    static ExecuteRegimeDiscoveryPipelineCommand Command(DateTime expiresAtUtc)
    {
        var workflowEntity = IntrinsicTimeStrategyWorkflowEntityId.Create(new FuturesItiSignalEntityId(
            "ES-202612", new DateOnly(2026, 8, 27), TimeFrameType.Daily));
        var workflowId = new StrategyWorkflowId(Guid.Parse("0198E212-3C00-7000-8000-000000000512"));
        var executionId = RegimeDiscoveryExecutionEntityId.Create(workflowEntity, workflowId);
        return new ExecuteRegimeDiscoveryPipelineCommand
        {
            CommandId = Guid.Parse("0198E212-3C00-7000-8000-000000000513"),
            Subject = new ActorSubject(ActorType.Command, ExecuteRegimeDiscoveryPipelineCommand.Actor,
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
        public void Set(DateTime value) => _now = new DateTimeOffset(value);
    }
}
