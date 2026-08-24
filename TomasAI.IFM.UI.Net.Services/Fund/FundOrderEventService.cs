using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.Services.Fund
{
    /// <summary>Provides the FundOrderEventService UI service boundary.</summary>
    public class FundOrderEventService : UiServiceBase<FundOrderEventService>
    {
        readonly IFundOrderUIEventConsumer _eventConsumer;

        /// <summary>
        /// fund order event model constructor
        /// </summary>
        /// <param name="fundEventConsumer"></param>
        public FundOrderEventService(IFundOrderUIEventConsumer fundEventConsumer)
        {
            _eventConsumer = IsArgumentNull.Set(fundEventConsumer);
        }

        /// <summary>
        /// start listening for fund order notification events
        /// </summary>
        /// <param name="listenerAction"></param>
        public async ValueTask StartFundOrderListenerAsync(Func<IEvent, ValueTask> listenerAction) 
            => await ExecuteValueTaskAsync( () => _eventConsumer.StartAsync(listenerAction) );

        /// <summary>
        /// stop listening for fund order notification events
        /// </summary>
        public async ValueTask StopFundOrderListenerAsync() 
            => await ExecuteValueTaskAsync( _eventConsumer.StopAsync );
        
    }
}
