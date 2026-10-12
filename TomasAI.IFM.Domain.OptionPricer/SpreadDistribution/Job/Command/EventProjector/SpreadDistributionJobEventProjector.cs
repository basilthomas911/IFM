using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Shared.EventSourcing;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.Events;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.EventProjector;

public sealed class SpreadDistributionJobEventProjector(
    IDbContextFactory dbFactory,
    IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource,
    IBlackboardService blackboardService,
    ILogger<SpreadDistributionJobEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null,
    IPostgresEventTransaction? transactions = null)
    : ConventionalEventProjector<SpreadDistributionJobCommandActor>(
        durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<SpreadDistributionJobSubmittedEvent, SpreadDistributionJobSubmittedCompleteEvent, SpreadDistributionJobSubmittedFailEvent, SpreadDistributionJobEntityId>(
            e => string.Equals(dbFactory.OptionPricerDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? Task.CompletedTask : dbFactory.OptionPricerDb.InsertSpreadDistributionJobAsync(e.SpreadDistributionJob), publishProcessingAfterApply:true)
            .WithReadModelRebuild((source,token) => RebuildAsync(dbFactory,transactions,source,token)),
        Describe<SpreadDistributionJobsInProgressDeletedEvent, SpreadDistributionJobsInProgressDeletedCompleteEvent, SpreadDistributionJobsInProgressDeletedFailEvent, OptionTradeEntityId>(
            e => string.Equals(dbFactory.OptionPricerDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? Task.CompletedTask : dbFactory.OptionPricerDb.DeleteSpreadDistributionJobsAsync(e.EntityId.OrderId,e.EntityId.TradeId))
            .WithReadModelRebuild((source,token) => RebuildAsync(dbFactory,transactions,source,token)),
        Describe<SpreadDistributionJobStatusUpdatedEvent, SpreadDistributionJobStatusUpdatedCompleteEvent, SpreadDistributionJobStatusUpdatedFailEvent, SpreadDistributionJobEntityId>(
            e => string.Equals(dbFactory.OptionPricerDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? Task.CompletedTask : dbFactory.OptionPricerDb.UpdateSpreadDistributionJobStatusAsync(e.EntityId.OrderId,e.EntityId.TradeId,e.EntityId.ValueDate,e.JobStatus,e.ReceivedOn))
            .WithReadModelRebuild((source,token) => RebuildAsync(dbFactory,transactions,source,token))
    ];

    static async Task RebuildAsync(IDbContextFactory databases, IPostgresEventTransaction? transactions, IEvent source, CancellationToken token)
    {
        var owner = transactions ?? throw new InvalidOperationException("Option-pricer reconstruction requires the PostgreSQL event transaction service.");
        await owner.ExecuteOnceAsync(async (transaction,cancellation) =>
        {
            await databases.OptionPricerDb.ProjectAcceptedEventAsync(transaction,source,cancellation).ConfigureAwait(false);
            return true;
        },token).ConfigureAwait(false);
    }

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
}
