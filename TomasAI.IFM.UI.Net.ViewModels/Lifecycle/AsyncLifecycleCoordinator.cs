using TomasAI.IFM.UI.Net.Contracts;

namespace TomasAI.IFM.UI.Net.ViewModels.Lifecycle;

/// <summary>
/// Coordinates idempotent initialization, cooperative cancellation, retained background work, and asynchronous
/// cleanup for a presentation component.
/// </summary>
public sealed class AsyncLifecycleCoordinator : IAsyncLifecycle, IAsyncDisposable
{
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly object _lifetimeGate = new();
    readonly Func<CancellationToken, Task> _initialize;
    readonly Func<CancellationToken, Task> _stop;
    readonly List<Task> _ownedTasks = [];
    CancellationTokenSource? _lifetimeCancellation;
    LifecycleState _state;

    /// <summary>
    /// Creates a lifecycle coordinator around component-specific initialization and cleanup functions.
    /// </summary>
    public AsyncLifecycleCoordinator(
        Func<CancellationToken, Task> initialize,
        Func<CancellationToken, Task> stop)
    {
        _initialize = initialize ?? throw new ArgumentNullException(nameof(initialize));
        _stop = stop ?? throw new ArgumentNullException(nameof(stop));
    }

    /// <summary>
    /// Gets whether initialization completed and the component is accepting owned work.
    /// </summary>
    public bool IsRunning => _state == LifecycleState.Running;

    /// <summary>
    /// Initializes the component once. Repeated calls while running complete without starting duplicate resources.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_state == LifecycleState.Running)
                return;
            if (_state is LifecycleState.Initializing or LifecycleState.Stopping)
                throw new InvalidOperationException($"Cannot initialize while lifecycle state is {_state}.");

            _state = LifecycleState.Initializing;
            var lifetimeCancellation = new CancellationTokenSource();
            lock (_lifetimeGate)
                _lifetimeCancellation = lifetimeCancellation;
            using var initializationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lifetimeCancellation.Token);
            try
            {
                await _initialize(initializationCancellation.Token).ConfigureAwait(false);
                _state = LifecycleState.Running;
            }
            catch (Exception initializationFailure)
            {
                lifetimeCancellation.Cancel();
                await AwaitOwnedTasksAsync().ConfigureAwait(false);
                Exception? cleanupFailure = null;
                try
                {
                    await _stop(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    cleanupFailure = ex;
                }
                ClearLifetimeCancellation(lifetimeCancellation);
                _state = LifecycleState.Stopped;
                if (cleanupFailure is not null)
                    throw new AggregateException(initializationFailure, cleanupFailure);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Starts and retains background work owned by this lifecycle. The work receives the component lifetime token.
    /// </summary>
    public Task RunAsync(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_state is not (LifecycleState.Initializing or LifecycleState.Running)
            || _lifetimeCancellation is null)
        {
            throw new InvalidOperationException("The component must be initializing or running before it can own background work.");
        }

        var task = operation(_lifetimeCancellation.Token);
        lock (_ownedTasks)
            _ownedTasks.Add(task);
        return task;
    }

    /// <summary>
    /// Cancels the component lifetime, awaits all retained work, and invokes component cleanup once.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Initialization owns the lifecycle gate until it completes. Signal its lifetime token before
        // waiting for that gate so a close request can interrupt in-flight startup work instead of
        // deadlocking behind it.
        CancelLifetime();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_state is LifecycleState.Created or LifecycleState.Stopped)
                return;

            _state = LifecycleState.Stopping;
            CancelLifetime();

            Exception? backgroundFailure = null;
            try
            {
                await AwaitOwnedTasksAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                backgroundFailure = ex;
            }

            Exception? cleanupFailure = null;
            try
            {
                await _stop(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                cleanupFailure = ex;
            }
            finally
            {
                ClearLifetimeCancellation();
                _state = LifecycleState.Stopped;
            }

            if (backgroundFailure is not null)
                throw backgroundFailure;
            if (cleanupFailure is not null)
                throw cleanupFailure;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Stops the component and releases synchronization resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _gate.Dispose();
    }

    async Task AwaitOwnedTasksAsync()
    {
        Task[] tasks;
        lock (_ownedTasks)
        {
            tasks = [.. _ownedTasks];
            _ownedTasks.Clear();
        }

        if (tasks.Length == 0)
            return;

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation?.IsCancellationRequested == true)
        {
            // Expected cooperative lifetime cancellation.
        }
    }

    void CancelLifetime()
    {
        lock (_lifetimeGate)
            _lifetimeCancellation?.Cancel();
    }

    void ClearLifetimeCancellation(CancellationTokenSource? expected = null)
    {
        lock (_lifetimeGate)
        {
            if (expected is not null && !ReferenceEquals(_lifetimeCancellation, expected))
                return;

            _lifetimeCancellation?.Dispose();
            _lifetimeCancellation = null;
        }
    }

    enum LifecycleState
    {
        Created,
        Initializing,
        Running,
        Stopping,
        Stopped
    }
}
