using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventService;


namespace TomasAI.IFM.Service.OrderExecution
{
    /// <summary>
    /// event service that manages trade order execution via external event handlers
    /// </summary>
    public class OrderExecutionService : BaseEventService, IOrderExecutionService
    {
        /// <summary>
        /// order execution service constructor
        /// </summary>
        /// <param name="eventHandlerResolver"></param>
        /// <param name="logger"></param>
         public OrderExecutionService(
            IEventServiceHandlerResolver eventHandlerResolver,
            ILogger<OrderExecutionService> logger) : base(eventHandlerResolver, logger)
        {
        }

        protected override string ServiceName => "OrderExecutionService";
    }
}
