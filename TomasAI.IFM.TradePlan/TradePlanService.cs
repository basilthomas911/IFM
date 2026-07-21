using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventService;

namespace TomasAI.IFM.TradePlan
{
    public class TradePlanService : BaseEventService, ITradePlanService
    {
        public TradePlanService(
            IEventServiceHandlerResolver eventHandlerResolver,
            ILogger<ITradePlanService> logger):base(eventHandlerResolver, logger)
        {
            logger.LogInformation("TradePlanService started");
        }

        protected override string ServiceName => GetType().Name;

    }
}
