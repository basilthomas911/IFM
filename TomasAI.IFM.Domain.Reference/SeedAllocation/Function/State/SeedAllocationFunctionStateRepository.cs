using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Reference.Shared.SeedAllocation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.SeedAllocation.Function.State;

public sealed class SeedAllocationFunctionStateRepository(
    IEventSourceActorStateFactory factory, IEventSourceActorDbContext database, IActorService actors,
    ILogger<SeedAllocationFunctionStateRepository> logger)
    : BaseEventSourceActorRepository(factory, database, actors, logger),
      IEventSourceFunctionStateRepository<SeedAllocationFunctionState, AllocateSeedIdCommand>
{
    public async ValueTask<SeedAllocationFunctionState> LoadStateAsync(AllocateSeedIdCommand request, CancellationToken cancellationToken = default)
        => await LoadStateFromLatestCheckpointAsync<SeedAllocationFunctionState, SeedAllocationCompletedEvent>(
            request, static result => result.Value > 0, cancellationToken).ConfigureAwait(false);

    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context, SeedAllocationFunctionState state,
        AllocateSeedIdCommand request, CancellationToken cancellationToken = default)
        => await SaveStateEventsAsync(state, request, expectedStreamVersion: 0, cancellationToken).ConfigureAwait(false);

    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events)
        => ValueTask.CompletedTask;
}
