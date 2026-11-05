using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ProducerOperations;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.StreamingRequestIdAllocation.Function.State;

public sealed class StreamingRequestIdAllocationFunctionStateRepository(
    IEventSourceActorStateFactory factory, IEventSourceActorDbContext database, IActorService actors,
    ILogger<StreamingRequestIdAllocationFunctionStateRepository> logger)
    : BaseEventSourceActorRepository(factory, database, actors, logger),
      IEventSourceFunctionStateRepository<StreamingRequestIdAllocationFunctionState, AllocateStreamingRequestIdCommand>
{
    public async ValueTask<StreamingRequestIdAllocationFunctionState> LoadStateAsync(AllocateStreamingRequestIdCommand request, CancellationToken cancellationToken = default)
        => await LoadStateFromLatestCheckpointAsync<StreamingRequestIdAllocationFunctionState, StreamingRequestIdAllocationCompletedEvent>(
            request, static result => result.Value > 0, cancellationToken).ConfigureAwait(false);

    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context, StreamingRequestIdAllocationFunctionState state,
        AllocateStreamingRequestIdCommand request, CancellationToken cancellationToken = default)
        => await SaveStateEventsAsync(state, request, expectedStreamVersion: 0, cancellationToken).ConfigureAwait(false);

    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events)
        => ValueTask.CompletedTask;
}
