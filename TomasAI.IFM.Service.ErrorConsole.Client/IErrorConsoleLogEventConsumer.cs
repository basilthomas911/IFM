using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Log.Events;

namespace TomasAI.IFM.Service.ErrorConsole.Client
{
    public interface IErrorConsoleLogEventConsumer
    {
        Task StartAsync(Action<ErrorConsoleLoggedEvent> eventAction, Guid siteId);
        Task StopAsync(Guid siteId);
    }
}
