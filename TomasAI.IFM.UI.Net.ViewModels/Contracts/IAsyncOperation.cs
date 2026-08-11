using System.ComponentModel;

namespace TomasAI.IFM.UI.Net.Contracts;

/// <summary>
/// Represents cancellable presentation work whose completion and running state are observable.
/// </summary>
public interface IAsyncOperation : INotifyPropertyChanged
{
    /// <summary>
    /// Gets whether the operation is currently executing.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets whether a new execution can be started.
    /// </summary>
    bool CanExecute { get; }

    /// <summary>
    /// Gets the last unexpected execution failure, or <see langword="null"/> after a successful/new execution.
    /// </summary>
    Exception? LastFailure { get; }

    /// <summary>
    /// Executes the operation asynchronously.
    /// </summary>
    Task ExecuteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests cooperative cancellation of the current execution.
    /// </summary>
    void Cancel();

    /// <summary>
    /// Notifies presentation adapters that externally supplied execution conditions may have changed.
    /// </summary>
    void NotifyCanExecuteChanged();
}
