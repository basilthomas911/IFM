using System.Collections.Immutable;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Shared.EventSourcing;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.EventProjector;

public sealed class FuturesEodDataEventProjector(
    ICommandActorContext<FuturesEodDataCommandActor> actorContext,
    IHistoricalObservationStore observationStore,
    IMarketSessionCalendar calendar,
    ILogger<FuturesEodDataEventProjector> logger, EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesEodDataCommandActor>(actorContext.DurableReplayQueue, actorContext.DbEventSource, actorContext.BlackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FuturesEodDataInsertedEvent, FuturesEodDataInsertedCompleteEvent, FuturesEodDataInsertedFailEvent, FuturesEodDataId>(
            e => string.Equals(actorContext.DbFactory.MarketDataDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal)
                ? Task.CompletedTask : observationStore.TryWriteRawEodAsync(
                    e.RawObservation ?? FuturesEodRawObservationFactory.Create(e,calendar),CancellationToken.None).AsTask())
            .WithReadModelRebuild((source,token) => RebuildAsync(actorContext,source,token)),
        Describe<VixFuturesEodDataInsertedEvent, VixFuturesEodDataInsertedCompleteEvent, VixFuturesEodDataInsertedFailEvent, FuturesEodDataId>(
            e => string.Equals(actorContext.DbFactory.MarketDataDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal)
                ? Task.CompletedTask : actorContext.DbFactory.MarketDataDb.InsertVixFuturesEodDataAsync(e.VixFuturesTickData,e.SessionStatistics))
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
