using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventService;

namespace TomasAI.IFM.Service.PredictiveModel
{
    public class PredictiveModelService : BaseEventService, IPredictiveModelService
    {

        /// <summary>
        /// create predictive model service to execute events via external event handlers
        /// </summary>
        /// <param name="eventHandlerResolver"></param>
        /// <param name="logger"></param>
        public PredictiveModelService(
            IEventServiceHandlerResolver eventHandlerResolver,
            ILogger<PredictiveModelService> logger):base(eventHandlerResolver, logger)
        {
        }

        protected override string ServiceName => GetType().Name;

    }
}
