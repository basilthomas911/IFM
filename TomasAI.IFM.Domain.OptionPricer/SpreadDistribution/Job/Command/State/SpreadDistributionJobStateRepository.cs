using TomasAI.IFM.Domain.Trade.Shared;
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
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
    {
        var db = dbFactory.OptionPricerDb;
        foreach (var domainEvent in domainEvents)
        {
            _ = domainEvent switch
            {
                SpreadDistributionJobSubmittedEvent e => await InsertJobBeforePublishingAsync(context, db, e),
                SpreadDistributionJobsInProgressDeletedEvent e => await UpdateReadModelAsync<SpreadDistributionJobsInProgressDeletedEvent, SpreadDistributionJobsInProgressDeletedCompleteEvent,
                        SpreadDistributionJobsInProgressDeletedFailEvent, OptionTradeEntityId>(context, e, () => SpreadDistributionJobsInProgressDeletedAsync(db, e)),
                SpreadDistributionJobStatusUpdatedEvent e => await UpdateReadModelAsync<SpreadDistributionJobStatusUpdatedEvent, SpreadDistributionJobStatusUpdatedCompleteEvent,
                        SpreadDistributionJobStatusUpdatedFailEvent, SpreadDistributionJobEntityId>(context, e, () => UpdateSpreadDistributionJobStatusAsync(db, e)),
                _ => false
            };
        }

        static async ValueTask UpdateSpreadDistributionJobStatusAsync(IOptionPricerDbContext db, SpreadDistributionJobStatusUpdatedEvent e)
            => await db.UpdateSpreadDistributionJobStatusAsync(e.EntityId.OrderId, e.EntityId.TradeId, e.EntityId.ValueDate, e.JobStatus, e.ReceivedOn);

        static async ValueTask SpreadDistributionJobsInProgressDeletedAsync(IOptionPricerDbContext db, SpreadDistributionJobsInProgressDeletedEvent e)
            => await db.DeleteSpreadDistributionJobsAsync(e.EntityId.OrderId, e.EntityId.TradeId);
    }

    async ValueTask<bool> InsertJobBeforePublishingAsync(
        ICommandActorContext context,
        IOptionPricerDbContext db,
        SpreadDistributionJobSubmittedEvent e)
    {
        try
        {
            e.CheckForEmptyCommandId();

            // The job event starts asynchronous work that can complete immediately. Persist
            // the in-progress row first so its completion/failure command always has a row to update.
            await db.InsertSpreadDistributionJobAsync(e.SpreadDistributionJob).ConfigureAwait(false);

            EventInitHelper.SetProperty(
                e,
                nameof(IEvent.Subject),
                new ActorSubject(ActorType.Event, e.Subject.Name, e.Subject.Verb, e.EntityId.Format()));
            await context.SendAsync<SpreadDistributionJobSubmittedEvent, SpreadDistributionJobEntityId>(e).ConfigureAwait(false);

            var completed = (SpreadDistributionJobSubmittedCompleteEvent)e
                .ToCompleteEvent<SpreadDistributionJobSubmittedCompleteEvent, SpreadDistributionJobEntityId>();
            await context.SendAsync<SpreadDistributionJobSubmittedCompleteEvent, SpreadDistributionJobEntityId>(completed).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(
                nameof(SpreadDistributionJobStateRepository),
                ex,
                "InsertJobBeforePublishingAsync failed for event: {EventName}",
                nameof(SpreadDistributionJobSubmittedEvent));
            var failed = (SpreadDistributionJobSubmittedFailEvent)e
                .ToFailEvent<SpreadDistributionJobSubmittedFailEvent, SpreadDistributionJobEntityId>(ex);
            await context.SendAsync<SpreadDistributionJobSubmittedFailEvent, SpreadDistributionJobEntityId>(failed).ConfigureAwait(false);
            return false;
        }
    }
}
