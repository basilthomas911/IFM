using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Models
{
    public class EventModel : BaseModel<EventModel>, IEventModel
    {
        readonly ICommandResponseUIEventConsumer _commandResponseEventConsumer;
        Guid _siteId;
   
        public EventModel(ICommandResponseUIEventConsumer commandResponseEventConsumer)
        {
            _commandResponseEventConsumer = commandResponseEventConsumer;
        }

        public Guid SiteId => _siteId;

        public bool WaitingForCommandResponse { get; set; }

        public void SetSiteId(Guid siteId) => _siteId = siteId;

        public async Task StartCommandResponseEventConsumerAsync(EventTopic eventTopic, ICollection<IEvent> commandResponseEvents, Action<IEvent> eventAction) 
        {
            var eventSource = $"{eventTopic}";
            foreach (var e in commandResponseEvents)
                e.SetEventSource(eventSource);
            await _commandResponseEventConsumer.StartAsync(commandResponseEvents, eventAction);
            WaitingForCommandResponse = true;
        }

        public async Task StopCommandResponseEventConsumerAsync()
        {
            await _commandResponseEventConsumer.StopAsync();
            WaitingForCommandResponse = false;
        }

    }
}
