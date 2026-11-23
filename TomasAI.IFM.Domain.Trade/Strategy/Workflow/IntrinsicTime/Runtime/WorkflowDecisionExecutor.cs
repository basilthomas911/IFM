using System.Threading.Channels;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;

/// <summary>The calculation capability exposes only a prepared input and cancellation, never storage or transport.</summary>
public interface IWorkflowDecisionOperator<in TInput, TOutput>
{
    ValueTask<TOutput> CalculateAsync(TInput input, CancellationToken cancellationToken);
}

public enum DecisionExecutionStatus { Completed, InputUnavailable, DeadlineExceeded, Overloaded, Cancelled, Failed }

public sealed record DecisionExecution<T>(DecisionExecutionStatus Status, T? Value,
    TimeSpan QueueTime, TimeSpan CalculationTime, Exception? Error = null);

/// <summary>Bounded single-attempt execution. Timeout rejects the result; non-cooperative work retains its worker slot.</summary>
public sealed class WorkflowDecisionExecutor : IAsyncDisposable
{
    public static readonly TimeSpan MaximumStageDuration = TimeSpan.FromSeconds(2);
    readonly Channel<Work> pending;
    readonly CancellationTokenSource stopping = new();
    readonly Task[] workers;
    readonly TimeProvider clock;
    abstract class Work
    {
        public abstract Task RunAsync();
        public abstract void Stop();
    }

    public WorkflowDecisionExecutor(int concurrency = 4, int capacity = 128, TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(concurrency);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        clock = timeProvider ?? TimeProvider.System;
        pending = Channel.CreateBounded<Work>(new BoundedChannelOptions(capacity)
        { SingleReader = concurrency == 1, FullMode = BoundedChannelFullMode.Wait });
        workers = Enumerable.Range(0, concurrency).Select(_ => Task.Run(ConsumeAsync)).ToArray();
    }

    /// <summary>The UTC boundary is captured by the manager when the stage becomes ready, before queueing.</summary>
    public async ValueTask<DecisionExecution<TOutput>> ExecuteAsync<TInput, TOutput>(
        IWorkflowDecisionOperator<TInput, TOutput> operation, TInput input, DateTime readyAtUtc,
        DateTime workflowExpiresAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (readyAtUtc.Kind != DateTimeKind.Utc || workflowExpiresAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Decision scheduling requires UTC boundaries.");
        var now = clock.GetUtcNow().UtcDateTime;
        var deadline = new[] { readyAtUtc.Add(MaximumStageDuration), workflowExpiresAtUtc }.Min();
        var remaining = deadline - now;
        if (remaining <= TimeSpan.Zero) return new(DecisionExecutionStatus.DeadlineExceeded, default, TimeSpan.Zero, TimeSpan.Zero);
        // Never grant extra time when a supplied UTC schedule lies in the future.
        if (remaining > MaximumStageDuration) remaining = MaximumStageDuration;
        if (stopping.IsCancellationRequested || cancellationToken.IsCancellationRequested)
            return new(DecisionExecutionStatus.Cancelled, default, TimeSpan.Zero, TimeSpan.Zero);
        using var timeout = new CancellationTokenSource(remaining, clock);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, stopping.Token, cancellationToken);
        var work = new Calculation<TInput, TOutput>(operation, input, clock, remaining, cancellation.Token, timeout.Token);
        if (!pending.Writer.TryWrite(work)) return new(DecisionExecutionStatus.Overloaded, default, TimeSpan.Zero, TimeSpan.Zero);
        try
        {
            var completed = await work.Completion.Task.WaitAsync(cancellation.Token).ConfigureAwait(false);
            return completed.Status == DecisionExecutionStatus.Completed && clock.GetElapsedTime(work.QueuedAt) >= remaining
                ? completed with { Status = DecisionExecutionStatus.DeadlineExceeded, Value = default } : completed;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return new(timeout.IsCancellationRequested ? DecisionExecutionStatus.DeadlineExceeded : DecisionExecutionStatus.Cancelled,
                default, work.QueueTime, work.CalculationTime);
        }
    }

    async Task ConsumeAsync()
    {
        await foreach (var work in pending.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            if (stopping.IsCancellationRequested) work.Stop();
            else await work.RunAsync().ConfigureAwait(false);
        }
    }

    sealed class Calculation<TInput, TOutput>(IWorkflowDecisionOperator<TInput, TOutput> operation,
        TInput input, TimeProvider clock, TimeSpan remaining, CancellationToken token, CancellationToken deadlineToken) : Work
    {
        public long QueuedAt { get; } = clock.GetTimestamp();
        long started;
        public TimeSpan QueueTime => Volatile.Read(ref started) is var stamp && stamp != 0
            ? clock.GetElapsedTime(QueuedAt, stamp) : clock.GetElapsedTime(QueuedAt);
        public TimeSpan CalculationTime => Volatile.Read(ref started) is var stamp && stamp != 0
            ? clock.GetElapsedTime(stamp) : TimeSpan.Zero;
        public TaskCompletionSource<DecisionExecution<TOutput>> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override void Stop() => Completion.TrySetResult(new(
            deadlineToken.IsCancellationRequested || clock.GetElapsedTime(QueuedAt) >= remaining
                ? DecisionExecutionStatus.DeadlineExceeded : DecisionExecutionStatus.Cancelled,
            default, QueueTime, CalculationTime));
        public override async Task RunAsync()
        {
            if (token.IsCancellationRequested || clock.GetElapsedTime(QueuedAt) >= remaining) { Stop(); return; }
            Volatile.Write(ref started, clock.GetTimestamp());
            var queueTime = clock.GetElapsedTime(QueuedAt, started);
            try
            {
                var result = await operation.CalculateAsync(input, token).ConfigureAwait(false);
                var expired = token.IsCancellationRequested || clock.GetElapsedTime(QueuedAt) >= remaining;
                var status = !expired ? DecisionExecutionStatus.Completed
                    : deadlineToken.IsCancellationRequested || clock.GetElapsedTime(QueuedAt) >= remaining
                        ? DecisionExecutionStatus.DeadlineExceeded : DecisionExecutionStatus.Cancelled;
                Completion.TrySetResult(new(status,
                    expired ? default : result, queueTime, clock.GetElapsedTime(started)));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { Stop(); }
            catch (Exception error)
            {
                Completion.TrySetResult(new(DecisionExecutionStatus.Failed, default, queueTime, clock.GetElapsedTime(started), error));
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await stopping.CancelAsync().ConfigureAwait(false);
        pending.Writer.TryComplete();
        // A blocked calculator cannot make disposal admit another worker or rerun the decision.
        await Task.WhenAll(workers).ConfigureAwait(false);
        stopping.Dispose();
    }
}
