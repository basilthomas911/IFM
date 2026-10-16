using System;
using System.Windows;

namespace TomasAI.IFM.Application.ServerManager;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="Preserve the existing public host, configuration and reflection contracts used by external test assemblies.")]

public interface IUiDispatcher
{
    void Post(Action action);
}
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="Preserve the existing public host, configuration and reflection contracts used by external test assemblies.")]

public sealed class WpfUiDispatcher : IUiDispatcher
{
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _ = dispatcher.BeginInvoke(action);
    }
}
