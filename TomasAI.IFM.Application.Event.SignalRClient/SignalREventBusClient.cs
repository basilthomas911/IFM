using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.SignalRClient;

namespace TomasAI.IFM.Application.Event.SignalRClient
{
    public class SignalREventBusClient : SignalRServiceApi , IEventBusClient
    {
        private readonly string _baseUri;

        public SignalREventBusClient(string baseUri)
        {
            _baseUri = baseUri;
        }

        public override string BaseUri => _baseUri;

        public async Task<ServiceResult<TResult>> PublishAsync<TResult>(object sender, IEvent[] domainEvents)
        {
            var serviceResult = new ServiceResult<TResult>(default(TResult));
            var hubConnection = await ConnectToHubAsync();
            if (hubConnection != null)
                foreach (var domainEvent in domainEvents)
                {
                    var serializedEvent = JsonConvert.SerializeObject(domainEvent, Formatting.None);
                    var serializedServiceResult = await hubConnection.InvokeAsync<string>($"PublishAsync", domainEvent.EventId, domainEvent.GetType().AssemblyQualifiedName, serializedEvent);
                    serviceResult = JsonConvert.DeserializeObject<ServiceResult<TResult>>(serializedServiceResult);
                    if (!serviceResult.Success)
                        break;
                }
            return serviceResult;
        }

        public async Task PostAsync(object sender, IEvent[] domainEvents)
        {
            var hubConnection = await ConnectToHubAsync();
            if (hubConnection != null)
                foreach (var domainEvent in domainEvents)
                {
                    var serializedEvent = JsonConvert.SerializeObject(domainEvent, Formatting.None);
                    await hubConnection.SendAsync($"PostAsync", domainEvent.EventId, domainEvent.GetType().AssemblyQualifiedName, serializedEvent);
                }
        }
        
    }
}
