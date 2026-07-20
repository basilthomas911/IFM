using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventService;

namespace TomasAI.IFM.Service.AlgoTrader
{
    public class AlgoTraderService2 : BaseEventService, IAlgoTraderService2
    {

        /// <summary>
        /// create algo trader service to execute events via external event handlers
        /// </summary>
        /// <param name="eventHandlerResolver"></param>
        /// <param name="logger"></param>
        public AlgoTraderService2(
            IEventServiceHandlerResolver eventHandlerResolver,
            ILogger<AlgoTraderService2> logger):base(eventHandlerResolver, logger)
        {
        }

        protected override string ServiceName => GetType().Name;

    }
}
