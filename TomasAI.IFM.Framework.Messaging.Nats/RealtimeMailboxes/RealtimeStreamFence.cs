namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;

/// <summary>Shared bounded stream operations run concurrently; a queued reset excludes new readers until it completes.</summary>
internal sealed class RealtimeStreamFence
{
    readonly SemaphoreSlim entry = new(1, 1);
    readonly SemaphoreSlim readersGate = new(1, 1);
    readonly SemaphoreSlim resource = new(1, 1);
    int readers;

    public async ValueTask<IAsyncDisposable> EnterReadAsync(CancellationToken cancellationToken = default)
    {
        await entry.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await readersGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // The first reader owns the shared resource for the reader cohort.
                // A queued writer holds entry, so later readers cannot prolong that cohort.
                if (readers == 0) await resource.WaitAsync(cancellationToken).ConfigureAwait(false);
                readers++;
            }
            finally { readersGate.Release(); }
        }
        finally { entry.Release(); }
        return new Lease(this, writer: false);
    }

    public async ValueTask<IAsyncDisposable> EnterWriteAsync(CancellationToken cancellationToken = default)
    {
        await entry.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await resource.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch { entry.Release(); throw; }
        // Keep entry until the writer releases the resource; new readers cannot bypass it.
        return new Lease(this, writer: true);
    }

    async ValueTask ExitReadAsync()
    {
        // Releasing an acquired lease must not be cancelled and strand the shared cohort.
        await readersGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (--readers == 0) resource.Release();
        }
        finally { readersGate.Release(); }
    }

    void ExitWrite()
    {
        resource.Release(); entry.Release();
    }

    sealed class Lease(RealtimeStreamFence owner, bool writer) : IAsyncDisposable
    {
        RealtimeStreamFence? current = owner;
        public ValueTask DisposeAsync()
        {
            var target = Interlocked.Exchange(ref current, null);
            if (target is null) return ValueTask.CompletedTask;
            if (writer) { target.ExitWrite(); return ValueTask.CompletedTask; }
            return target.ExitReadAsync();
        }
    }
}
