using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.SignalR
{
    public interface IServiceListener
    {
        Task StartAsync();
        Task StopAsync();
        Task ResetAsync();
    }

    public interface IServiceApiListener
    {
        Task StartAsync<TEvent>(Action<TEvent> eventAction);
        Task StopAsync<TEvent>();
        Task ResetAsync<TEvent>();
    }

    public interface IServiceApiListenerOptions
    {
        string BaseUri { get; }
    }
}
