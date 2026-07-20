using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Event.SignalRClient
{
    public class SignalREventBusListenerOptions : IEventBusListenerOptions
    {
        private readonly string _baseUri;

        public SignalREventBusListenerOptions(string baseUri)
            => _baseUri = baseUri;

        public string BaseUri => _baseUri;
    }
}
