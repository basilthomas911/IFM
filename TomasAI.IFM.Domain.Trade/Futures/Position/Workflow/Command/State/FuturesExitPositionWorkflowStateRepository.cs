using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.State;

public sealed class FuturesExitPositionWorkflowStateRepository(
    IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource,
    IActorService actorService, IEventProjector<FuturesExitPositionWorkflowCommandActor> projector,
    ILogger<FuturesExitPositionWorkflowStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
        IEventSourceActorStateRepository<FuturesExitPositionWorkflowCommandState>
{
    public ValueTask<FuturesExitPositionWorkflowCommandState> LoadStateAsync(ICommand command) =>
        new(LoadStateAsync<FuturesExitPositionWorkflowCommandState>(command));

    public async ValueTask SaveStateAsync(ICommandActorContext context,
        FuturesExitPositionWorkflowCommandState state, ICommand command) =>
        await SaveStateAndDenormalizeEventsAsync(context, state, command).ConfigureAwait(false);

    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context,
        DomainEventCollection events) => projector.DomainEventsProjectionAsync(events);
}
