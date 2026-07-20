using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.EventSourcing
{
    public interface IEventService
    {
        Task ExecuteAsync(IEvent @event);
        Task ExecuteAsync(IEvent @event, IEventService eventService);
    }
}
