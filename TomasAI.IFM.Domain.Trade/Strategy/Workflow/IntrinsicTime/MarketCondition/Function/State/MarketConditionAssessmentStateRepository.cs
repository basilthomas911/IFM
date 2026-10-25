using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.State;
public sealed class MarketConditionAssessmentStateRepository(IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource, IActorService actorService, ILogger<MarketConditionAssessmentStateRepository> logger, IDbContextFactory? databases = null, IPostgresEventTransaction? transactions = null) : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger), ITransactionalFunctionStateRepository<MarketConditionAssessmentState, ExecuteMarketConditionAssessmentCommand, MarketConditionAssessmentCompletedEvent>, INativeTradeFunctionRepository
{
    public bool UsesAtomicPersistence => string.Equals(databases?.TradeDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal);

    public ValueTask<MarketConditionAssessmentCompletedEvent> CommitAsync(IFunctionActorContext context, ExecuteMarketConditionAssessmentCommand request, MarketConditionAssessmentCompletedEvent candidate, CancellationToken cancellationToken = default) => TradePipelineAtomicPersistence.CommitAsync(request, candidate, EventSourceDb, databases, transactions, cancellationToken);
    public async ValueTask<MarketConditionAssessmentState> LoadStateAsync(ExecuteMarketConditionAssessmentCommand request, CancellationToken cancellationToken = default) => await LoadStateAsync<MarketConditionAssessmentState>(request, cancellationToken).ConfigureAwait(false);
    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context, MarketConditionAssessmentState state, ExecuteMarketConditionAssessmentCommand request, CancellationToken cancellationToken = default)
    {
        using var activity = MarketConditionTelemetry.Start("market-condition.completed-state-append");
        await SaveStateEventsAsync(state, request, expectedStreamVersion: 0, cancellationToken).ConfigureAwait(false);
    }

    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents) => ValueTask.CompletedTask;
}
