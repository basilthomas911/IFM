using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.Event.SignalR.Services
{
    public interface IEventActionBlock
    {
        void PostEvent(long eventId, string eventTypeName, string serializedEvent);
        Task PublishEventAsync(long eventId, string eventTypeName, string serializedEvent);
    }
}
