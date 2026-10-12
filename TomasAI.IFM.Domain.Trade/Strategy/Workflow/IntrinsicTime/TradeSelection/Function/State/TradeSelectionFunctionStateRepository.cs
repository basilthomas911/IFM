using TomasAI.IFM.Domain.EventSourcing.Repository;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.State;
public sealed class TradeSelectionFunctionStateRepository(IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource, IActorService actorService, ILogger<TradeSelectionFunctionStateRepository> logger, IDbContextFactory? databases = null, IPostgresEventTransaction? transactions = null) : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger), ITransactionalFunctionStateRepository<TradeSelectionFunctionState, ExecuteTradeSelectionPipelineCommand, TradeSelectionFunctionCompletedEvent>, INativeTradeFunctionRepository
{
    public bool UsesAtomicPersistence => string.Equals(databases?.TradeDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal);

    public ValueTask<TradeSelectionFunctionCompletedEvent> CommitAsync(IFunctionActorContext context, ExecuteTradeSelectionPipelineCommand request, TradeSelectionFunctionCompletedEvent candidate, CancellationToken cancellationToken = default) => TradePipelineAtomicPersistence.CommitAsync(request, candidate, EventSourceDb, databases, transactions, cancellationToken);
    public async ValueTask<TradeSelectionFunctionState> LoadStateAsync(ExecuteTradeSelectionPipelineCommand request, CancellationToken cancellationToken = default) => await LoadStateAsync<TradeSelectionFunctionState>(request, cancellationToken).ConfigureAwait(false);
    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context, TradeSelectionFunctionState state, ExecuteTradeSelectionPipelineCommand request, CancellationToken cancellationToken = default)
    {
        await SaveStateEventsAsync(state, request, expectedStreamVersion: 0, cancellationToken).ConfigureAwait(false);
    }

    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents) => ValueTask.CompletedTask;
}
