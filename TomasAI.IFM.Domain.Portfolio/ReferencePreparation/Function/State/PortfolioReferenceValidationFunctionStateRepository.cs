using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.ReferencePreparation.Function.State;

public sealed class PortfolioReferenceValidationFunctionStateRepository(
    IEventSourceActorStateFactory factory, IEventSourceActorDbContext database, IActorService actors,
    ILogger<PortfolioReferenceValidationFunctionStateRepository> logger)
    : BaseEventSourceActorRepository(factory, database, actors, logger),
      IEventSourceFunctionStateRepository<PortfolioReferenceValidationFunctionState, ValidatePortfolioReferenceCommand>
{
    public async ValueTask<PortfolioReferenceValidationFunctionState> LoadStateAsync(ValidatePortfolioReferenceCommand request, CancellationToken cancellationToken = default)
        => await LoadStateFromLatestCheckpointAsync<PortfolioReferenceValidationFunctionState, PortfolioReferenceValidationCompletedEvent>(
            request, static result => !string.IsNullOrWhiteSpace(result.InputHash), cancellationToken).ConfigureAwait(false);

    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context, PortfolioReferenceValidationFunctionState state,
        ValidatePortfolioReferenceCommand request, CancellationToken cancellationToken = default)
        => await SaveStateEventsAsync(state, request, expectedStreamVersion: 0, cancellationToken).ConfigureAwait(false);

    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events)
        => ValueTask.CompletedTask;
}
