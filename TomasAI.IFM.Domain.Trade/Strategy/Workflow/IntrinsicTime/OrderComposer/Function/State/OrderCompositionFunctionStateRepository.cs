using TomasAI.IFM.Domain.EventSourcing.Repository;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.State;
public sealed class OrderCompositionFunctionStateRepository(IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource, IActorService actorService, ILogger<OrderCompositionFunctionStateRepository> logger, IDbContextFactory? databases = null, IPostgresEventTransaction? transactions = null) : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger), ITransactionalFunctionStateRepository<OrderCompositionFunctionState, ExecuteOrderCompositionPipelineCommand, OrderCompositionFunctionCompletedEvent>, INativeTradeFunctionRepository
{
    public bool UsesAtomicPersistence => string.Equals(databases?.TradeDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal);

    public ValueTask<OrderCompositionFunctionCompletedEvent> CommitAsync(IFunctionActorContext context, ExecuteOrderCompositionPipelineCommand request, OrderCompositionFunctionCompletedEvent candidate, CancellationToken cancellationToken = default) => TradePipelineAtomicPersistence.CommitAsync(request, candidate, EventSourceDb, databases, transactions, cancellationToken);
    public async ValueTask<OrderCompositionFunctionState> LoadStateAsync(ExecuteOrderCompositionPipelineCommand request, CancellationToken cancellationToken = default) => await LoadStateAsync<OrderCompositionFunctionState>(request, cancellationToken).ConfigureAwait(false);
    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context, OrderCompositionFunctionState state, ExecuteOrderCompositionPipelineCommand request, CancellationToken cancellationToken = default)
    {
        await SaveStateEventsAsync(state, request, expectedStreamVersion: 0, cancellationToken).ConfigureAwait(false);
    }

    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents) => ValueTask.CompletedTask;
}
