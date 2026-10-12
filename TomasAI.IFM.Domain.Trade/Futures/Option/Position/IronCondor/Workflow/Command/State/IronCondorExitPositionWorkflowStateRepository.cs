using TomasAI.IFM.Domain.Trade.Persistence;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Command.State;
public sealed class IronCondorExitPositionWorkflowStateRepository(IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource, IActorService actorService, IEventProjector<IronCondorExitPositionWorkflowCommandActor> projector, ILogger<IronCondorExitPositionWorkflowStateRepository> logger, IDbContextFactory? dbFactory = null, IPostgresEventTransaction? transactions = null) : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger), IEventSourceActorStateRepository<IronCondorExitPositionWorkflowCommandState>
{
    public ValueTask<IronCondorExitPositionWorkflowCommandState> LoadStateAsync(ICommand command) => new(LoadStateAsync<IronCondorExitPositionWorkflowCommandState>(command));
    public async ValueTask SaveStateAsync(ICommandActorContext context, IronCondorExitPositionWorkflowCommandState state, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.Equals(dbFactory?.TradePlanDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
        {
            var pending = new DomainEventCollection(state.Events);
            await ExitWorkflowAtomicPersistence.CommitAsync(command, pending, state.CommittedStreamVersion, EventSourceDb, dbFactory, transactions).ConfigureAwait(false);
            state.AcceptChanges();
            await projector.DomainEventsProjectionAsync(pending).ConfigureAwait(false);
            return;
        }
        await SaveStateAndDenormalizeEventsAsync(context, state, command).ConfigureAwait(false);
    }
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents) => projector.DomainEventsProjectionAsync(domainEvents);
}
