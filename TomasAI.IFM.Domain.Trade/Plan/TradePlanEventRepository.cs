using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;

namespace TomasAI.IFM.Domain.Trade.Plan;

/// <summary>
/// create trade plan event repository
/// </summary>
/// <param name="aggFactory"></param>
/// <param name="dbEventSource"></param>
/// <param name="eventDenormalizer"></param>
/// <param name="logger"></param>
public class TradePlanEventRepository(
    IBoundedContextFactory aggFactory,
    IEventSourceDbContext dbEventSource,
    IEventDenormalizer<TradePlanBoundedContextState> eventDenormalizer,
    ILogger<BaseEventSourceRepository> logger) 
    : BaseEventSourceRepository(aggFactory, dbEventSource, eventDenormalizer, logger), IEventRepository<TradePlanBoundedContextState>
{
    /// <summary>
    /// load trade plan bounded context
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public async Task<IBoundedContext<TradePlanBoundedContextState>> LoadBoundedContextAsync(ICommand command)
        => await LoadBoundedContextFromSnapshotAsync<TradePlanBoundedContext, TradePlanBoundedContextState, TradePlanUpdatedEvent>(command);

    /// <summary>
    /// save trade plan bounded context state change events
    /// </summary>
    /// <param name="boundedContextState"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async Task SaveBoundedContextAsync(IBoundedContextState<TradePlanBoundedContextState> boundedContextState, ICommand command)
        => await SaveBoundedContextAsync<TradePlanBoundedContextState>(boundedContextState, command);
 }
