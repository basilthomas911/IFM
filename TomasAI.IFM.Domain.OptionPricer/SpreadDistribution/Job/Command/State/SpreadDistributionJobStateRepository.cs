using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.OptionPricerDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.Trade;

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
        => await LoadStateFromSnapshotAsync<SpreadDistributionJobCommandState, SpreadDistributionJobSubmittedEvent>(command);

    /// <summary>
    /// save spread distribution job state changes
    /// </summary>
    /// <param name="context"></param>
    /// <param name="state"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, SpreadDistributionJobCommandState state, ICommand command)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command);

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
                SpreadDistributionJobSubmittedEvent e => await UpdateReadModelAsync<SpreadDistributionJobSubmittedEvent, SpreadDistributionJobSubmittedCompleteEvent,
                        SpreadDistributionJobSubmittedFailEvent, SpreadDistributionJobEntityId>(context, e, () => InsertSpreadDistributionJobAsync(db, e.SpreadDistributionJob)),
                SpreadDistributionJobsInProgressDeletedEvent e => await UpdateReadModelAsync<SpreadDistributionJobsInProgressDeletedEvent, SpreadDistributionJobsInProgressDeletedCompleteEvent,
                        SpreadDistributionJobsInProgressDeletedFailEvent, OptionTradeEntityId>(context, e, () => SpreadDistributionJobsInProgressDeletedAsync(db, e)),
                SpreadDistributionJobStatusUpdatedEvent e => await UpdateReadModelAsync<SpreadDistributionJobStatusUpdatedEvent, SpreadDistributionJobStatusUpdatedCompleteEvent,
                        SpreadDistributionJobStatusUpdatedFailEvent, SpreadDistributionJobEntityId>(context, e, () => UpdateSpreadDistributionJobStatusAsync(db, e)),
                _ => false
            };
        }

        static async ValueTask InsertSpreadDistributionJobAsync(IOptionPricerDbContext db, SpreadDistributionJobReadModel spreadDistributionJob)
            => await db.InsertSpreadDistributionJobAsync(spreadDistributionJob);

        static async ValueTask UpdateSpreadDistributionJobStatusAsync(IOptionPricerDbContext db, SpreadDistributionJobStatusUpdatedEvent e)
            => await db.UpdateSpreadDistributionJobStatusAsync(e.EntityId.OrderId, e.EntityId.TradeId, e.JobStatus, e.ReceivedOn);

        static async ValueTask SpreadDistributionJobsInProgressDeletedAsync(IOptionPricerDbContext db, SpreadDistributionJobsInProgressDeletedEvent e)
            => await db.DeleteSpreadDistributionJobsAsync(e.EntityId.OrderId, e.EntityId.TradeId);
    }
}
