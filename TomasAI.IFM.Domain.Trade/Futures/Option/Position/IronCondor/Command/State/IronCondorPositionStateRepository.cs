using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;

/// <summary>Commits position source events and publishes current marks independently of history projection.</summary>
/// <param name="factory">Creates the command-owned position state.</param><param name="database">The source event store.</param>
/// <param name="actors">The actor transport.</param><param name="projector">Independent history and financial projection.</param>
/// <param name="logger">Structured persistence and publication diagnostics.</param>
public sealed class IronCondorPositionStateRepository(
    IEventSourceActorStateFactory factory, IEventSourceActorDbContext database, IActorService actors,
    IEventProjector<FuturesIronCondorTradePositionCommandActor> projector,
    ILogger<IronCondorPositionStateRepository> logger)
    : BaseEventSourceActorRepository(factory, database, actors, logger),
      IResidentEventSourceActorStateRepository<IronCondorPositionCommandState>
{
    /// <summary>Loads the committed position state and stream version.</summary>
    /// <param name="command">The position command whose source stream is loaded.</param><returns>The restored position state.</returns>
    public ValueTask<IronCondorPositionCommandState> LoadStateAsync(ICommand command)
        => LoadStateAsync(command, CancellationToken.None);

    /// <summary>Loads position source state using the supplied cancellation deadline.</summary>
    /// <param name="command">The position command.</param><param name="token">The load deadline.</param><returns>The restored state and stream version.</returns>
    public async ValueTask<IronCondorPositionCommandState> LoadStateAsync(ICommand command, CancellationToken token)
        => await LoadStateAsync<IronCondorPositionCommandState>(command, token).ConfigureAwait(false);

    /// <summary>Commits a financial position transition before admitting its projections.</summary>
    /// <param name="context">The command publication capability.</param><param name="state">The command-owned position state.</param>
    /// <param name="command">The originating financial transition.</param><returns>The source commit and projection admission.</returns>
    public ValueTask SaveStateAsync(ICommandActorContext context, IronCondorPositionCommandState state, ICommand command)
        => SaveStateAsync(context, state, command, CancellationToken.None);

    /// <summary>Commits a financial position transition through the existing source repository.</summary>
    /// <param name="context">The command publication capability.</param><param name="state">The command-owned position state.</param>
    /// <param name="command">The originating financial transition.</param><param name="token">The command lifetime.</param>
    /// <returns>The source commit and financial projection admission.</returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, IronCondorPositionCommandState state,
        ICommand command, CancellationToken token)
        => await SaveStateAndDenormalizeEventsAsync(context, state, command, token).ConfigureAwait(false);

    /// <summary>Commits each current mark before publishing it; history projection cannot hold its live notification.</summary>
    /// <param name="context">The owning command actor publication capability.</param>
    /// <param name="events">The detached position changes.</param>
    /// <param name="command">The originating market-mark command.</param>
    /// <param name="version">The committed source stream version, separate from global event IDs.</param>
    /// <param name="token">The resident worker lifetime.</param>
    /// <returns>The source commit and independent history admission.</returns>
    public async ValueTask SaveResidentEventsAsync(ICommandActorContext context, DomainEventCollection events,
        ICommand command, long version, CancellationToken token)
    {
        var committed = await EventSourceDb.SaveCommandEventsAtomicallyAsync(command, events, version, token).ConfigureAwait(false);
        await PublishCurrentMarksAsync(context, committed, token).ConfigureAwait(false);
        await projector.DomainEventsProjectionAsync(committed).ConfigureAwait(false);
    }

    /// <summary>Publishes committed market marks once to the UI event and realtime plan routes, with logged disposable failures.</summary>
    /// <param name="context">The current publication owner.</param><param name="events">Already committed source payloads.</param>
    /// <param name="token">The worker lifetime; no history read or projection wait is required.</param>
    /// <returns>The two current-event enqueue attempts.</returns>
    async ValueTask PublishCurrentMarksAsync(ICommandActorContext context, DomainEventCollection events, CancellationToken token)
    {
        foreach (var changed in events.OfType<IronCondorPositionChangedEvent>()
            .Where(value => value.PositionSnapshot.Phase == StrategyPositionPhase.MarkToMarket))
        {
            foreach (var destination in new[]
            {
                new ActorMailboxId(ActorType.Event, "FuturesIronCondorTradePositionEvent"),
                new ActorMailboxId(ActorType.Realtime, IronCondorTradePositionRealtimeActor.ActorName)
            })
            {
                try
                {
                    await context.SendAsync<IronCondorPositionChangedEvent, StrategyPositionId>(changed with
                    { Subject = new(destination.ActorType, destination.Name, IronCondorPositionChangedEvent.Verb, changed.EntityId.Format()) }, token)
                        .ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    logger.LogError(error,
                        "Current position notification dropped; MethodName={MethodName} PositionId={PositionId} PositionSequence={PositionSequence} EventId={EventId} CommandId={CommandId} Destination={Destination}",
                        nameof(PublishCurrentMarksAsync), changed.EntityId.Format(), changed.PositionSnapshot.PositionSequence, changed.EventId, changed.CommandId, destination);
                }
            }
        }
    }

    /// <summary>Admits independently persisted history and financial-boundary projections.</summary>
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events)
        => projector.DomainEventsProjectionAsync(events);
}
