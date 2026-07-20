using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Shared.TradeOrder.Events;

namespace TomasAI.IFM.Domain.Trade.Order;

/// <summary>
/// create trade order event repository
/// </summary>
/// <param name="aggFactory"></param>
/// <param name="dbEventSource"></param>
/// <param name="eventDenormalizer"></param>
/// <param name="logger"></param>
public class TradeOrderEventRepository(
    IBoundedContextFactory aggFactory,
    IEventSourceDbContext dbEventSource,
    IEventDenormalizer<TradeOrderBoundedContextState> eventDenormalizer,
    ILogger<BaseEventSourceRepository> logger) 
    : BaseEventSourceRepository(aggFactory, dbEventSource, eventDenormalizer, logger), IEventRepository<TradeOrderBoundedContextState>
{

    /// <summary>
    /// load trade order bounded context
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public async Task<IBoundedContext<TradeOrderBoundedContextState>> LoadBoundedContextAsync(ICommand command)
        => await LoadBoundedContextFromSnapshotAsync<TradeOrderBoundedContext, TradeOrderBoundedContextState, TradeOrderPlacedEvent>(command);

    /// <summary>
    /// save trade order bounded context state change events
    /// </summary>
    /// <param name="boundedContextState"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async Task SaveBoundedContextAsync(IBoundedContextState<TradeOrderBoundedContextState> boundedContextState, ICommand command)
        => await SaveBoundedContextAsync<TradeOrderBoundedContextState>(boundedContextState, command);
 }
