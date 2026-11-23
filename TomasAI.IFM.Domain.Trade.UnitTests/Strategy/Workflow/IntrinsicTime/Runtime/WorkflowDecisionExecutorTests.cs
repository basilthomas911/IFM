using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.Runtime;

[Collection(WorkflowDecisionQualificationCollection.Name)]
public sealed class WorkflowDecisionExecutorTests
{
    [Fact]
    public async Task An_expired_schedule_never_invokes_the_operator()
    {
        await using var executor = new WorkflowDecisionExecutor();
        var operation = new CountingOperator();
        var now = DateTime.UtcNow;
        var result = await executor.ExecuteAsync(operation, 7, now.AddSeconds(-2), now.AddSeconds(10));
        result.Status.Should().Be(DecisionExecutionStatus.DeadlineExceeded);
        operation.Calls.Should().Be(0);
        WorkflowDecisionExecutor.MaximumStageDuration.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Queued_work_uses_its_original_budget_and_overload_does_not_retry()
    {
        await using var executor = new WorkflowDecisionExecutor(concurrency: 1, capacity: 1);
        var operation = new PausedOperator();
        var now = DateTime.UtcNow;
        var running = executor.ExecuteAsync(operation, 7, now, now.AddSeconds(10)).AsTask();
        await operation.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            var queuedOperation = new CountingOperator();
            now = DateTime.UtcNow;
            var queued = executor.ExecuteAsync(queuedOperation, 8, now.AddMilliseconds(-1900), now.AddSeconds(10)).AsTask();
            var overloaded = await executor.ExecuteAsync(queuedOperation, 9, now, now.AddSeconds(10));
            overloaded.Status.Should().Be(DecisionExecutionStatus.Overloaded);
            (await queued).Status.Should().Be(DecisionExecutionStatus.DeadlineExceeded);
            queuedOperation.Calls.Should().Be(0);
        }
        finally { operation.Release.TrySetResult(7); }
        // The deliberately blocked first calculation may itself expire under concurrent test load.
        // It must retain the worker until release, even after its caller receives that timeout.
        var first = await running;
        first.Status.Should().BeOneOf(DecisionExecutionStatus.Completed, DecisionExecutionStatus.DeadlineExceeded);
        if (first.Status == DecisionExecutionStatus.Completed)
            (first.QueueTime + first.CalculationTime).Should().BeLessThan(WorkflowDecisionExecutor.MaximumStageDuration);
        else first.Value.Should().Be(default(int));
        operation.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Late_result_is_rejected_even_if_the_timer_callback_has_not_run()
    {
        var clock = new ManualClock();
        await using var executor = new WorkflowDecisionExecutor(timeProvider: clock);
        var operation = new AdvancingOperator(clock);
        var now = clock.GetUtcNow().UtcDateTime;
        var result = await executor.ExecuteAsync(operation, 7, now, now.AddSeconds(10));
        result.Status.Should().Be(DecisionExecutionStatus.DeadlineExceeded);
        operation.Calls.Should().Be(1);
    }

    [Fact]
    public async Task A_burst_is_bounded_and_every_admitted_input_gets_at_most_one_attempt()
    {
        await using var executor = new WorkflowDecisionExecutor(concurrency: 4, capacity: 16);
        var operation = new BurstOperator();
        var now = DateTime.UtcNow;
        var calls = Enumerable.Range(0, 256).Select(value => executor.ExecuteAsync(operation, value, now, now.AddSeconds(10)).AsTask()).ToArray();
        operation.Release.TrySetResult();
        var results = await Task.WhenAll(calls);
        results.Count(value => value.Status == DecisionExecutionStatus.Overloaded).Should().BeGreaterThanOrEqualTo(236);
        operation.Calls.Values.Should().OnlyContain(count => count == 1);
        operation.Calls.Count.Should().BeLessThanOrEqualTo(20);
        for (var i = 0; i < results.Length; i++)
        {
            var result = results[i];
            result.Status.Should().BeOneOf(DecisionExecutionStatus.Completed, DecisionExecutionStatus.Overloaded, DecisionExecutionStatus.DeadlineExceeded);
            if (result.Status != DecisionExecutionStatus.Completed) continue;
            result.Value.Should().Be(i);
            (result.QueueTime + result.CalculationTime).Should().BeLessThan(WorkflowDecisionExecutor.MaximumStageDuration);
        }
        var output = Environment.GetEnvironmentVariable("IFM_WORKFLOW_LOAD_REPORT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            var durations = results.Where(value => value.Status == DecisionExecutionStatus.Completed)
                .Select(value => (value.QueueTime + value.CalculationTime).TotalMilliseconds).Order().ToArray();
            double Percentile(double p) => durations.Length == 0 ? 0 : durations[(int)Math.Ceiling(p * (durations.Length - 1))];
            await File.WriteAllTextAsync(output, System.Text.Json.JsonSerializer.Serialize(new
            {
                Scope = "Executor burst guard, not whole-workflow throughput", Requests = 256, Workers = 4, QueueCapacity = 16,
                Outcomes = results.GroupBy(value => value.Status.ToString()).ToDictionary(group => group.Key, group => group.Count()),
                SuccessfulMilliseconds = new { P50 = Percentile(.5), P95 = Percentile(.95), P99 = Percentile(.99), Maximum = Percentile(1) }
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }

    sealed class BurstOperator : IWorkflowDecisionOperator<int, int>
    {
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public System.Collections.Concurrent.ConcurrentDictionary<int, int> Calls { get; } = new();
        public async ValueTask<int> CalculateAsync(int input, CancellationToken token)
        { Calls.AddOrUpdate(input, 1, (_, previous) => previous + 1); await Release.Task.WaitAsync(token); return input; }
    }

    sealed class CountingOperator : IWorkflowDecisionOperator<int, int>
    {
        public int Calls;
        public ValueTask<int> CalculateAsync(int input, CancellationToken cancellationToken)
        { Interlocked.Increment(ref Calls); return ValueTask.FromResult(input); }
    }
    sealed class PausedOperator : IWorkflowDecisionOperator<int, int>
    {
        public int Calls;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<int> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask<int> CalculateAsync(int input, CancellationToken cancellationToken)
        { Interlocked.Increment(ref Calls); Started.TrySetResult(); return await Release.Task; }
    }
    sealed class AdvancingOperator(ManualClock clock) : IWorkflowDecisionOperator<int, int>
    {
        public int Calls;
        public ValueTask<int> CalculateAsync(int input, CancellationToken cancellationToken)
        { Calls++; clock.Advance(TimeSpan.FromSeconds(3)); return ValueTask.FromResult(input); }
    }
    sealed class ManualClock : TimeProvider
    {
        long ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref ticks);
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero).AddTicks(GetTimestamp());
        public void Advance(TimeSpan duration) => Interlocked.Add(ref ticks, duration.Ticks);
    }
}
