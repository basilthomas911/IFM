using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.SignalRClient;

namespace TomasAI.IFM.Application.Event.SignalRClient
{
    public class SignalREventBusListener : SignalRServiceListener, IEventBusListener
    {
        private readonly IEventBus _eventBus;
        private readonly string _baseUri;

        public SignalREventBusListener(IEventBus eventBus, IEventBusListenerOptions options)
        {
            _eventBus = eventBus;
            _baseUri = options.BaseUri;
        }

        public override string BaseUri => _baseUri;

        public override void ConnectEvents(HubConnection hubConnection)
        {
            hubConnection.On<IEvent[]>("PublishEvents", e => _eventBus.PublishAsync(this, e));
        }

    }
}
