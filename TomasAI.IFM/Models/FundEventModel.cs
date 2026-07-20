using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.Models
{
    public class FundEventModel : BaseModel<FundEventModel>
    {
        readonly IFundUIEventConsumer _eventConsumer;

        /// <summary>
        /// fund event model constructor
        /// </summary>
        /// <param name="fundEventConsumer"></param>
        public FundEventModel(IFundUIEventConsumer fundEventConsumer)
        {
            _eventConsumer = IsArgumentNull.Set(fundEventConsumer);
        }

        /// <summary>
        /// start listening for fund notification events
        /// </summary>
        /// <param name="consumeEvents"></param>
        /// <param name="listenerAction"></param>
        public async Task StartFundListenerAsync(ICollection<IEvent> consumeEvents, Func<IEvent, Task> listenerAction) 
            => await ExecuteAsync( () => _eventConsumer.StartAsync(consumeEvents, listenerAction) );

        /// <summary>
        /// stop listening for fund notification events
        /// </summary>
        public async Task StopFundListenerAsync() 
            => await ExecuteAsync( _eventConsumer.StopAsync );
        
    }
}
