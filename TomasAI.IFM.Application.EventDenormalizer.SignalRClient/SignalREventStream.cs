using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.SignalRClient;

namespace TomasAI.IFM.Application.EventStream.SignalRClient
{
    public class SignalREventStream : SignalRServiceApi,IEventStream
    {
        private readonly string _baseUri;

        public SignalREventStream(string baseUri)
        {
            _baseUri = baseUri;
        }

        public override string BaseUri => _baseUri;

        /// <summary>
        /// post events to event hub directly
        /// </summary>
        /// <param name="domainEvents"></param>
        /// <returns></returns>
        public async Task PostAsync(IEvent[] domainEvents)
        {
            try
            {
                var hubConnection = await ConnectToHubAsync();
                if (hubConnection != null)
                    foreach (var domainEvent in domainEvents)
                    {
                        var serializedEvent = JsonConvert.SerializeObject(domainEvent, Formatting.None);
                        await hubConnection.SendAsync($"PostEvent", domainEvent.EventId, domainEvent.GetType().AssemblyQualifiedName, serializedEvent);
                    }
            }
            catch { }
        }


    }
}
