using System.Collections.Concurrent;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed class ActiveRunRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runs = new();

    public ActiveRunRegistration Register(Guid runId, params CancellationToken[] tokens)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(tokens);
        if (!_runs.TryAdd(runId, source))
        {
            source.Dispose();
            throw new InvalidOperationException($"Run '{runId}' is already registered as active.");
        }

        return new ActiveRunRegistration(runId, source, this);
    }

    public bool RequestCancellation(Guid runId)
    {
        if (!_runs.TryGetValue(runId, out var source))
        {
            return false;
        }

        source.Cancel();
        return true;
    }

    public void CancelAll()
    {
        foreach (var source in _runs.Values)
        {
            source.Cancel();
        }
    }

    private void Complete(Guid runId, CancellationTokenSource source)
    {
        _runs.TryRemove(new KeyValuePair<Guid, CancellationTokenSource>(runId, source));
        source.Dispose();
    }

    public sealed class ActiveRunRegistration(
        Guid runId,
        CancellationTokenSource source,
        ActiveRunRegistry owner) : IDisposable
    {
        public CancellationToken Token => source.Token;

        public void Dispose() => owner.Complete(runId, source);
    }
}
