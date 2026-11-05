using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Identity.PortfolioBusinessIdAllocation.Function.State;

public sealed class PortfolioBusinessIdAllocationFunctionStateRepository(
    IEventSourceActorStateFactory factory, IEventSourceActorDbContext database, IActorService actors,
    ILogger<PortfolioBusinessIdAllocationFunctionStateRepository> logger)
    : BaseEventSourceActorRepository(factory, database, actors, logger),
      IEventSourceFunctionStateRepository<PortfolioBusinessIdAllocationFunctionState, AllocatePortfolioBusinessIdCommand>
{
    public async ValueTask<PortfolioBusinessIdAllocationFunctionState> LoadStateAsync(AllocatePortfolioBusinessIdCommand request, CancellationToken cancellationToken = default)
        => await LoadStateFromLatestCheckpointAsync<PortfolioBusinessIdAllocationFunctionState, PortfolioBusinessIdAllocationCompletedEvent>(
            request, static result => result.Value is not null && !string.IsNullOrWhiteSpace(result.InputHash), cancellationToken).ConfigureAwait(false);

    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context, PortfolioBusinessIdAllocationFunctionState state,
        AllocatePortfolioBusinessIdCommand request, CancellationToken cancellationToken = default)
        => await SaveStateEventsAsync(state, request, expectedStreamVersion: 0, cancellationToken).ConfigureAwait(false);

    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events)
        => ValueTask.CompletedTask;
}
