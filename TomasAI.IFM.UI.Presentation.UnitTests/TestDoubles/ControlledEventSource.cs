namespace TomasAI.IFM.UI.Presentation.UnitTests.TestDoubles;

/// <summary>
/// Provides a deterministic, single-listener event source for ViewModel and
/// lifecycle tests without requiring a NATS connection.
/// </summary>
public sealed class ControlledEventSource<TEvent> : IAsyncDisposable
{
    readonly SemaphoreSlim _publishGate = new(1, 1);
    CancellationTokenSource? _lifetime;
    Func<TEvent, CancellationToken, ValueTask>? _listener;

    /// <summary>
    /// Gets whether a listener is currently active.
    /// </summary>
    public bool IsRunning => _listener is not null;

    /// <summary>
    /// Starts the source with exactly one listener.
    /// </summary>
    public ValueTask StartAsync(Func<TEvent, CancellationToken, ValueTask> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        if (_listener is not null)
            throw new InvalidOperationException("The event source is already running.");

        _lifetime = new CancellationTokenSource();
        _listener = listener;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Publishes one event and awaits the listener. Concurrent publications are
    /// serialized so tests can assert lossless ordered behavior.
    /// </summary>
    public async ValueTask PublishAsync(
        TEvent eventData,
        CancellationToken cancellationToken = default)
    {
        var listener = _listener
            ?? throw new InvalidOperationException("The event source is not running.");
        var lifetime = _lifetime
            ?? throw new InvalidOperationException("The event source has no lifetime.");

        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lifetime.Token);
        await _publishGate.WaitAsync(linkedSource.Token);
        try
        {
            await listener(eventData, linkedSource.Token);
        }
        finally
        {
            _publishGate.Release();
        }
    }

    /// <summary>
    /// Stops the source, cancels an active listener, and waits for active
    /// publication to leave the serialization gate.
    /// </summary>
    public async ValueTask StopAsync()
    {
        var lifetime = _lifetime;
        if (lifetime is null)
            return;

        lifetime.Cancel();
        await _publishGate.WaitAsync();
        try
        {
            _listener = null;
            _lifetime = null;
        }
        finally
        {
            _publishGate.Release();
            lifetime.Dispose();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _publishGate.Dispose();
    }
}
