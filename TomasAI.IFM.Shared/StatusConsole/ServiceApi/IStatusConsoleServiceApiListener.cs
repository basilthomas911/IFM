using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.SignalR;
using TomasAI.IFM.Shared.StatusConsole.Events;

namespace TomasAI.IFM.Shared.StatusConsole.ServiceApi;

public interface IStatusConsoleServiceApiListener : IServiceListener
{
    Task StartAsync(Action<StatusConsoleLoggedEvent> eventAction);
}

public interface IMarketDataStatusListener : IStatusConsoleServiceApiListener
{
}
