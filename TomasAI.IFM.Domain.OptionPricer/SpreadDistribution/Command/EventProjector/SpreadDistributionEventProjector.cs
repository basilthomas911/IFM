using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.Events;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command.EventProjector;

public sealed class SpreadDistributionEventProjector(
    IDbContextFactory dbFactory,
    IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource,
    IBlackboardService blackboardService,
    ILogger<SpreadDistributionEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<SpreadDistributionCommandActor>(
        durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<SpreadDistributionInsertedEvent, SpreadDistributionInsertedCompleteEvent, SpreadDistributionInsertedFailEvent, SpreadDistributionEntityId>(
            (e, context) => dbFactory.OptionPricerDb.InsertSpreadDistributionsAsync(
                e.PutSpreadDistribution with
                {
                    Id = e.PutSpreadDistribution.Id != 0
                        ? e.PutSpreadDistribution.Id
                        : StableReplayId(context.EventId, isCall: false)
                },
                e.CallSpreadDistribution with
                {
                    Id = e.CallSpreadDistribution.Id != 0
                        ? e.CallSpreadDistribution.Id
                        : StableReplayId(context.EventId, isCall: true)
                })),
        Describe<SpreadDistributionDeletedEvent, SpreadDistributionDeletedCompleteEvent, SpreadDistributionDeletedFailEvent, SpreadDistributionEntityId>(
            e => dbFactory.OptionPricerDb.DeleteSpreadDistributionAsync(e.EntityId.TradeId, e.EntityId.ValueDate))
    ];

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();

    // Negative values reserve a replay-only identity range that cannot collide with
    // the positive SpreadDistribution_Id sequence used by ordinary database callers.
    static long StableReplayId(long eventId, bool isCall)
    {
        if (eventId <= 0 || eventId > long.MaxValue / 2)
            throw new ArgumentOutOfRangeException(nameof(eventId), eventId, "A positive persisted event id is required.");

        return isCall ? checked(-(eventId * 2 + 1)) : checked(-(eventId * 2));
    }
}
