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
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<SpreadDistributionJobCommandActor>(
        durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<SpreadDistributionJobSubmittedEvent, SpreadDistributionJobSubmittedCompleteEvent, SpreadDistributionJobSubmittedFailEvent, SpreadDistributionJobEntityId>(
            e => dbFactory.OptionPricerDb.InsertSpreadDistributionJobAsync(e.SpreadDistributionJob),
            publishProcessingAfterApply: true),
        Describe<SpreadDistributionJobsInProgressDeletedEvent, SpreadDistributionJobsInProgressDeletedCompleteEvent, SpreadDistributionJobsInProgressDeletedFailEvent, OptionTradeEntityId>(
            e => dbFactory.OptionPricerDb.DeleteSpreadDistributionJobsAsync(e.EntityId.OrderId, e.EntityId.TradeId)),
        Describe<SpreadDistributionJobStatusUpdatedEvent, SpreadDistributionJobStatusUpdatedCompleteEvent, SpreadDistributionJobStatusUpdatedFailEvent, SpreadDistributionJobEntityId>(
            e => dbFactory.OptionPricerDb.UpdateSpreadDistributionJobStatusAsync(
                e.EntityId.OrderId, e.EntityId.TradeId, e.EntityId.ValueDate, e.JobStatus, e.ReceivedOn))
    ];

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
}
