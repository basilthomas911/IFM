using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Plan;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.TradeAlgorithm.Events;
using TomasAI.IFM.Shared.TradeAlgorithm.Commands;

namespace TomasAI.IFM.Domain.Trade.Option.Algorithm;

/// <summary>
/// create trade plan event repository
/// </summary>
/// <param name="aggFactory"></param>
/// <param name="dbEventSource"></param>
/// <param name="eventDenormalizer"></param>
/// <param name="logger"></param>
public class OptionTradeAlgorithmEventRepository(
    IBoundedContextFactory aggFactory,
    IEventSourceDbContext dbEventSource,
    IEventDenormalizer<TradePlanBoundedContextState> eventDenormalizer,
    ILogger<BaseEventSourceRepository> logger) 
    : BaseEventSourceRepository(aggFactory, dbEventSource, eventDenormalizer, logger), IEventRepository<OptionTradeAlgorithmBoundedContextState>
{
    /// <summary>
    /// load  option trade algorithm bounded context
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public async Task<IBoundedContext<OptionTradeAlgorithmBoundedContextState>> LoadBoundedContextAsync(ICommand command)
        => command switch
        {
            ExecuteLongIronCondorAlgorithmCommand => await LoadBoundedContextFromSnapshotAsync<OptionTradeAlgorithmBoundedContext, OptionTradeAlgorithmBoundedContextState, LongIronCondorAlgorithmExecutedEvent>(command),
            ExecuteShortIronCondorAlgorithmCommand  => await LoadBoundedContextFromSnapshotAsync<OptionTradeAlgorithmBoundedContext, OptionTradeAlgorithmBoundedContextState, ShortIronCondorAlgorithmExecutedEvent>(command),
            _ => throw new InvalidOperationException($"LoadAggregateAsync: unknown command '{command.GetType().Name}'")
        };

    /// <summary>
    /// save option trade algorithm bounded context state change events
    /// </summary>
    /// <param name="boundedContextState"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async Task SaveBoundedContextAsync(IBoundedContextState<OptionTradeAlgorithmBoundedContextState> boundedContextState, ICommand command)
        => await SaveBoundedContextAsync<OptionTradeAlgorithmBoundedContextState>(boundedContextState, command);
}
