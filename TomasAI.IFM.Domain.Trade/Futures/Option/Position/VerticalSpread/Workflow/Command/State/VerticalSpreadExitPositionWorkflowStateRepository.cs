using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Domain.Trade.Persistence;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Command.State;
public sealed class VerticalSpreadExitPositionWorkflowStateRepository(IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource, IActorService actorService, IEventProjector<VerticalSpreadExitPositionWorkflowCommandActor> projector, ILogger<VerticalSpreadExitPositionWorkflowStateRepository> logger, IDbContextFactory? dbFactory = null, IPostgresEventTransaction? transactions = null) : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger), IEventSourceActorStateRepository<VerticalSpreadExitPositionWorkflowCommandState>
{
    public ValueTask<VerticalSpreadExitPositionWorkflowCommandState> LoadStateAsync(ICommand command) => LoadStateAsync(command, CancellationToken.None);
    public ValueTask<VerticalSpreadExitPositionWorkflowCommandState> LoadStateAsync(ICommand command, CancellationToken token) => new(LoadStateFromLatestCheckpointAsync<VerticalSpreadExitPositionWorkflowCommandState>(
        command, ExitWorkflowSnapshotFacts.CheckpointTypes, static (state, source, _) => state.ApplyCommittedSnapshot(source), token));
    public ValueTask SaveStateAsync(ICommandActorContext context, VerticalSpreadExitPositionWorkflowCommandState state, ICommand command) =>
        SaveStateAsync(context, state, command, CancellationToken.None);
    public async ValueTask SaveStateAsync(ICommandActorContext context, VerticalSpreadExitPositionWorkflowCommandState state, ICommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.Equals(dbFactory?.TradePlanDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
        {
            var pending = new DomainEventCollection(state.Events);
            await ExitWorkflowAtomicPersistence.CommitAsync(command, pending, state.CommittedStreamVersion, EventSourceDb, dbFactory, transactions, token).ConfigureAwait(false);
            state.AcceptChanges();
            await ExitWorkflowProjectionSubmission.SubmitAsync(projector.DomainEventsProjectionAsync, pending, logger).ConfigureAwait(false);
            return;
        }
        await SaveStateAndDenormalizeEventsAsync(context, state, command, token).ConfigureAwait(false);
    }
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents) => projector.DomainEventsProjectionAsync(domainEvents);
}
