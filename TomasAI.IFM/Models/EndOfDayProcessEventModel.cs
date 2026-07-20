using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.Models
{
    public class EndOfDayProcessEventModel : BaseModel<EndOfDayProcessEventModel>
    {
        readonly IEndOfDayProcessUIEventConsumer _eventConsumer;

        public EndOfDayProcessEventModel(IEndOfDayProcessUIEventConsumer eodEventConsumer)
        {
            _eventConsumer = IsArgumentNull.Set(eodEventConsumer);
        }

        /// <summary>
        /// start listening for eod process notification events
        /// </summary>
        /// <param name="consumeEvents"></param>
        /// <param name="listenerAction"></param>
        public async Task StartEndOfDayProcessListenerAsync(ICollection<IEvent> consumeEvents, Func<IEvent, Task> listenerAction) 
            => await ExecuteAsync( () => _eventConsumer.StartAsync(consumeEvents, listenerAction));

        /// <summary>
        /// stop listening for eod process notification events
        /// </summary>
        public async Task StopEndOfDayProcessListenerAsync() 
            => await ExecuteAsync( _eventConsumer.StopAsync );
        
    }
}
