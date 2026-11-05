using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ProducerOperations;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Preparation.Function.State;

public sealed class OptionContractPreparationFunctionStateRepository(
    IEventSourceActorStateFactory factory, IEventSourceActorDbContext database, IActorService actors,
    ILogger<OptionContractPreparationFunctionStateRepository> logger)
    : BaseEventSourceActorRepository(factory, database, actors, logger),
      IEventSourceFunctionStateRepository<OptionContractPreparationFunctionState, PrepareOptionContractsCommand>
{
    public async ValueTask<OptionContractPreparationFunctionState> LoadStateAsync(PrepareOptionContractsCommand request, CancellationToken cancellationToken = default)
        => await LoadStateFromLatestCheckpointAsync<OptionContractPreparationFunctionState, OptionContractPreparationCompletedEvent>(
            request, static result => result.RequestFingerprint.Length > 0 && result.Contracts is { Length: > 0 }, cancellationToken).ConfigureAwait(false);

    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context, OptionContractPreparationFunctionState state,
        PrepareOptionContractsCommand request, CancellationToken cancellationToken = default)
        => await SaveStateEventsAsync(state, request, expectedStreamVersion: 0, cancellationToken).ConfigureAwait(false);

    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events)
        => ValueTask.CompletedTask;
}
