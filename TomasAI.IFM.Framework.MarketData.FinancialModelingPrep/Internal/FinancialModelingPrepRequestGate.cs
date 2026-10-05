namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;

internal sealed class FinancialModelingPrepRequestGate
{
    private readonly SemaphoreSlim _semaphore;
    private readonly object _circuitLock = new();
    private int _consecutiveFailures;
    private DateTimeOffset? _breakUntilUtc;

    /// <summary>Initializes a new FinancialModelingPrepRequestGate instance.</summary>
    /// <param name="options">The configuration governing provider or feed operation.</param>
    public FinancialModelingPrepRequestGate(FinancialModelingPrepOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _semaphore = new SemaphoreSlim(options.MaximumConcurrentRequests, options.MaximumConcurrentRequests);
    }

    internal async ValueTask<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(_semaphore);
    }

    internal void ThrowIfCircuitOpen(TimeProvider timeProvider)
    {
        lock (_circuitLock)
        {
            if (_breakUntilUtc is { } breakUntil && breakUntil > timeProvider.GetUtcNow())
            {
                throw new FinancialModelingPrepUnavailableException("The FMP circuit breaker is open after repeated provider failures.");
            }

            if (_breakUntilUtc is not null)
            {
                _breakUntilUtc = null;
                _consecutiveFailures = 0;
            }
        }
    }

    internal void RecordSuccess()
    {
        lock (_circuitLock)
        {
            _consecutiveFailures = 0;
            _breakUntilUtc = null;
        }
    }

    internal void RecordTransientFailure(FinancialModelingPrepOptions options, TimeProvider timeProvider)
    {
        lock (_circuitLock)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= options.CircuitBreakerFailureThreshold)
            {
                _breakUntilUtc = timeProvider.GetUtcNow() + options.CircuitBreakerBreakDuration;
            }
        }
    }

    /// <summary>Initializes a new Releaser instance.</summary>
    /// <param name="semaphore">The concurrency permit returned when this lease is disposed.</param>
    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        /// <summary>Releases the resources owned by this instance.</summary>
        public void Dispose() => semaphore.Release();
    }
}
