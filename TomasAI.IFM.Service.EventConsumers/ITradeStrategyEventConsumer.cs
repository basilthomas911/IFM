using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Service.EventConsumers
{
    public interface ITradeStrategyEventConsumer
    {
        Task StartAsync(Guid siteId, ICollection<IEvent> consumeEvents, Action<IEvent> eventAction);
        Task StopAsync();

    }
}
