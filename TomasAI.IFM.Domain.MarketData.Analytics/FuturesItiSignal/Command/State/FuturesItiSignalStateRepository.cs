using TomasAI.IFM.Application.EventSourcing.Projections.MarketData;
using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Application.EventSourcing.Persistence.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Application.Identity;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;

/// <summary>
/// Provides functionality to manage the state of futures ITI signals, including loading state and saving
/// state changes. This repository is designed to work with event-sourced actors.
/// </summary>
/// <remarks>This class extends <see cref="BaseEventSourceActorRepository"/> and implements <see
/// cref="IEventSourceActorStateRepository{FuturesItiSignalCommandState}"/> to provide specialized behavior for managing <see
/// cref="FuturesItiSignalCommandState"/> entities. It relies on an event-sourcing pattern to persist and retrieve
/// state.</remarks>
/// <param name="aggregateFactory">The factory used to create instances of event source actor state.</param>
/// <param name="dbEventSource">The database context used to access event source actor data.</param>
/// <param name="actorService">The service responsible for managing actor instances.</param>
/// <param name="dbFactory">The database context factory used to create instances of the market data database context.</param>
/// <param name="logger">The logger used to record events and errors.</param>
public class FuturesItiSignalStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    IDbContextFactory dbFactory,
    IEventProjector<FuturesItiSignalCommandActor> eventProjector,
    ILogger<FuturesItiSignalStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IFuturesItiResidentRepository
{
    ICommandActorContext? residentContext;
    readonly Dictionary<string, long> processed = new(StringComparer.Ordinal);
    static readonly CommandAuditMessagePackCodec auditCodec = new();
    public FuturesItiResidentStore? ResidentStore { get; private set; }
    public async ValueTask OpenResidentWriterAsync(ICommandActorContext context, CancellationToken token)
    {
        if (ResidentStore is not null) throw new InvalidOperationException("ITI writer already registered.");
        var source = EventSourceDb as EventSourceActorDbContext
            ?? throw new InvalidOperationException("Resident ITI requires PostgreSQL event storage.");
        ResidentStore = await FuturesItiResidentStore.OpenAsync(source, token).ConfigureAwait(false);
        residentContext = context;
        _ = await ResidentStore.ExecuteAsync((transaction, ct) => transaction.ExecuteAsync(FuturesItiResidentStore.CreateStatement, [], ct), token).ConfigureAwait(false);
    }
    public async ValueTask CloseResidentWriterAsync()
    {
        if (ResidentStore is not null) await ResidentStore.DisposeAsync().ConfigureAwait(false);
        ResidentStore = null;
        residentContext = null;
        processed.Clear();
    }
    public void AdvanceProcessed(string stream, long sequence) => processed[stream] = sequence;
    /// <summary>Rejects cached no-change acknowledgement if another writer advanced this stream.</summary>
    public async ValueTask ValidateNoChangeAsync(FuturesItiSignalCommandState state, ICommand command, CancellationToken cancellationToken)
    {
        if (await EventSourceDb.GetCommittedStreamVersionAsync(command.StreamId, cancellationToken).ConfigureAwait(false)
            != state.CommittedStreamVersion)
            throw new TomasAI.IFM.Shared.Exceptions.ConcurrencyException("Resident ITI stream advanced; reload committed state.");
    }
    /// <summary>
    /// Asynchronously loads the state associated with the specified command.
    /// </summary>
    /// <remarks>This method retrieves the state from a snapshot, which may involve I/O operations. Ensure
    /// that the command is valid and properly initialized before calling this method.</remarks>
    /// <param name="command">The command for which the state is to be loaded. This parameter must not be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the state of type
    /// FuturesItiSignalCommandState.</returns>
    public async ValueTask<FuturesItiSignalCommandState> LoadStateAsync(ICommand command)
        => await LoadStateAsync(command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask<FuturesItiSignalCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken)
        => await LoadStateFromCreationSnapshotWithCheckpointAsync<FuturesItiSignalCommandState, FuturesItiSignalGeneratedEvent>(command, static snapshot => snapshot.FuturesItiSignal is not null, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Saves futures ITI signal state changes and denormalizes the associated domain events.
    /// </summary>
    /// <param name="context">The command actor context providing access to the actor system.</param>
    /// <param name="state">The current command state containing new events to persist.</param>
    /// <param name="command">The command that triggered the state changes.</param>
    /// <returns>A task that represents the asynchronous save and denormalization operation.</returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesItiSignalCommandState state, ICommand command)
       => await SaveStateAsync(context, state, command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesItiSignalCommandState state, ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        var database = dbFactory.MarketDataDb;
        if (!string.Equals(database.ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
        {
            await SaveStateAndDenormalizeEventsAsync(context,state,command,cancellationToken).ConfigureAwait(false);
            return;
        }
        if (state.Events.Count == 0) return;
        if (!string.Equals(EventSourceDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ||
            PostgresDatabaseIdentity.Parse(database.ConnectionString) != PostgresDatabaseIdentity.Parse(EventSourceDb.ConnectionString))
            throw new InvalidOperationException("Analytic source events and MarketDataDb must share one PostgreSQL database/security identity.");
        var owner = context.Container.Resolve<IPostgresEventTransaction>();
        var pending = new DomainEventCollection(state.Events);
        var admission = command is GenerateFuturesItiSignalCommand generate
            && generate.ResidentAdmission is FuturesItiDurableAdmission candidate && candidate.Matches(generate) ? candidate : null;
        async Task<bool> PersistAsync(EnlistedEventTransaction transaction, CancellationToken token)
        {
            if (ResidentStore is null)
            {
                if (await transaction.ScalarAsync("SELECT pg_try_advisory_xact_lock($1)", [FuturesItiResidentStore.Fence], token).ConfigureAwait(false) is not true)
                    throw new InvalidOperationException("ITI stream is owned by another resident writer.");
            }
            else if (!ReferenceEquals(context, residentContext))
                throw new InvalidOperationException("ITI write did not originate from its fenced actor context.");
            var version = state.CommittedStreamVersion;
            foreach (var source in pending)
            {
                if (admission is not null)
                {
                    var input = admission.Inputs.Single(item => item.CommandId == source.CommandId);
                    var outcome = await transaction.ReserveCommandAuditAsync(CommandAuditPreparation.Create(input, auditCodec), token).ConfigureAwait(false);
                    if (outcome == TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditReservationOutcome.PayloadConflict)
                        throw new InvalidOperationException("ITI journal command conflicts with its audit reservation.");
                    if (outcome != TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditReservationOutcome.Accepted
                        && await transaction.HasCommittedCommandEventsAsync(input.CommandId, input.StreamId, token).ConfigureAwait(false))
                        throw new InvalidOperationException("Committed ITI input must be reconciled from its checkpoint, never reapplied.");
                }
                await transaction.AppendAsync(new EnlistedEventAppend(command.StreamId,source.CommandId,source,version++),token).ConfigureAwait(false);
                _ = await AcceptedMarketSignalProjection.TryProjectAsync(transaction,source,token).ConfigureAwait(false);
            }
            var sequence = admission?.Inputs[^1].JournalSequence ?? processed.GetValueOrDefault(command.StreamId);
            if (ResidentStore is not null && sequence > 0)
                await FuturesItiResidentStore.CheckpointAsync(transaction, command.StreamId, sequence, version, token).ConfigureAwait(false);
            return true;
        }
        if (ResidentStore is { } store) _ = await store.ExecuteAsync(PersistAsync, cancellationToken).ConfigureAwait(false);
        else _ = await owner.ExecuteAsync(PersistAsync, cancellationToken).ConfigureAwait(false);
        state.AcceptChanges();
        await DenormalizeEventsAsync(context,pending).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the futures ITI signal
    /// read model asynchronously.
    /// </summary>
    /// <remarks>This method processes each domain event in the provided collection. For
    /// <see cref="FuturesItiSignalGeneratedEvent"/> events, it skips trending mode signals, inserts the signal
    /// into the database, and checks whether the hold-trade state should be preserved on trend direction changes.</remarks>
    /// <param name="context">The command actor context that provides access to the actor's container and state required for denormalization.</param>
    /// <param name="domainEvents">A collection of domain events to be denormalized and applied to the read model state.</param>
    /// <returns>A task that represents the asynchronous denormalization operation.</returns>
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
        => await eventProjector.DomainEventsProjectionAsync(domainEvents).ConfigureAwait(false);
}

