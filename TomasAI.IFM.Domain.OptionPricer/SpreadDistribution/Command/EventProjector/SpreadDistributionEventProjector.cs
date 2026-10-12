using TomasAI.IFM.Domain.OptionPricer.Persistence;
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
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command.EventProjector;

public sealed class SpreadDistributionEventProjector(
    IDbContextFactory dbFactory,
    IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource,
    IBlackboardService blackboardService,
    ILogger<SpreadDistributionEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null,
    IPostgresEventTransaction? transactions = null)
    : ConventionalEventProjector<SpreadDistributionCommandActor>(
        durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<SpreadDistributionInsertedEvent, SpreadDistributionInsertedCompleteEvent, SpreadDistributionInsertedFailEvent, SpreadDistributionEntityId>(
            (e, context) => (string.Equals(dbFactory.OptionPricerDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal)) switch
{
    true => Task.CompletedTask,
    false => dbFactory.OptionPricerDb.InsertSpreadDistributionsAsync(e.PutSpreadDistribution with { Id = e.PutSpreadDistribution.Id != 0 ? e.PutSpreadDistribution.Id : StableReplayId(context.EventId, false) }, e.CallSpreadDistribution with { Id = e.CallSpreadDistribution.Id != 0 ? e.CallSpreadDistribution.Id : StableReplayId(context.EventId, true) })})
            .WithReadModelRebuild((source,token) => RebuildAsync(dbFactory,transactions,source,token)),
        Describe<SpreadDistributionDeletedEvent, SpreadDistributionDeletedCompleteEvent, SpreadDistributionDeletedFailEvent, SpreadDistributionEntityId>(
            e => string.Equals(dbFactory.OptionPricerDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? Task.CompletedTask : dbFactory.OptionPricerDb.DeleteSpreadDistributionAsync(e.EntityId.TradeId,e.EntityId.ValueDate))
            .WithReadModelRebuild((source,token) => RebuildAsync(dbFactory,transactions,source,token))
    ];

    static async Task RebuildAsync(IDbContextFactory databases, IPostgresEventTransaction? transactions, IEvent source, CancellationToken token)
    {
        var owner = transactions ?? throw new InvalidOperationException("Option-pricer reconstruction requires the PostgreSQL event transaction service.");
        await owner.ExecuteOnceAsync(async (transaction,cancellation) =>
        {
            await AcceptedOptionPricerProjection.ProjectAsync(transaction,source,cancellation).ConfigureAwait(false);
            return true;
        },token).ConfigureAwait(false);
    }

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;

    // Negative values reserve a replay-only identity range that cannot collide with
    // the positive SpreadDistribution_Id sequence used by ordinary database callers.
    static long StableReplayId(long eventId, bool isCall)
    {
        if (eventId <= 0 || eventId > long.MaxValue / 2)
            throw new ArgumentOutOfRangeException(nameof(eventId), eventId, "A positive persisted event id is required.");

        return isCall ? checked(-(eventId * 2 + 1)) : checked(-(eventId * 2));
    }
}
