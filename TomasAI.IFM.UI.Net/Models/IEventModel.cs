using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.Models
{
    public interface IEventModel
    {
        Guid SiteId { get; }

        void SetSiteId(Guid siteId);

        Task StartCommandResponseEventConsumerAsync(EventTopic eventTopic, ICollection<IEvent> commandResponseEvents, Action<IEvent> eventAction);

        Task StopCommandResponseEventConsumerAsync();

    }
}
