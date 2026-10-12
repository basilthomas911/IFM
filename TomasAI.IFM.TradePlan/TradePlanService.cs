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
            ILogger<TradePlanService> logger) : base(eventHandlerResolver, logger)
        {
            Started(logger, null);
        }

        static readonly Action<ILogger, Exception?> Started = LoggerMessage.Define(LogLevel.Information, new EventId(1, "Started"), "TradePlanService started");

        protected override string ServiceName => GetType().Name;

    }
}
