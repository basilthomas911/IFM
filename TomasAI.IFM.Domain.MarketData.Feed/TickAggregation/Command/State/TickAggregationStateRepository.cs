using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Command.State;

public sealed class TickAggregationStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    ILogger<TickAggregationStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger),
      IEventSourceActorStateRepository<TickAggregationCommandState>
{
    public async ValueTask<TickAggregationCommandState> LoadStateAsync(ICommand command)
    {
        // V1 retains no mutable aggregate data; command audit provides retry
        // deduplication, so replaying an unbounded tick history would be pure cost.
        var state = await LoadEmptyStateAsync<TickAggregationCommandState>().ConfigureAwait(false);
        state.Id = command.Subject.ThreadId;
        return state;
    }

    public async ValueTask SaveStateAsync(
        ICommandActorContext context,
        TickAggregationCommandState state,
        ICommand command) =>
        await SaveStateAndDenormalizeEventsAsync(context, state, command).ConfigureAwait(false);

    protected override async ValueTask DenormalizeEventsAsync(
        ICommandActorContext context,
        DomainEventCollection domainEvents)
    {
        foreach (var domainEvent in domainEvents)
        {
            switch (domainEvent)
            {
                case FuturesTickTradeDataInsertedEvent trade:
                    await PostEventAsync<FuturesTickTradeDataInsertedEvent, TickDataEntityId>(context, trade).ConfigureAwait(false);
                    break;
                case FuturesTickQuoteDataInsertedEvent quote:
                    await PostEventAsync<FuturesTickQuoteDataInsertedEvent, TickDataEntityId>(context, quote).ConfigureAwait(false);
                    break;
            }
        }
    }
}
