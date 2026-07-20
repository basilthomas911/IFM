using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;

namespace TomasAI.IFM.Domain.Trade.Plan.ForwardLossLimit;

/// <summary>
/// Provides functionality for loading and saving bounded context state changes related to trade plan forward loss limit
/// events.
/// </summary>
/// <remarks>This repository is responsible for managing the persistence and retrieval of  <see
/// cref="TradePlanForwardLossLimitBoundedContextState"/> instances, including handling snapshots and events. It extends the
/// <see cref="BaseEventSourceRepository"/> to provide specialized behavior for trade plan forward loss limit
/// aggregates.</remarks>
/// <param name="aggFactory"></param>
/// <param name="dbEventSource"></param>
/// <param name="eventDenormalizer"></param>
/// <param name="logger"></param>
public class TradePlanForwardLossLimitEventRepository(
    IBoundedContextFactory aggFactory,
    IEventSourceDbContext dbEventSource,
    IEventDenormalizer<TradePlanForwardLossLimitBoundedContextState> eventDenormalizer,
    ILogger<BaseEventSourceRepository> logger)
    : BaseEventSourceRepository(aggFactory, dbEventSource, eventDenormalizer, logger), IEventRepository<TradePlanForwardLossLimitBoundedContextState>
{
    /// <summary>
    /// load trade plan forward loss limit bounded context
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public async Task<IBoundedContext<TradePlanForwardLossLimitBoundedContextState>> LoadBoundedContextAsync(ICommand command)
        => await LoadBoundedContextFromSnapshotAsync<TradePlanForwardLossLimitBoundedContext, TradePlanForwardLossLimitBoundedContextState, TradePlanForwardLossLimitClearedEvent>(command);

    /// <summary>
    /// save trade plan forward loss limit bounded context state change events
    /// </summary>
    /// <param name="boundedContextState"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async Task SaveBoundedContextAsync(IBoundedContextState<TradePlanForwardLossLimitBoundedContextState> boundedContextState, ICommand command)
        => await SaveBoundedContextAsync<TradePlanForwardLossLimitBoundedContextState>(boundedContextState, command);
 }
