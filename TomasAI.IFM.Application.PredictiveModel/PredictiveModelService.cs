using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventService;

namespace TomasAI.IFM.Application.PredictiveModel;

/// <summary>
/// predictive model service constructor
/// </summary>
/// <param name="eventHandlerResolver"></param>
/// <param name="logger"></param>
public class PredictiveModelService(
   IEventServiceHandlerResolver eventHandlerResolver,
   ILogger<PredictiveModelService> logger) 
    : BaseEventService(eventHandlerResolver, logger), IPredictiveModelService
{
    protected override string ServiceName => GetType().Name;
}
