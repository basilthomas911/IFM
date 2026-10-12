using System.Collections.Immutable;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Shared.EventSourcing;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.EventProjector;

public sealed class FuturesBarDataEventProjector(
    ICommandActorContext<FuturesBarDataCommandActor> actorContext,
    ILogger<FuturesBarDataEventProjector> logger, EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesBarDataCommandActor>(actorContext.DurableReplayQueue, actorContext.DbEventSource, actorContext.BlackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FuturesBarDataStreamingStartedEvent, FuturesBarDataStreamingStartedCompleteEvent, FuturesBarDataStreamingStartedFailEvent, FuturesBarDataStreamingId>(
            static _ => Task.CompletedTask, useDurableReplay: false),
        Describe<FuturesBarDataStreamingStoppedEvent, FuturesBarDataStreamingStoppedCompleteEvent, FuturesBarDataStreamingStoppedFailEvent, FuturesBarDataStreamingId>(
            static _ => Task.CompletedTask, useDurableReplay: false),
        Describe<FuturesBarDataInsertedEvent, FuturesBarDataInsertedCompleteEvent, FuturesBarDataInsertedFailEvent, FuturesBarDataId>(
            e => string.Equals(actorContext.DbFactory.MarketDataDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal)
                ? Task.CompletedTask : actorContext.DbFactory.MarketDataDb.InsertFuturesBarDataAsync(e.FuturesBarData))
            .WithReadModelRebuild((source,token) => RebuildAsync(actorContext,source,token)),
        Describe<FuturesBarDataDeletedEvent, FuturesBarDataDeletedCompleteEvent, FuturesBarDataDeletedFailEvent, FuturesBarDataId>(
            e => string.Equals(actorContext.DbFactory.MarketDataDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal)
                ? Task.CompletedTask : actorContext.DbFactory.MarketDataDb.DeleteFuturesBarDataAsync(e.BarDataId))
            .WithReadModelRebuild((source,token) => RebuildAsync(actorContext,source,token))
    ];
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    static async Task RebuildAsync(ICommandActorContext context,IEvent source,CancellationToken token)
    {
        await context.Container.Resolve<IPostgresEventTransaction>().ExecuteOnceAsync(async(transaction,cancellation) =>
        {
            if (!await PostgresMarketSignalStore.TryProjectAsync(transaction,source,cancellation).ConfigureAwait(false))
                throw new InvalidOperationException("No audited feed materialization exists for this event.");
            return true;
        },token).ConfigureAwait(false);
    }
}
