using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Fixed asynchronous worker pool backed by a shared ready-mailbox queue.
/// </summary>
public sealed class ActorThreadPoolV2(
    IActorSupervisor supervisor,
    ILogger logger) : IActorThreadPool, IAsyncDisposable
{
    readonly IActorSupervisor _supervisor = IsArgumentNull.Set(supervisor);
    readonly ILogger _logger = IsArgumentNull.Set(logger);
    readonly ActorReadyQueue _readyQueue = new();
    ActorThreadV2[] _workers = [];
    int _initialized;
    int _disposed;

    public IActorThreadPool Initialize(int initialThreadCount)
    {
        if (initialThreadCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(initialThreadCount));
        if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
            throw new InvalidOperationException("The actor thread pool has already been initialized.");

        var workers = new ActorThreadV2[initialThreadCount];
        for (var index = 0; index < workers.Length; index++)
        {
            var worker = new ActorThreadV2(_supervisor, _logger, _readyQueue);
            worker.Start();
            workers[index] = worker;
        }
        Volatile.Write(ref _workers, workers);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IActorThread GetThread(ActorThreadId threadId)
    {
        IsArgumentNull.Check(threadId);
        ThrowIfUnavailable();
        if (!_supervisor.Children.ContainsKey(threadId.MailboxId))
            throw new KeyNotFoundException($"Actor with mailbox id '{threadId.MailboxId}' not found in context.");

        var workers = Volatile.Read(ref _workers);
        var hash = (uint)threadId.GetHashCode();
        return workers[hash % (uint)workers.Length];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<IActorThread> GetThreadAsync(ActorThreadId threadId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(GetThread(threadId));
    }

    // Mailbox execution is released by its atomic scheduling state; workers themselves are never leased.
    public void ReleaseThread(ActorThreadId threadId) => IsArgumentNull.Check(threadId);

    public int Count => Volatile.Read(ref _workers).Length;

    void ThrowIfUnavailable()
    {
        if (Volatile.Read(ref _initialized) == 0)
            throw new InvalidOperationException("The actor thread pool has not been initialized.");
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(ActorThreadPoolV2));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _readyQueue.Complete();
        var workers = Volatile.Read(ref _workers);
        foreach (var worker in workers)
            await worker.Completion.ConfigureAwait(false);
        foreach (var worker in workers)
            await worker.DisposeAsync().ConfigureAwait(false);

        Volatile.Write(ref _workers, []);
        GC.SuppressFinalize(this);
    }
}
