using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.Models
{
    public class FundOrderEventModel : BaseModel<FundOrderEventModel>
    {
        readonly IFundOrderUIEventConsumer _eventConsumer;

        /// <summary>
        /// fund order event model constructor
        /// </summary>
        /// <param name="fundEventConsumer"></param>
        public FundOrderEventModel(IFundOrderUIEventConsumer fundEventConsumer)
        {
            _eventConsumer = IsArgumentNull.Set(fundEventConsumer);
        }

        /// <summary>
        /// start listening for fund order notification events
        /// </summary>
        /// <param name="consumeEvents"></param>
        /// <param name="listenerAction"></param>
        public async Task StartFundOrderListenerAsync(ICollection<IEvent> consumeEvents, Func<IEvent, Task> listenerAction) 
            => await ExecuteAsync( () => _eventConsumer.StartAsync(consumeEvents, listenerAction) );

        /// <summary>
        /// stop listening for fund order notification events
        /// </summary>
        public async Task StopFundOrderListenerAsync() 
            => await ExecuteAsync( _eventConsumer.StopAsync );
        
    }
}
