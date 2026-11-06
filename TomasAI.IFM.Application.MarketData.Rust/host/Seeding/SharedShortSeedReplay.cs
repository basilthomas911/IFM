using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.MarketData.Rust.Host.Seeding;

/// <summary>Combines simultaneous short-frame requests for one contract and session into one qualified replay.</summary>
internal sealed class SharedShortSeedReplay
{
    static readonly TimeSpan JoinWindow = TimeSpan.FromMilliseconds(10);
    readonly object gate = new();
    readonly Dictionary<(string ContractId, string SourceVersion, DateOnly ValueDate), Batch> pending = [];

    internal sealed record Window(TimeFrameType Frame, MarketSessionBounds[] Intervals, DateTimeOffset Cutoff);

    sealed class Request(Window window, CancellationToken token)
    {
        public Window Window { get; } = window;
        public CancellationToken Token { get; } = token;
        public TaskCompletionSource<FuturesTradeSessionBarReadModel[]> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    sealed class Batch
    {
        public List<Request> Requests { get; } = [];
        public TaskCompletionSource Paired { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public Task<FuturesTradeSessionBarReadModel[]> ReadAsync(string contractId, string sourceVersion, Window window,
        Func<Window[], CancellationToken, Task<FuturesTradeSessionBarReadModel[][]>> read,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var key = (contractId, sourceVersion, window.Intervals[0].ValueDate);
        var request = new Request(window, token);
        Batch batch;
        bool start;
        lock (gate)
        {
            start = !pending.TryGetValue(key, out batch!);
            if (start)
            {
                batch = new Batch();
                pending.Add(key, batch);
            }
            batch.Requests.Add(request);
            if (batch.Requests[0].Window.Frame != window.Frame)
                batch.Paired.TrySetResult();
        }
        if (start)
            _ = RunAsync(key, batch, read);
        return request.Completion.Task.WaitAsync(token);
    }

    async Task RunAsync((string ContractId, string SourceVersion, DateOnly ValueDate) key, Batch batch,
        Func<Window[], CancellationToken, Task<FuturesTradeSessionBarReadModel[][]>> read)
    {
        // An arriving partner starts work immediately. A single-frame caller
        // waits at most one short join interval before opening its own replay.
        await Task.WhenAny(batch.Paired.Task, Task.Delay(JoinWindow)).ConfigureAwait(false);
        lock (gate)
            pending.Remove(key);

        foreach (var request in batch.Requests.Where(request => request.Token.IsCancellationRequested))
            request.Completion.TrySetCanceled(request.Token);
        var active = batch.Requests.Where(request => !request.Completion.Task.IsCompleted).ToArray();
        if (active.Length == 0) return;

        using var replayCancellation = new CancellationTokenSource();
        var waiters = active.Length;
        var registrations = new CancellationTokenRegistration[active.Length];
        try
        {
            for (var index = 0; index < active.Length; index++)
            {
                var request = active[index];
                registrations[index] = request.Token.UnsafeRegister(_ =>
                {
                    if (Interlocked.Decrement(ref waiters) == 0)
                        replayCancellation.Cancel();
                }, null);
            }

            var bars = await read(active.Select(request => request.Window).ToArray(), replayCancellation.Token)
                .ConfigureAwait(false);
            for (var index = 0; index < active.Length; index++)
                active[index].Completion.TrySetResult(bars[index]);
        }
        catch (OperationCanceledException) when (replayCancellation.IsCancellationRequested)
        {
            foreach (var request in active)
                request.Completion.TrySetCanceled(request.Token);
        }
        catch (Exception exception)
        {
            foreach (var request in active)
                request.Completion.TrySetException(exception);
        }
        finally
        {
            foreach (var registration in registrations)
                registration.Dispose();
        }
    }
}
