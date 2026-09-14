namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;

/// <summary>
/// Allows at most one Futures ITI generation operation to run while immediately rejecting
/// additional realtime triggers. It does not retain, queue, or replay skipped ticks.
/// </summary>
public sealed class FuturesItiSignalGenerationGate
{
    sealed class GenerationLease
    {
        internal TaskCompletionSource Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    GenerationLease? active;

    /// <summary>Gets whether a Futures ITI generation operation is currently active.</summary>
    public bool IsBusy => Volatile.Read(ref active) is not null;

    /// <summary>
    /// Starts the supplied generation operation when the gate is free.
    /// </summary>
    /// <param name="onStarted">Records the successful Free-to-Busy transition before generation begins.</param>
    /// <param name="generate">The complete asynchronous generation operation.</param>
    /// <returns><see langword="true"/> when generation started; otherwise <see langword="false"/>.</returns>
    public bool TryStart(Action onStarted, Func<ValueTask> generate)
    {
        ArgumentNullException.ThrowIfNull(onStarted);
        ArgumentNullException.ThrowIfNull(generate);
        if (Volatile.Read(ref active) is not null)
            return false;

        var lease = new GenerationLease();
        if (Interlocked.CompareExchange(ref active, lease, null) is not null)
            return false;

        try
        {
            onStarted();
            _ = RunAsync(lease, generate);
            return true;
        }
        catch
        {
            _ = Interlocked.CompareExchange(ref active, null, lease);
            throw;
        }
    }

    /// <summary>Waits for the active generation operation, if any, during orderly actor shutdown.</summary>
    public async ValueTask WaitForIdleAsync()
    {
        while (Volatile.Read(ref active) is { } lease)
            await lease.Completion.Task.ConfigureAwait(false);
    }

    async Task RunAsync(GenerationLease lease, Func<ValueTask> generate)
    {
        try
        {
            await generate().ConfigureAwait(false);
            lease.Completion.TrySetResult();
        }
        catch (Exception exception)
        {
            lease.Completion.TrySetException(exception);
        }
        finally
        {
            _ = Interlocked.CompareExchange(ref active, null, lease);
        }
    }
}
