using TomasAI.IFM.UI.Net.Models.Operations;

namespace TomasAI.IFM.UI.Net.ViewModels.Operations.Domain;

/// <summary>Correlates one accepted command with a transport-neutral terminal notification.</summary>
public sealed class TerminalNotificationCorrelation
{
    readonly object _gate = new();
    readonly Dictionary<Guid, TerminalNotificationUiModel> _earlyNotifications = [];
    readonly int _earlyNotificationCapacity;
    TaskCompletionSource<TerminalNotificationUiModel>? _completion;
    Guid _commandId;
    bool _isActive;

    /// <summary>Creates a correlation owner with bounded early-notification storage.</summary>
    /// <param name="earlyNotificationCapacity">The maximum number of early notifications retained.</param>
    public TerminalNotificationCorrelation(int earlyNotificationCapacity = 32)
    {
        if (earlyNotificationCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(earlyNotificationCapacity));
        _earlyNotificationCapacity = earlyNotificationCapacity;
    }

    /// <summary>Gets the accepted command identifier while an attempt is active.</summary>
    public Guid CommandId
    {
        get
        {
            lock (_gate)
                return _commandId;
        }
    }

    /// <summary>Starts an attempt before command submission.</summary>
    public void BeginAttempt()
    {
        lock (_gate)
        {
            if (_isActive)
                throw new InvalidOperationException("A terminal-notification attempt is already active.");
            _isActive = true;
            _commandId = Guid.Empty;
            _completion = null;
            _earlyNotifications.Clear();
        }
    }

    /// <summary>Waits for the terminal notification carrying the accepted command identifier.</summary>
    public Task<TerminalNotificationUiModel> AwaitAsync(
        Guid commandId,
        CancellationToken cancellationToken)
        => PrepareAwait(commandId).WaitAsync(cancellationToken);

    /// <summary>Waits for the correlated notification until the bounded observation timeout expires.</summary>
    /// <param name="commandId">The accepted command identifier.</param>
    /// <param name="timeout">The maximum time to observe the terminal notification.</param>
    /// <param name="timeProvider">The clock used to measure the timeout.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    public Task<TerminalNotificationUiModel> AwaitAsync(
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

    /// <summary>Publishes a candidate notification and returns whether it was buffered or matched.</summary>
    public bool TryPublish(TerminalNotificationUiModel notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (notification.CommandId == Guid.Empty)
            return false;

        TaskCompletionSource<TerminalNotificationUiModel>? completion;
        lock (_gate)
        {
            if (!_isActive)
                return false;
            if (_commandId == Guid.Empty)
            {
                if (_earlyNotifications.ContainsKey(notification.CommandId)
                    || _earlyNotifications.Count < _earlyNotificationCapacity)
                {
                    _earlyNotifications[notification.CommandId] = notification;
                    return true;
                }
                return false;
            }
            if (_commandId != notification.CommandId)
                return false;
            completion = _completion;
        }
        return completion?.TrySetResult(notification) == true;
    }

    /// <summary>Ends the current attempt and releases buffered correlation state.</summary>
    public void EndAttempt()
    {
        TaskCompletionSource<TerminalNotificationUiModel>? completion;
        lock (_gate)
        {
            completion = _completion;
            _completion = null;
            _commandId = Guid.Empty;
            _earlyNotifications.Clear();
            _isActive = false;
        }
        completion?.TrySetCanceled();
    }

    Task<TerminalNotificationUiModel> PrepareAwait(Guid commandId)
    {
        if (commandId == Guid.Empty)
            throw new ArgumentException("A non-empty command identifier is required.", nameof(commandId));

        TerminalNotificationUiModel? earlyNotification;
        TaskCompletionSource<TerminalNotificationUiModel> completion;
        lock (_gate)
        {
            if (!_isActive)
                throw new InvalidOperationException("BeginAttempt must be called before awaiting a terminal notification.");
            if (_commandId != Guid.Empty)
                throw new InvalidOperationException("The active attempt already has a command identifier.");

            _commandId = commandId;
            completion = new TaskCompletionSource<TerminalNotificationUiModel>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _completion = completion;
            _earlyNotifications.Remove(commandId, out earlyNotification);
            _earlyNotifications.Clear();
        }
        if (earlyNotification is not null)
            completion.TrySetResult(earlyNotification);
        return completion.Task;
    }
}
