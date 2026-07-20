using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Log.ViewModels;
using TomasAI.IFM.Shared.Log.ServiceApi;

namespace TomasAI.IFM.StatusConsole.SignalRClient
{

    public class SignalRMarketDataStatusListener : SignalRStatusConsoleListener, IMarketDataStatusListener
    {
        public SignalRMarketDataStatusListener(IStatusConsoleServiceApiOptions options) :base(options)
        {
        }
    }
}
