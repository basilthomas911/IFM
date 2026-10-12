using TomasAI.IFM.Domain.Trade.Persistence;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.State;
public sealed class FuturesTradePlanFunctionStateRepository(IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource, IActorService actorService, IEventProjector<FuturesTradePositionCommandActor> projector, ILogger<FuturesTradePlanFunctionStateRepository> logger, IDbContextFactory? dbFactory = null, IPostgresEventTransaction? transactions = null) : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger), IEventSourceFunctionStateRepository<FuturesTradePlanFunctionState, UpdateFuturesTradePlanCommand>
{
    public async ValueTask<FuturesTradePlanFunctionState> LoadStateAsync(UpdateFuturesTradePlanCommand request, CancellationToken cancellationToken = default) => (await LoadStateFromSnapshotAsync<FuturesTradePlanFunctionState, FuturesTradePlanUpdatedEvent>(request, cancellationToken).ConfigureAwait(false)).Prepare(request);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Publication is best effort after the atomic source and snapshot commit; failure must not roll back or retry an accepted plan.")]
    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context, FuturesTradePlanFunctionState state, UpdateFuturesTradePlanCommand request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        if (string.Equals(dbFactory?.TradePlanDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
        {
            var owner = transactions ?? throw new InvalidOperationException("Atomic trade plan persistence requires the PostgreSQL transaction service.");
            var pending = new DomainEventCollection(state.Events);
            try
            {
                await TradePlanAtomicPersistence.CommitAsync(request, pending, state.CommittedStreamVersion,
                    EventSourceDb, dbFactory, owner, cancellationToken).ConfigureAwait(false);
                state.AcceptChanges();
            }
            catch (Exception exception)
            {
                TradePlanPersistenceLog.Dropped(logger, exception, request.CommandId, request.StreamId,
                    state.CompletedEvent?.Id, state.CompletedEvent?.Plan.PlanRevision, state.CompletedEvent?.Plan.ValueDate);
                throw;
            }
            try
            {
                if (state.CompletedEvent?.Plan.MaterialChange == true)
                    await projector.DomainEventsProjectionAsync(pending).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                TradePlanPersistenceLog.PublicationFailed(logger, exception, request.CommandId, request.StreamId, state.CompletedEvent?.Id);
            }
            return;
        }
        var committed = await SaveStateEventsAsync(state, request, state.LastPersistedEventId, cancellationToken).ConfigureAwait(false);
        if (state.CompletedEvent?.Plan.MaterialChange == true)
            await projector.DomainEventsProjectionAsync(committed).ConfigureAwait(false);
    }

    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents) => ValueTask.CompletedTask;
}
