using TomasAI.IFM.UI.Net.Contracts;

namespace TomasAI.IFM.UI.Net.Views.Presentation;

/// <summary>
/// Marshals presentation work through a WinForms control handle.
/// </summary>
public sealed class WinFormsUiDispatcher(Control control) : IUiDispatcher
{
    readonly Control _control = control ?? throw new ArgumentNullException(nameof(control));

    public bool CheckAccess() => !_control.InvokeRequired;

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_control.IsDisposed || _control.Disposing)
            return;
        _control.BeginInvoke(action);
    }

    public async ValueTask InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        if (_control.IsDisposed || _control.Disposing)
            return;
        if (CheckAccess())
        {
            action();
            return;
        }

        try
        {
            await _control.InvokeAsync(action, cancellationToken);
        }
        catch (InvalidOperationException) when (_control.IsDisposed || _control.Disposing)
        {
        }
        catch (ObjectDisposedException) when (_control.IsDisposed || _control.Disposing)
        {
        }
    }

    public async ValueTask<TResult> InvokeAsync<TResult>(
        Func<TResult> function,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(function);
        cancellationToken.ThrowIfCancellationRequested();
        if (_control.IsDisposed || _control.Disposing)
            return default!;
        if (CheckAccess())
            return function();

        try
        {
            return await _control.InvokeAsync(function, cancellationToken);
        }
        catch (InvalidOperationException) when (_control.IsDisposed || _control.Disposing)
        {
            return default!;
        }
        catch (ObjectDisposedException) when (_control.IsDisposed || _control.Disposing)
        {
            return default!;
        }
    }
}
