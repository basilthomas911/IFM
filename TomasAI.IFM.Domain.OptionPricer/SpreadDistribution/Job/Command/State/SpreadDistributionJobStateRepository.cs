using TomasAI.IFM.Domain.OptionPricer.Persistence;
using TomasAI.IFM.Domain.EventSourcing.Repository;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.Actor;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.Events;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.State;

public class SpreadDistributionJobStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    IDbContextFactory dbFactory,
    IEventProjector<SpreadDistributionJobCommandActor> eventProjector,
    ILogger<SpreadDistributionJobStateRepository> logger,
    IPostgresEventTransaction? transactions = null)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<SpreadDistributionJobCommandState>
{
    /// <summary>
    /// load spread distribution job state from snapshot event
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask<SpreadDistributionJobCommandState> LoadStateAsync(ICommand command)
        => await LoadStateAsync(command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask<SpreadDistributionJobCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken)
        => await LoadStateFromSnapshotAsync<SpreadDistributionJobCommandState, SpreadDistributionJobSubmittedEvent>(command, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// save spread distribution job state changes
    /// </summary>
    /// <param name="context"></param>
    /// <param name="state"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, SpreadDistributionJobCommandState state, ICommand command)
       => await SaveStateAsync(context, state, command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask SaveStateAsync(ICommandActorContext context, SpreadDistributionJobCommandState state, ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        var database = dbFactory.OptionPricerDb;
        if (!string.Equals(database.ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
        {
            await SaveStateAndDenormalizeEventsAsync(context,state,command,cancellationToken).ConfigureAwait(false);
            return;
        }
        if (state.Events.Count == 0) return;
        if (!string.Equals(EventSourceDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ||
            PostgresDatabaseIdentity.Parse(database.ConnectionString) != PostgresDatabaseIdentity.Parse(EventSourceDb.ConnectionString))
            throw new InvalidOperationException("Option-pricer sources and projections must share one PostgreSQL database/security identity.");
        var owner = transactions ?? throw new InvalidOperationException("Option-pricer persistence requires the PostgreSQL event transaction service.");
        var pending = new DomainEventCollection(state.Events);
        await owner.ExecuteAsync(async (transaction,token) =>
        {
            var version = state.CommittedStreamVersion;
            foreach (var source in pending)
            {
                await transaction.AppendAsync(command.StreamId,command.CommandId,source,version++,token).ConfigureAwait(false);
                await AcceptedOptionPricerProjection.ProjectAsync(transaction,source,token).ConfigureAwait(false);
            }
            return true;
        },cancellationToken).ConfigureAwait(false);
        state.AcceptChanges();
        await DenormalizeEventsAsync(context,pending).ConfigureAwait(false);
    }

    /// <summary>
    /// denormalize spread distribution job events to read model
    /// </summary>
    /// <param name="context"></param>
    /// <param name="domainEvents"></param>
    /// <returns></returns>
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
        => eventProjector.DomainEventsProjectionAsync(domainEvents);
}
