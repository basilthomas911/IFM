using TomasAI.IFM.Application.Storage;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Command.State;

public sealed class FuturesPositionStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource,
    IActorService actorService,
    IEventProjector<FuturesTradePositionCommandActor> projector,
    ILogger<FuturesPositionStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
        IResidentEventSourceActorStateRepository<FuturesPositionCommandState>
{
    public ValueTask<FuturesPositionCommandState> LoadStateAsync(ICommand command) =>
        LoadStateAsync(command, CancellationToken.None);

    public async ValueTask<FuturesPositionCommandState> LoadStateAsync(
        ICommand command,
        CancellationToken cancellationToken) =>
        await LoadStateAsync<FuturesPositionCommandState>(command).ConfigureAwait(false);

    public ValueTask SaveStateAsync(
        ICommandActorContext context,
        FuturesPositionCommandState state,
        ICommand command) => SaveStateAsync(context, state, command, CancellationToken.None);

    public async ValueTask SaveStateAsync(
        ICommandActorContext context,
        FuturesPositionCommandState state,
        ICommand command,
        CancellationToken cancellationToken) =>
        await SaveStateAndDenormalizeEventsAsync(context, state, command).ConfigureAwait(false);

    public async ValueTask SaveResidentEventsAsync(
        ICommandActorContext context,
        DomainEventCollection events,
        ICommand command,
        long expectedStreamVersion,
        CancellationToken cancellationToken)
    {
        var committed = await EventSourceDb.SaveCommandEventsAtomicallyAsync(
            command, events, expectedStreamVersion, cancellationToken).ConfigureAwait(false);
        await projector.DomainEventsProjectionAsync(committed).ConfigureAwait(false);
    }

    protected override ValueTask DenormalizeEventsAsync(
        ICommandActorContext context,
        DomainEventCollection events) => projector.DomainEventsProjectionAsync(events);
}
