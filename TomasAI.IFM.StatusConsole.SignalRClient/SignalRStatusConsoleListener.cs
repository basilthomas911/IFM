using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using TomasAI.IFM.Shared.Log.Events;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.SignalRClient;

namespace TomasAI.IFM.StatusConsole.SignalRClient
{

    public class SignalRStatusConsoleListener :  SignalRServiceListener, IStatusConsoleServiceApiListener
    {
        private readonly string _baseUri;
        private Action<StatusConsoleLoggedEvent> _eventAction;

        /// <summary>
        /// create status console listener 
        /// </summary>
        /// <param name="options"></param>
        public SignalRStatusConsoleListener(IStatusConsoleServiceApiOptions options)
        {
            _baseUri = options.BaseUri;
        }

        public override string BaseUri => _baseUri;

        public override void ConnectEvents(HubConnection hubConnection)
        {
            hubConnection.On<StatusConsoleLoggedEvent>("StatusConsoleLogUpdated", e => _eventAction(e));
            hubConnection.On<StatusConsoleLoggedEvent>("StatusConsoleLogError", e => _eventAction(e));
        }

        /// <summary>
        /// listen for status console log updates
        /// </summary>
        /// <typeparam name="StatusConsoleLogReadModel"></typeparam>
        /// <param name="eventAction"></param>
        /// <returns></returns>
        public async Task StartAsync( Action<StatusConsoleLoggedEvent> eventAction)
        {
            _eventAction = eventAction;
            await base.StartAsync();
        }

    }
}
