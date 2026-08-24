namespace TomasAI.IFM.UI.Net.Services.Subscriptions;

/// <summary>Serializes subscription startup, shutdown, and asynchronous disposal.</summary>
internal sealed class OwnedUiEventSubscription(
    Func<CancellationToken, ValueTask> start,
    Func<ValueTask> stop) : IUiEventSubscription
{
    readonly Func<CancellationToken, ValueTask> _start =
        start ?? throw new ArgumentNullException(nameof(start));
    readonly Func<ValueTask> _stop = stop ?? throw new ArgumentNullException(nameof(stop));
    readonly SemaphoreSlim _gate = new(1, 1);
    bool _isStarted;
    bool _isDisposed;

    /// <inheritdoc />
    public bool IsStarted => _isStarted;

    /// <inheritdoc />
    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_isStarted)
                return;
            await _start(cancellationToken).ConfigureAwait(false);
            _isStarted = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        await StopCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;
        await StopCoreAsync(CancellationToken.None).ConfigureAwait(false);
        _isDisposed = true;
        _gate.Dispose();
    }

    async ValueTask StopCoreAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_isStarted)
                return;
            await _stop().ConfigureAwait(false);
            _isStarted = false;
        }
        finally
        {
            _gate.Release();
        }
    }
}
