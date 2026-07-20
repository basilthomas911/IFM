using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.SignalRClient;

namespace TomasAI.IFM.Application.Event.SignalRClient
{
    public class SignalREventBus : SignalRServiceApi, IEventBus
    {
        private readonly string _baseUri;
 
        public SignalREventBus(string baseUri)
        {
            _baseUri = baseUri;
        }

        public override string BaseUri => _baseUri;

        public async Task PublishAsync(object sender, IEvent[] domainEvents)
        {
            var hubConnection = await ConnectToHubAsync();
            if (hubConnection != null)
                foreach (var domainEvent in domainEvents)
                {
                    var serializedEvent = JsonConvert.SerializeObject(domainEvent, Formatting.None);
                    await TryExecute(3, () => hubConnection.InvokeAsync($"PublishEventAsync", domainEvent.EventId, domainEvent.GetType().AssemblyQualifiedName, serializedEvent));
                }
        }

        public async Task PostAsync(object sender, IEvent[] domainEvents)
        {
            var hubConnection = await ConnectToHubAsync();
            if (hubConnection != null)
                foreach (var domainEvent in domainEvents)
                {
                    var serializedEvent = JsonConvert.SerializeObject(domainEvent, Formatting.None);
                    await TryExecute(3, () => hubConnection.SendAsync($"PostEvent", domainEvent.EventId, domainEvent.GetType().AssemblyQualifiedName, serializedEvent));
                }
        }
        
        private async Task TryExecute(int maxRetryCount, Func<Task> tryAction)
        {
            for(var retryCount=0; retryCount < maxRetryCount; retryCount++)
            {
                try
                {
                    await tryAction();
                    break;
                }
                catch
                {
                    await Task.Delay(2000);
                }
            }
        }
    }
}
