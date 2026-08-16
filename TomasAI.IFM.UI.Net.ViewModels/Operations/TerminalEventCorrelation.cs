using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.Net.ViewModels.Operations;

/// <summary>
/// Correlates one accepted command with its terminal event while closing the event-before-response race.
/// </summary>
public sealed class TerminalEventCorrelation
{
    readonly object _gate = new();
    readonly Dictionary<Guid, IEvent> _earlyEvents = [];
    readonly int _earlyEventCapacity;
    TaskCompletionSource<IEvent>? _completion;
    Guid _commandId;
    bool _isActive;

    /// <summary>Creates a single-attempt correlation owner with bounded early-event storage.</summary>
    public TerminalEventCorrelation(int earlyEventCapacity = 32)
    {
        if (earlyEventCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(earlyEventCapacity));
        _earlyEventCapacity = earlyEventCapacity;
    }

    /// <summary>Gets the accepted command identifier while the current attempt is active.</summary>
    public Guid CommandId
    {
        get
        {
            lock (_gate)
                return _commandId;
        }
    }

    /// <summary>Starts an attempt before its command is submitted.</summary>
    public void BeginAttempt()
    {
        lock (_gate)
        {
            if (_isActive)
                throw new InvalidOperationException("A terminal-event correlation attempt is already active.");
            _isActive = true;
            _commandId = Guid.Empty;
            _completion = null;
            _earlyEvents.Clear();
        }
    }

    /// <summary>Waits for the event carrying the accepted command identifier.</summary>
    public Task<IEvent> AwaitAsync(Guid commandId, CancellationToken cancellationToken)
        => PrepareAwait(commandId).WaitAsync(cancellationToken);

    /// <summary>Waits for the correlated event until the bounded observation timeout expires.</summary>
    public Task<IEvent> AwaitAsync(
        Guid commandId,
        TimeSpan timeout,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        ArgumentNullException.ThrowIfNull(timeProvider);
        return PrepareAwait(commandId).WaitAsync(timeout, timeProvider, cancellationToken);
    }

    /// <summary>
    /// Publishes a candidate terminal event. Returns true only when the event was buffered or matched.
    /// </summary>
    public bool TryPublish(IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (@event.CommandId == Guid.Empty)
            return false;

        TaskCompletionSource<IEvent>? completion;
        lock (_gate)
        {
            if (!_isActive)
                return false;
            if (_commandId == Guid.Empty)
            {
                if (_earlyEvents.ContainsKey(@event.CommandId)
                    || _earlyEvents.Count < _earlyEventCapacity)
                {
                    _earlyEvents[@event.CommandId] = @event;
                    return true;
                }
                return false;
            }
            if (_commandId != @event.CommandId)
                return false;
            completion = _completion;
        }
        return completion?.TrySetResult(@event) == true;
    }

    /// <summary>Ends the current attempt and releases all buffered correlation state.</summary>
    public void EndAttempt()
    {
        TaskCompletionSource<IEvent>? completion;
        lock (_gate)
        {
            completion = _completion;
            _completion = null;
            _commandId = Guid.Empty;
            _earlyEvents.Clear();
            _isActive = false;
        }
        completion?.TrySetCanceled();
    }

    Task<IEvent> PrepareAwait(Guid commandId)
    {
        if (commandId == Guid.Empty)
            throw new ArgumentException("A non-empty command identifier is required.", nameof(commandId));

        IEvent? earlyEvent;
        TaskCompletionSource<IEvent> completion;
        lock (_gate)
        {
            if (!_isActive)
                throw new InvalidOperationException("BeginAttempt must be called before awaiting a terminal event.");
            if (_commandId != Guid.Empty)
                throw new InvalidOperationException("The active attempt already has a command identifier.");

            _commandId = commandId;
            completion = new TaskCompletionSource<IEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _completion = completion;
            _earlyEvents.Remove(commandId, out earlyEvent);
            _earlyEvents.Clear();
        }
        if (earlyEvent is not null)
            completion.TrySetResult(earlyEvent);
        return completion.Task;
    }
}
