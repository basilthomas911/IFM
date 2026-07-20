using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Log.Events;

namespace TomasAI.IFM.Service.StatusConsole
{
    public interface IStatusConsoleEventConsumer
    {
        Task StartAsync(Action<StatusConsoleLoggedEvent> eventAction, Guid siteId);
        Task StopAsync(Guid siteId);
    }
}
