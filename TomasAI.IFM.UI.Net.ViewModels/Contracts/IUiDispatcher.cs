namespace TomasAI.IFM.UI.Net.Contracts;

/// <summary>
/// Marshals presentation work onto the active UI thread without coupling shared code to a UI framework.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>
    /// Gets whether the caller is already executing on the UI thread.
    /// </summary>
    bool CheckAccess();

    /// <summary>
    /// Queues an action for UI-thread execution when completion does not need to be observed.
    /// </summary>
    void Post(Action action);

    /// <summary>
    /// Executes an action on the UI thread and completes after the action has finished.
    /// </summary>
    ValueTask InvokeAsync(Action action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a function on the UI thread and returns its result.
    /// </summary>
    ValueTask<TResult> InvokeAsync<TResult>(
        Func<TResult> function,
        CancellationToken cancellationToken = default);
}
