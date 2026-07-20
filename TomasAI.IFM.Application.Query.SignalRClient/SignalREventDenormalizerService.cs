using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.SignalRClient;


namespace TomasAI.IFM.Application.Query.SignalRClient
{
    public class SignalREventDenormalizerService : SignalRServiceApi, IEventDenormalizerService
    {
        private readonly string _baseUri;

        /// <summary>
        /// create event denormalizer service client
        /// </summary>
        /// <param name="baseUri"></param>
        public SignalREventDenormalizerService(string baseUri)
        {
            _baseUri = baseUri;
        }

        public override string BaseUri => _baseUri;

        /// <summary>
        /// send domain event to write data to query store data or dernomalize data
        /// </summary>
        /// <param name="domainEvent"></param>
        /// <returns></returns>
        public async Task DenormalizeEventAsync(IEvent domainEvent)
        {
            var serializedEvent = JsonConvert.SerializeObject(domainEvent, Formatting.None);
            var queryHub = await ConnectToHubAsync();
            await queryHub?.SendAsync($"DenormalizeEventAsync", domainEvent.GetType().AssemblyQualifiedName, serializedEvent);
        }
       
    }
}
