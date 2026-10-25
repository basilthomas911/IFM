using TomasAI.IFM.Application.EventSourcing.Repository;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;

/// <summary>Persists strategy-configuration lifecycle events in the PostgreSQL event log.</summary>
public sealed class ParameterSetStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource,
    IActorService actorService,
    IEventProjector<ParameterSetCommandActor> projector,
    ILogger<ParameterSetStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceActorStateRepository<ParameterSetCommandState>
{
    /// <summary>Loads the complete configuration stream.</summary>
    public async ValueTask<ParameterSetCommandState> LoadStateAsync(ICommand command)
        => await LoadStateAsync(command, CancellationToken.None)
            .ConfigureAwait(false);

    /// <summary>Loads configuration source facts with cancellation.</summary>
    public async ValueTask<ParameterSetCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken)
        => await LoadStateAsync<ParameterSetCommandState>(command, cancellationToken).ConfigureAwait(false);

    /// <summary>Saves pending lifecycle events atomically.</summary>
    public async ValueTask SaveStateAsync(ICommandActorContext context,
        ParameterSetCommandState state, ICommand command)
        => await SaveStateAsync(context, state, command, CancellationToken.None).ConfigureAwait(false);

    /// <summary>Commits the source event, accepted configuration, audit and applicable cache policy together.</summary>
    public async ValueTask SaveStateAsync(ICommandActorContext context, ParameterSetCommandState state,
        ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        var databases = context.Container.Resolve<IDbContextFactory>();
        var database = databases.ConfigurationDb;
        if (!string.Equals(database.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
        {
            await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken).ConfigureAwait(false);
            return;
        }
        if (state.Events.Count == 0) return;
        if (!string.Equals(EventSourceDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ||
            PostgresDatabaseIdentity.Parse(database.ConnectionString) != PostgresDatabaseIdentity.Parse(EventSourceDb.ConnectionString))
            throw new InvalidOperationException("Parameter source events and projections must share one PostgreSQL database/security identity.");
        var pending = new DomainEventCollection(state.Events);
        var transactions = context.Container.Resolve<IPostgresEventTransaction>();
        await transactions.ExecuteAsync(async (transaction, token) =>
        {
            var version = state.CommittedStreamVersion;
            foreach (var source in pending)
            {
                await transaction.AppendAsync(command.StreamId, command.CommandId, source, version++, token).ConfigureAwait(false);
                await ParameterAcceptedProjection.ApplyAsync(databases, transaction, source, token).ConfigureAwait(false);
            }
            return true;
        }, cancellationToken).ConfigureAwait(false);
        state.AcceptChanges();
        await DenormalizeEventsAsync(context, pending).ConfigureAwait(false);
    }

    /// <summary>Queues committed lifecycle events for ConfigurationDb projection.</summary>
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
        => projector.DomainEventsProjectionAsync(domainEvents);
}
