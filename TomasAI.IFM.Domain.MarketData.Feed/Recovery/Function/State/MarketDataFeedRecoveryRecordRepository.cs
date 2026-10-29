using TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventSourcing.Repository;
namespace TomasAI.IFM.Domain.MarketData.Feed.Recovery.Function.State;
/// <summary>Per-record durable acknowledgement, repairable after an incident commit without a reply.</summary>
public sealed class MarketDataFeedRecoveryRecordRepository(IEventSourceActorStateFactory factory, IEventSourceActorDbContext events,
    IActorService actors, ILogger<MarketDataFeedRecoveryRecordRepository> logger)
    : BaseEventSourceActorRepository(factory, events, actors, logger),
      IEventSourceFunctionStateRepository<MarketDataFeedRecoveryRecordState, RecordMarketDataFeedRecoveryCommand>
{
    public async ValueTask<MarketDataFeedRecoveryRecordState> LoadStateAsync(RecordMarketDataFeedRecoveryCommand request, CancellationToken token = default)
    {
        var state = new MarketDataFeedRecoveryRecordState { Id = request.Subject.ThreadId };
        var stream = await GetStreamId(request.StreamId, token).ConfigureAwait(false);
        await EventSourceDb.MapReduceActorEventStreamAsync<MarketDataFeedRecoveryRecordState, MarketDataFeedRecoveryRecordStoredEvent>(stream, 1, state.ReplayEvents, token).ConfigureAwait(false);
        return state;
    }
    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context, MarketDataFeedRecoveryRecordState state, RecordMarketDataFeedRecoveryCommand request, CancellationToken token = default)
    {
        await SaveStateEventsAsync(state, request, state.CommittedStreamVersion, token).ConfigureAwait(false);
        state.AcceptChanges();
    }
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events) => ValueTask.CompletedTask;
}
