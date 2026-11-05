using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ProducerOperations;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.FeedSessionPreparation.Function.State;

public sealed class FeedSessionPreparationFunctionStateRepository(
    IEventSourceActorStateFactory factory, IEventSourceActorDbContext database, IActorService actors,
    ILogger<FeedSessionPreparationFunctionStateRepository> logger)
    : BaseEventSourceActorRepository(factory, database, actors, logger),
      IEventSourceFunctionStateRepository<FeedSessionPreparationFunctionState, PrepareMarketDataFeedSessionCommand>
{
    public async ValueTask<FeedSessionPreparationFunctionState> LoadStateAsync(PrepareMarketDataFeedSessionCommand request, CancellationToken cancellationToken = default)
        => await LoadStateFromLatestCheckpointAsync<FeedSessionPreparationFunctionState, FeedSessionPreparationCompletedEvent>(
            request, static result => result.ValueDate != default && result.Contracts is { Length: > 0 }, cancellationToken).ConfigureAwait(false);

    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context, FeedSessionPreparationFunctionState state,
        PrepareMarketDataFeedSessionCommand request, CancellationToken cancellationToken = default)
        => await SaveStateEventsAsync(state, request, expectedStreamVersion: 0, cancellationToken).ConfigureAwait(false);

    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events)
        => ValueTask.CompletedTask;
}
