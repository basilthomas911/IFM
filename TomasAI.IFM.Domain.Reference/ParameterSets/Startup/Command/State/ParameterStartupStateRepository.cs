using TomasAI.IFM.Domain.Reference.ParameterSets.Persistence;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Actor;
using TomasAI.IFM.Application.EventSourcing.Repository;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.State;

/// <summary>Persists strategy-configuration lifecycle events in the PostgreSQL event log.</summary>
public sealed class ParameterStartupStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource,
    IActorService actorService,
    IEventProjector<ParameterStartupCommandActor> projector,
    ILogger<ParameterStartupStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceActorStateRepository<ParameterStartupCommandState>
{
    /// <summary>Loads the complete configuration stream.</summary>
    public async ValueTask<ParameterStartupCommandState> LoadStateAsync(ICommand command)
        => await LoadStateAsync(command, CancellationToken.None)
            .ConfigureAwait(false);

    /// <summary>Loads configuration source facts with cancellation.</summary>
    public async ValueTask<ParameterStartupCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var state = await LoadStateFromLatestCheckpointAsync<ParameterStartupCommandState, ParameterStartupStateSnapshottedEvent>(
            command, ParameterStartupCommandState.IsCompleteSnapshot, cancellationToken).ConfigureAwait(false);
        if (state.Revision != 0 || command is not IParameterStartupMutation startup || startup.RunId == Guid.Empty) return state;
        var registry = ParameterStartupEntityId.Registry;
        var legacyAddress = new ApplySignalStartupPlanCommand
        {
            EntityId = registry,
            Subject = new ActorSubject(ActorType.Command, ApplySignalStartupPlanCommand.Actor, ApplySignalStartupPlanCommand.Verb, registry.Format())
        };
        var legacy = await LoadStateFromCreationSnapshotAsync<ParameterStartupRegistryState>(legacyAddress, cancellationToken).ConfigureAwait(false);
        if (!legacy.AllRuns.TryGetValue(startup.RunId, out var run)) return state;
        var operationBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("ParameterStartupRegistryImport:" + startup.RunId.ToString("N")));
        var creation = new ParameterStartupCreatedEvent
        {
            CommandId = new Guid(operationBytes.AsSpan(0, 16)), Id = new Guid(operationBytes.AsSpan(0, 16)),
            EventSource = ApplySignalStartupPlanCommand.Actor,
            EntityId = new(startup.RunId), RunId = startup.RunId,
            Subject = new ActorSubject(ActorType.Event, ParameterStartupCreatedEvent.Actor, ParameterStartupCreatedEvent.Verb, startup.RunId.ToString("N")),
            Revision = 1, Run = run, Report = legacy.Reports.GetValueOrDefault(startup.RunId),
            ImportedFromRegistry = true, ReleasedAtCreation = !legacy.ActiveRuns.ContainsKey(startup.RunId)
        };
        state.ReplayEvents(new IEvent[] { creation });
        state.RegistryCreation = creation;
        return state;
    }

    /// <summary>Saves pending lifecycle events atomically.</summary>
    public async ValueTask SaveStateAsync(ICommandActorContext context,
        ParameterStartupCommandState state, ICommand command)
        => await SaveStateAsync(context, state, command, CancellationToken.None).ConfigureAwait(false);

    /// <summary>Commits the source events, pending projection markers, and evidence fence together.</summary>
    public async ValueTask SaveStateAsync(ICommandActorContext context, ParameterStartupCommandState state,
        ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        if (state.Events.Count == 0) return;
        if (state.RegistryCreation is { } importedCreation && state.Events[0] is not ParameterStartupCreatedEvent)
            state.Events.Insert(0, importedCreation);
        var databases = context.Container.Resolve<IDbContextFactory>();
        var database = databases.ConfigurationDb;
        if (!string.Equals(database.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
        {
            await SaveStateWithPeriodicSnapshotAsync(context, state, command, () => state.CaptureSnapshot(command), cancellationToken).ConfigureAwait(false);
            state.RegistryCreation = null;
            return;
        }
        if (state.Events.Count == 0) return;
        if (!string.Equals(EventSourceDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ||
            PostgresDatabaseIdentity.Parse(database.ConnectionString) != PostgresDatabaseIdentity.Parse(EventSourceDb.ConnectionString))
            throw new InvalidOperationException("Parameter source events and projections must share one PostgreSQL database/security identity.");
        var pending = new DomainEventCollection(state.Events);
        var snapshot = ShouldCapturePeriodicSnapshot(state.CommittedStreamVersion, pending.Count)
            ? state.CaptureSnapshot(command) : null;
        var transactions = context.Container.Resolve<IPostgresEventTransaction>();
        await transactions.ExecuteAsync(async (transaction, token) =>
        {
            var version = state.CommittedStreamVersion;
            foreach (var source in pending)
            {
                var eventId = await transaction.AppendAsync(command.StreamId, command.CommandId, source, version++, token).ConfigureAwait(false);
                await database.MarkParameterProjectionPendingAsync(transaction, eventId, token).ConfigureAwait(false);
            }
            if (snapshot is not null)
                await transaction.AppendAsync(command.StreamId, command.CommandId, snapshot, version, token).ConfigureAwait(false);
            await database.AdvanceParameterRevisionFenceAsync(transaction, token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
        state.AcceptChanges();
        if (snapshot is not null)
            state.RestoreCommittedStreamVersion(checked(state.CommittedStreamVersion + 1));
        state.RegistryCreation = null;
        // First apply through the projector after commit so the normal reply still supports immediate reads.
        // The committed marker remains recoverable if this process stops before projection or enqueue.
        foreach (var source in pending)
            await projector.ProcessDomainEventAsync(source).ConfigureAwait(false);
        await DenormalizeEventsAsync(context, pending).ConfigureAwait(false);
    }

    /// <summary>Queues committed lifecycle events for ConfigurationDb projection.</summary>
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
        => projector.DomainEventsProjectionAsync(domainEvents);
}
