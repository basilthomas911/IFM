using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Command.State;

public sealed class VerticalSpreadExitPositionWorkflowStateRepository(
    IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource,
    IActorService actorService, IEventProjector<VerticalSpreadExitPositionWorkflowCommandActor> projector,
    ILogger<VerticalSpreadExitPositionWorkflowStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
        IEventSourceActorStateRepository<VerticalSpreadExitPositionWorkflowCommandState>
{
    public ValueTask<VerticalSpreadExitPositionWorkflowCommandState> LoadStateAsync(ICommand command) =>
        new(LoadStateAsync<VerticalSpreadExitPositionWorkflowCommandState>(command));

    public async ValueTask SaveStateAsync(ICommandActorContext context,
        VerticalSpreadExitPositionWorkflowCommandState state, ICommand command) =>
        await SaveStateAndDenormalizeEventsAsync(context, state, command).ConfigureAwait(false);

    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context,
        DomainEventCollection events) => projector.DomainEventsProjectionAsync(events);
}
