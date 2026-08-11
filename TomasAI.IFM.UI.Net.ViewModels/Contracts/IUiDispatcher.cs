namespace TomasAI.IFM.UI.Net.Contracts;

/// <summary>
/// Dispatches presentation-state work to the UI thread without coupling shared
/// ViewModels to WinForms or WPF.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>
    /// Gets whether the caller already has access to the UI thread.
    /// </summary>
    bool CheckAccess();

    /// <summary>
    /// Executes an action on the UI thread and completes after the action has run.
    /// </summary>
    ValueTask InvokeAsync(
        Action action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a function on the UI thread and returns its result.
    /// </summary>
    ValueTask<TResult> InvokeAsync<TResult>(
        Func<TResult> function,
        CancellationToken cancellationToken = default);
}
