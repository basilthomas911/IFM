using System.Threading.Channels;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public interface IDatabentoOperationRunner : IAsyncDisposable
{
    Task<T> RunAsync<T>(
        Func<IDatabentoMarketDataQueries, T> operation,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Executes synchronous native query calls on a fixed set of observed workers
/// behind bounded admission. It never creates a task or thread per request.
/// </summary>
public sealed class DatabentoOperationRunner : IDatabentoOperationRunner
{
    private readonly Channel<WorkItem> _queue;
    private readonly IDatabentoMarketDataQueries[] _queries;
    private readonly Task[] _workers;
    private readonly CancellationTokenSource _shutdown = new();
    private int _disposed;

    public DatabentoOperationRunner(
        IReadOnlyList<IDatabentoMarketDataQueries> queries,
        int queueCapacity)
    {
        ArgumentNullException.ThrowIfNull(queries);
        if (queries.Count == 0)
            throw new ArgumentException("At least one provider query worker is required.", nameof(queries));
        if (queueCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(queueCapacity));

        _queries = queries.ToArray();
        _queue = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(queueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = queries.Count == 1,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        _workers = new Task[_queries.Length];
        for (var index = 0; index < _queries.Length; index++)
        {
            var workerIndex = index;
            _workers[index] = Task.Run(() => WorkerAsync(_queries[workerIndex]));
        }
    }

    public Task<T> RunAsync<T>(
        Func<IDatabentoMarketDataQueries, T> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();

        var item = new WorkItem<T>(operation, cancellationToken);
        if (!_queue.Writer.TryWrite(item))
            throw new InvalidOperationException("The bounded DataBento operation queue is full.");
        return item.Task;
    }

    private async Task WorkerAsync(IDatabentoMarketDataQueries queries)
    {
        try
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(_shutdown.Token)
                .ConfigureAwait(false))
            {
                item.Execute(queries);
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _queue.Writer.TryComplete();
        try { await Task.WhenAll(_workers).ConfigureAwait(false); }
        finally { _shutdown.Dispose(); }
    }

    private abstract class WorkItem
    {
        internal abstract void Execute(IDatabentoMarketDataQueries queries);
    }

    private sealed class WorkItem<T>(
        Func<IDatabentoMarketDataQueries, T> operation,
        CancellationToken cancellationToken) : WorkItem
    {
        private readonly TaskCompletionSource<T> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task<T> Task => _completion.Task;

        internal override void Execute(IDatabentoMarketDataQueries queries)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _completion.TrySetCanceled(cancellationToken);
                return;
            }

            try { _completion.TrySetResult(operation(queries)); }
            catch (Exception exception) { _completion.TrySetException(exception); }
        }
    }
}
