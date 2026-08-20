using System;
using System.Windows;

namespace TomasAI.IFM.Application.ServerManager;

public interface IUiDispatcher
{
    void Post(Action action);
}

public sealed class WpfUiDispatcher : IUiDispatcher
{
    public void Post(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _ = dispatcher.BeginInvoke(action);
    }
}
