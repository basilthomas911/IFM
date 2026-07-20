using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventService;

namespace TomasAI.IFM.Service.Fund;

/// <summary>
/// create fund event service to execute events via external event handlers
/// </summary>
/// <param name="eventHandlerResolver"></param>
/// <param name="logger"></param>
public class FundEventService(
    IEventServiceHandlerResolver eventHandlerResolver,
    ILogger<IFundEventService> logger) : BaseEventService(eventHandlerResolver, logger), IFundEventService
{
    protected override string ServiceName => GetType().Name;
}
