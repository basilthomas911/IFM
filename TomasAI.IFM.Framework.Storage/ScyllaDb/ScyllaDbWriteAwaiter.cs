namespace TomasAI.IFM.Framework.Storage.ScyllaDb;

internal static class ScyllaDbWriteAwaiter
{
    public static Task<TResult> AwaitAsync<TResult>(
        Task<TResult> pendingExecution,
        CancellationToken cancellationToken)
        where TResult : IDisposable
    {
        ArgumentNullException.ThrowIfNull(pendingExecution);
        return cancellationToken.CanBeCanceled
            ? AwaitCancellableAsync(pendingExecution, cancellationToken)
            : pendingExecution;
    }

    static async Task<TResult> AwaitCancellableAsync<TResult>(
        Task<TResult> pendingExecution,
        CancellationToken cancellationToken)
        where TResult : IDisposable
    {
        try
        {
            return await pendingExecution.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // WaitAsync and the driver task can complete concurrently. Always transfer
            // ownership when the caller observes cancellation; the drain handles an
            // already-completed task without leaking its result.
            _ = DrainCancelledAsync(pendingExecution);
            throw;
        }
    }

    public static async Task DrainAndDisposeAsync(Task pendingExecution, IDisposable ownedResult)
    {
        ArgumentNullException.ThrowIfNull(pendingExecution);
        ArgumentNullException.ThrowIfNull(ownedResult);
        try
        {
            await pendingExecution.ConfigureAwait(false);
        }
        catch
        {
            // Cancellation has already been reported to the caller. Observe the
            // driver's eventual failure before releasing the result it is mutating.
        }
        finally
        {
            try
            {
                ownedResult.Dispose();
            }
            catch
            {
                // The original caller can no longer observe disposal failures.
            }
        }
    }

    public static async Task DrainAsync(Task pendingExecution, object lifetimeAnchor)
    {
        ArgumentNullException.ThrowIfNull(pendingExecution);
        ArgumentNullException.ThrowIfNull(lifetimeAnchor);
        try
        {
            await pendingExecution.ConfigureAwait(false);
        }
        catch
        {
            // Cancellation has already been reported to the caller. Observe the
            // driver's eventual failure while retaining the object it is mutating.
        }
        finally
        {
            GC.KeepAlive(lifetimeAnchor);
        }
    }

    static async Task DrainCancelledAsync<TResult>(Task<TResult> pendingExecution)
        where TResult : IDisposable
    {
        try
        {
            using var result = await pendingExecution.ConfigureAwait(false);
        }
        catch
        {
            // The caller observed cancellation. Drain the driver task so its exception and result resources
            // do not become unobserved when Cassandra completes the request in the background.
        }
    }
}
