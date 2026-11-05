using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.FinancialBookPreparation.Function.State;

public sealed class FinancialBookPreparationFunctionStateRepository(
    IEventSourceActorStateFactory factory, IEventSourceActorDbContext database, IActorService actors,
    ILogger<FinancialBookPreparationFunctionStateRepository> logger)
    : BaseEventSourceActorRepository(factory, database, actors, logger),
      IEventSourceFunctionStateRepository<FinancialBookPreparationFunctionState, PrepareFinancialBookCommand>
{
    public async ValueTask<FinancialBookPreparationFunctionState> LoadStateAsync(PrepareFinancialBookCommand request, CancellationToken cancellationToken = default)
        => await LoadStateFromLatestCheckpointAsync<FinancialBookPreparationFunctionState, FinancialBookPreparationCompletedEvent>(
            request, static result => result.Value is not null && !string.IsNullOrWhiteSpace(result.InputHash), cancellationToken).ConfigureAwait(false);

    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context, FinancialBookPreparationFunctionState state,
        PrepareFinancialBookCommand request, CancellationToken cancellationToken = default)
        => await SaveStateEventsAsync(state, request, expectedStreamVersion: 0, cancellationToken).ConfigureAwait(false);

    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events)
        => ValueTask.CompletedTask;
}
