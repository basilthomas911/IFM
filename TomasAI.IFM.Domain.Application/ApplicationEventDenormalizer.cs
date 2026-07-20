using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Application.ServiceApi;
using TomasAI.IFM.Shared.Application.Events;

namespace TomasAI.IFM.Domain.Application;

/// <summary>
/// application event denormalizer constructor
/// </summary>
/// <param name="eventProducer"></param>
/// <param name="logger"></param>
public class ApplicationEventDenormalizer(
    ILogger logger)
        : BaseEventQueueDenormalizer(default, logger), IEventDenormalizer<ApplicationBoundedContextState>
{

    /// <summary>
    /// denormalize events
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    protected override async Task<bool> DenormalizeAsync(IEvent e)
        => e switch {
            ApplicationStartupEvent o => await PostEventAsync(o),
            ApplicationShutdownEvent o => await PostEventAsync(o),
            _ => false,
        };

}
