using TomasAI.IFM.Shared.Diagnostics;
using TomasAI.IFM.Application.EventSourcing.Repository;
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
/// <param name = "factory">Creates the command-owned position state.</param><param name = "database">The source event store.</param>
/// <param name = "actors">The actor transport.</param><param name = "projector">Independent history and financial projection.</param>
/// <param name = "logger">Structured persistence and publication diagnostics.</param>
public sealed class IronCondorPositionStateRepository(IEventSourceActorStateFactory factory, IEventSourceActorDbContext database, IActorService actors, IEventProjector<FuturesIronCondorTradePositionCommandActor> projector, ILogger<IronCondorPositionStateRepository> logger, IPostgresEventTransaction? transactions = null) : BaseEventSourceActorRepository(factory, database, actors, logger), IResidentEventSourceActorStateRepository<IronCondorPositionCommandState>
{
    /// <summary>Loads the committed position state and stream version.</summary>
    /// <param name = "command">The position command whose source stream is loaded.</param><returns>The restored position state.</returns>
    public ValueTask<IronCondorPositionCommandState> LoadStateAsync(ICommand command) => LoadStateAsync(command, CancellationToken.None);
    /// <summary>Loads position source state using the supplied cancellation deadline.</summary>
    /// <param name = "command">The position command.</param><param name = "token">The load deadline.</param><returns>The restored state and stream version.</returns>
    public async ValueTask<IronCondorPositionCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken)
    {
        using var step = new OptionFeedTrace("option.position.load_state", logger, command.CommandId.ToString());
        try
        {
            var state = await LoadStateFromSnapshotAsync<IronCondorPositionCommandState, IronCondorPositionChangedEvent>(command, cancellationToken).ConfigureAwait(false);
            // Position changes replace the complete position, but monitoring limits live in
            // a separate event which can precede the latest position snapshot.
            if (state.TradeLimits is null)
            {
                var streamId = await GetStreamId(command.StreamId, cancellationToken).ConfigureAwait(false);
                await EventSourceDb.MapReduceActorEventStreamAsync<IronCondorPositionCommandState, IronCondorMonitoringInitializedEvent>(
                    streamId, 1, state.ReplayEvents, cancellationToken).ConfigureAwait(false);
            }
            step.Complete();
            return state;
        }
        catch (Exception error) { step.Fail(error); throw; }
    }
    /// <summary>Commits a financial position transition before admitting its projections.</summary>
    /// <param name = "context">The command publication capability.</param><param name = "state">The command-owned position state.</param>
    /// <param name = "command">The originating financial transition.</param><returns>The source commit and projection admission.</returns>
    public ValueTask SaveStateAsync(ICommandActorContext context, IronCondorPositionCommandState state, ICommand command) => SaveStateAsync(context, state, command, CancellationToken.None);
    /// <summary>Commits a financial position transition through the existing source repository.</summary>
    /// <param name = "context">The command publication capability.</param><param name = "state">The command-owned position state.</param>
    /// <param name = "command">The originating financial transition.</param><param name = "token">The command lifetime.</param>
    /// <returns>The source commit and financial projection admission.</returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, IronCondorPositionCommandState state, ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (context is IIronCondorPositionCommandContext typed && string.Equals(typed.DbFactory.TradeDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
        {
            var pending = new DomainEventCollection(state.Events);
            await TomasAI.IFM.Domain.Trade.Persistence.TradeAtomicPersistence.CommitAsync(command, pending, state.CommittedStreamVersion, typed.DbEventSource, typed.DbFactory.TradeDb, transactions, cancellationToken).ConfigureAwait(false);
            state.AcceptChanges();
            await projector.DomainEventsProjectionAsync(pending).ConfigureAwait(false);
        }
        else
            await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Commits each current mark before publishing it; history projection cannot hold its live notification.</summary>
    /// <param name = "context">The owning command actor publication capability.</param>
    /// <param name = "events">The detached position changes.</param>
    /// <param name = "command">The originating market-mark command.</param>
    /// <param name = "version">The committed source stream version, separate from global event IDs.</param>
    /// <param name = "token">The resident worker lifetime.</param>
    /// <returns>The source commit and independent history admission.</returns>
    public async ValueTask SaveResidentEventsAsync(ICommandActorContext context, DomainEventCollection events, ICommand command, long expectedStreamVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        using var operation = new OptionFeedTrace("option.position.persist_and_publish", logger, command.CommandId.ToString(), realtime: true);
        using var commit = new OptionFeedTrace("option.position.commit", logger, command.CommandId.ToString(), realtime: true);
        DomainEventCollection committed;
        if (context is IIronCondorPositionCommandContext typed && string.Equals(typed.DbFactory.TradeDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
        {
            await TomasAI.IFM.Domain.Trade.Persistence.TradeAtomicPersistence.CommitAsync(command, events, expectedStreamVersion, typed.DbEventSource, typed.DbFactory.TradeDb, transactions, cancellationToken).ConfigureAwait(false);
            committed = events;
        }
        else
            committed = await EventSourceDb.SaveCommandEventsAtomicallyAsync(command, events, expectedStreamVersion, cancellationToken).ConfigureAwait(false);
        commit.Complete();
        commit.Dispose();
        using (var publication = new OptionFeedTrace("option.position.publish_current_marks", logger, command.CommandId.ToString(), realtime: true))
        {
            await PublishCurrentMarksAsync(context, committed, cancellationToken).ConfigureAwait(false);
            publication.Complete();
        }
        using (var projection = new OptionFeedTrace("option.position.admit_projection", logger, command.CommandId.ToString(), realtime: true))
        {
            await projector.DomainEventsProjectionAsync(committed).ConfigureAwait(false);
            projection.Complete();
        }
        operation.Complete();
    }

    /// <summary>Publishes committed market marks once to the UI event and realtime plan routes, with logged disposable failures.</summary>
    /// <param name = "context">The current publication owner.</param><param name = "events">Already committed source payloads.</param>
    /// <param name = "token">The worker lifetime; no history read or projection wait is required.</param>
    /// <returns>The two current-event enqueue attempts.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Each current-mark destination is independent; log a failed delivery and continue to the remaining monitoring listeners.")]
    async ValueTask PublishCurrentMarksAsync(ICommandActorContext context, DomainEventCollection events, CancellationToken token)
    {
        foreach (var changed in events.OfType<IronCondorPositionChangedEvent>().Where(value => value.PositionSnapshot.Phase == StrategyPositionPhase.MarkToMarket))
        {
            foreach (var destination in new[]
            {
                new ActorMailboxId(ActorType.Event, "FuturesIronCondorTradePositionEvent"),
                new ActorMailboxId(ActorType.Realtime, IronCondorTradePositionRealtimeActor.ActorName)
            }

            )
            {
                using var delivery = new OptionFeedTrace("option.position.publish." + destination.Name, logger, changed.CommandId.ToString(), realtime: true);
                try
                {
                    await context.SendAsync<IronCondorPositionChangedEvent, StrategyPositionId>(changed with { Subject = new(destination.ActorType, destination.Name, IronCondorPositionChangedEvent.Verb, changed.EntityId.Format()) }, token).ConfigureAwait(false);
                    delivery.Complete();
                }
                catch (Exception error)
                {
                    delivery.Fail(error);
                    if (logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Error))
                        global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write39(logger, error, nameof(PublishCurrentMarksAsync), changed.EntityId.Format(), changed.PositionSnapshot.PositionSequence, changed.EventId, changed.CommandId, destination);
                }
            }
        }
    }

    /// <summary>Admits independently persisted history and financial-boundary projections.</summary>
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents) => projector.DomainEventsProjectionAsync(domainEvents);
}
