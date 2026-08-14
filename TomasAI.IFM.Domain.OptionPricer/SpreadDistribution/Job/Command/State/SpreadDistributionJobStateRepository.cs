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
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.State;

public class SpreadDistributionJobStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    IDbContextFactory dbFactory,
    IEventProjector<SpreadDistributionJobCommandActor> eventProjector,
    ILogger<SpreadDistributionJobStateRepository> logger)
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
       => await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// denormalize spread distribution job events to read model
    /// </summary>
    /// <param name="context"></param>
    /// <param name="domainEvents"></param>
    /// <returns></returns>
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
        => eventProjector.DomainEventsProjectionAsync(domainEvents);
}
