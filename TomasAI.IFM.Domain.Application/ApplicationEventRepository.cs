using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;

namespace TomasAI.IFM.Domain.Application;

/// <summary>
/// create application repository
/// </summary>
/// <param name="aggFactory"></param>
/// <param name="dbEventSource"></param>
/// <param name="eventDenormalizer"></param>
/// <param name="logger"></param>
public class ApplicationEventRepository(
    IBoundedContextFactory aggFactory,
    IEventSourceDbContext dbEventSource,
    IEventDenormalizer<ApplicationBoundedContextState> eventDenormalizer,
    ILogger<BaseEventSourceRepository> logger) 
    : BaseEventSourceRepository(aggFactory, dbEventSource, eventDenormalizer, logger), IEventRepository<ApplicationBoundedContextState>
{

    /// <summary>
    /// load empty application bounded context
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public async Task<IBoundedContext<ApplicationBoundedContextState>> LoadBoundedContextAsync(ICommand command) 
        => await LoadEmptyBoundedContextAsync<ApplicationBoundedContext, ApplicationBoundedContextState>();

    /// <summary>
    /// post application bounded context state events
    /// </summary>
    /// <param name="boundedContextState"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async Task SaveBoundedContextAsync(IBoundedContextState<ApplicationBoundedContextState> boundedContextState, ICommand command)
        => await PostEventsAsync(boundedContextState);
}
