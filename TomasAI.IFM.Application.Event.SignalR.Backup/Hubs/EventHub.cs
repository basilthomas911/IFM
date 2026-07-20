using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.AspNetCore.SignalR;
using TomasAI.IFM.Application.Event.SignalR.Services;

namespace TomasAI.IFM.Application.Event.SignalR.Hubs
{
    public class EventHub : Hub
    {
        private readonly IEventActionBlock _eventActionBlock;

        public EventHub(IEventActionBlock eventActionBlock)
            => _eventActionBlock = eventActionBlock;

        public async Task PublishEventAsync(long eventId, string eventTypeName, string serializedEvent)
            => await _eventActionBlock.PublishEventAsync(eventId, eventTypeName, serializedEvent);

        public void PostEvent(long eventId, string eventTypeName, string serializedEvent)
            => _eventActionBlock.PostEvent(eventId, eventTypeName, serializedEvent);
 
        public override async Task OnConnectedAsync()
            => await base.OnConnectedAsync();

        public override async Task OnDisconnectedAsync(Exception ex)
            => await base.OnDisconnectedAsync(ex);
    }
}
