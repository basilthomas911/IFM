using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Domain.Trade.Persistence;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function.State;
public sealed class RiskManagementFunctionStateRepository(IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource, IActorService actorService, ILogger<RiskManagementFunctionStateRepository> logger,
    IDbContextFactory? databases = null, IPostgresEventTransaction? transactions = null)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      ITransactionalFunctionStateRepository<RiskManagementFunctionState, ExecuteRiskManagementPipelineCommand, RiskManagementFunctionCompletedEvent>, INativeTradeFunctionRepository
{
    public bool UsesAtomicPersistence => string.Equals(databases?.TradeDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal);

    public ValueTask<RiskManagementFunctionCompletedEvent> CommitAsync(IFunctionActorContext context, ExecuteRiskManagementPipelineCommand request,
        RiskManagementFunctionCompletedEvent candidate, CancellationToken cancellationToken = default)
        => TradePipelineAtomicPersistence.CommitAsync(request, candidate, EventSourceDb, databases, transactions, cancellationToken);

    public async ValueTask<RiskManagementFunctionState> LoadStateAsync(ExecuteRiskManagementPipelineCommand request, CancellationToken cancellationToken = default) => await LoadStateFromLatestCheckpointAsync<RiskManagementFunctionState, RiskManagementFunctionCompletedEvent>(
        request, static fact => fact.EventId > 0, cancellationToken).ConfigureAwait(false);
    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context, RiskManagementFunctionState state, ExecuteRiskManagementPipelineCommand request, CancellationToken cancellationToken = default)
    {
        await SaveStateEventsAsync(state, request, expectedStreamVersion: 0, cancellationToken).ConfigureAwait(false);
    }

    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents) => ValueTask.CompletedTask;
}
