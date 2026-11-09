using TomasAI.IFM.Shared.Diagnostics;
using TomasAI.IFM.Application.EventSourcing.Repository;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
/// <summary>Commits position source events and publishes current marks independently of history projection.</summary>
/// <param name = "factory">Creates the command-owned position state.</param><param name = "database">The source event store.</param>
/// <param name = "actors">The actor transport.</param><param name = "projector">Independent history and financial projection.</param>
/// <param name = "logger">Structured persistence and publication diagnostics.</param>
public sealed class IronCondorPositionStateRepository(IEventSourceActorStateFactory factory, IEventSourceActorDbContext database, IActorService actors, IEventProjector<FuturesIronCondorTradePositionCommandActor> projector, ILogger<IronCondorPositionStateRepository> logger, IPostgresEventTransaction? transactions = null, IDbContextFactory? databases = null) : BaseEventSourceActorRepository(factory, database, actors, logger), IResidentEventSourceActorStateRepository<IronCondorPositionCommandState>
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
            // These are the only two retained state components. Each source fact
            // replaces its complete component; neither is a delta or a read model.
            var state = await LoadStateFromLatestSnapshotsAsync<IronCondorPositionCommandState,
                IronCondorPositionChangedEvent, IronCondorMonitoringInitializedEvent>(
                command, cancellationToken).ConfigureAwait(false);
            // Source ordering preserves limits whether initialization precedes or
            // follows the latest position snapshot.
            step.Complete();
            return state;
        }
        catch (Exception error) { step.Fail(error); throw; }
    }

    /// <summary>Explicitly records missing legacy currency evidence using the owning Trade stream.</summary>
    public async Task<IronCondorCurrencyEvidenceReconciliation> ReconcileLegacyCurrencyEvidenceAsync(
        ReconcileIronCondorPositionCurrencyEvidenceCommand command, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.CommandId == Guid.Empty || !command.EntityId.IsValid || command.ExpectedStreamVersion <= 0 ||
            command.Subject != new ActorSubject(ActorType.Command, FuturesIronCondorTradePositionCommandActor.ActorName,
                ReconcileIronCondorPositionCurrencyEvidenceCommand.Verb, command.EntityId.Format()))
            throw new ArgumentException("Legacy position reconciliation requires its exact command route and expected version.", nameof(command));
        if (transactions is null || databases?.TradeDb is not { ProviderName: "System.Data.Postgres" } tradeDb
            || !string.Equals(EventSourceDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal)
            || PostgresDatabaseIdentity.Parse(EventSourceDb.ConnectionString) != PostgresDatabaseIdentity.Parse(tradeDb.ConnectionString)
            || tradeDb.Database is not IEnlistedTradeDbWriter writer)
            throw new InvalidOperationException("Legacy position reconciliation requires enlisted PostgreSQL storage.");

        var state = await LoadStateAsync(command, token).ConfigureAwait(false);
        if (state.Current is not { } position || position.Id != command.EntityId)
            return IronCondorCurrencyEvidenceReconciliation.Unavailable;
        if (HasCurrencyEvidence(position)) return IronCondorCurrencyEvidenceReconciliation.AlreadyPresent;
        if (state.CommittedStreamVersion != command.ExpectedStreamVersion)
            throw new ConcurrencyException("The position stream changed before currency evidence reconciliation.");

        try
        {
            return await transactions.ExecuteOnceAsync(async (transaction, ct) =>
            {
                var tradeStream = new ActorSubject(ActorType.Command, FuturesOptionTradeActorNames.Command,
                    string.Empty, position.Id.Trade.Format()).StreamId;
                var evidenceRows = await transaction.QueryAsync("""
                    SELECT e.EventVersion,n.EventTypeName,e.StreamVersion,e.EventPayload
                    FROM event_stream_id s
                    JOIN event_log e ON e.EventStreamId=s.EventStreamId AND e.StreamVersion=s.CurrentVersion
                    JOIN event_name_id n ON n.EventNameId=e.EventNameId
                    WHERE s.EventStream=$1 FOR SHARE OF s;
                    """, [tradeStream], row => new EventStreamReadModel
                    {
                        EventVersion = row.GetInt64(0), EventTypeName = row.GetString(1), StreamVersion = row.GetInt64(2),
                        EventData = row.GetFieldValue<byte[]>(3)
                    }, ct).ConfigureAwait(false);
                if (evidenceRows.SingleOrDefault()?.ToDomainEvent() is not OptionTradeChangedEvent evidence ||
                    evidence.EventId <= 0 || evidence.EntityId != position.Id.Trade ||
                    !Qualifies(position, evidence.EstablishedTradeDefinition))
                    return IronCondorCurrencyEvidenceReconciliation.Unavailable;

                var repaired = StrategyPositionCurrencyPnl.Apply(position, evidence.EstablishedTradeDefinition);
                if (!HasCurrencyEvidence(repaired) || repaired.DailyPnl is null)
                    return IronCondorCurrencyEvidenceReconciliation.Unavailable;
                var fact = new IronCondorPositionChangedEvent
                {
                    Id = command.CommandId, CommandId = command.CommandId, EntityId = command.EntityId,
                    Subject = new(ActorType.Event, "FuturesIronCondorTradePositionEvent", IronCondorPositionChangedEvent.Verb,
                        command.EntityId.Format()),
                    EventSource = command.EventSource,
                    CurrencyEvidenceEventId = evidence.EventId,
                    PositionSnapshot = repaired with
                    {
                        PositionSequence = checked(position.PositionSequence + 1),
                        HistoryPositionSequence = position.HistoryPositionSequence == 0
                            ? position.PositionSequence : position.HistoryPositionSequence
                    }
                };
                await transaction.ReserveCommandAsync(command, ct).ConfigureAwait(false);
                await transaction.AppendAsync(command.StreamId, command.CommandId, fact, command.ExpectedStreamVersion, ct)
                    .ConfigureAwait(false);
                await writer.ProjectPositionAsync(transaction, fact.PositionSnapshot, ct).ConfigureAwait(false);
                return IronCondorCurrencyEvidenceReconciliation.Reconciled;
            }, token).ConfigureAwait(false);
        }
        catch (CommandAuditDuplicateException exception) when (exception.CommandId == command.CommandId && exception.HasCommittedEvents)
        {
            return IronCondorCurrencyEvidenceReconciliation.AlreadyPresent;
        }
    }

    static bool HasCurrencyEvidence(StrategyPositionSnapshot position) => position.OpeningCommission is >= 0
        && position.OpeningValueDate.HasValue && position.ContractCashMultiplier is > 0;

    static bool Qualifies(StrategyPositionSnapshot position, EstablishedTradeDefinition trade)
    {
        if (trade.Id != position.Id.Trade || trade.AssetFamily != TradeAssetFamily.FuturesOption ||
            trade.StrategyKind != TradeStrategyKind.IronCondor || position.StrategyKind != TradeStrategyKind.IronCondor ||
            trade.OpeningCommission < 0 || trade.EstablishedAtUtc.Kind != DateTimeKind.Utc || trade.TradeDate is null ||
            position.OpeningCommission is { } commission && commission != trade.OpeningCommission ||
            position.OpeningValueDate is { } date && date != trade.TradeDate ||
            position.Legs.Length != 4 || trade.Legs.Length != 4 ||
            trade.Legs.Select(leg => leg.TradeLegId).Distinct().Count() != 4)
            return false;
        return position.Legs.Select(leg => leg.TradeLegId).Distinct().Count() == 4 &&
            position.Legs.All(leg => leg.TradeLegId != Guid.Empty && !string.IsNullOrWhiteSpace(leg.ContractId) &&
                trade.Legs.Any(source => source.TradeLegId == leg.TradeLegId && source.ContractId == leg.ContractId));
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

public enum IronCondorCurrencyEvidenceReconciliation
{
    Unavailable,
    AlreadyPresent,
    Reconciled
}
