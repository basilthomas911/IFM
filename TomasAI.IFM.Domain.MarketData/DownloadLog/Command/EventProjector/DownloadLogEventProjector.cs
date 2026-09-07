using System.Collections.Immutable;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Actor;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Command.EventProjector;

/// <summary>Replays only committed log outcomes; provider imports are never projection work.</summary>
public sealed class DownloadLogEventProjector(ICommandActorContext<DownloadLogCommandActor> actorContext,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : BaseEventProjector<DownloadLogCommandActor>(actorContext.DurableReplayQueue, actorContext.DbEventSource,
        actorContext.BlackboardService, actorContext.Logger, reliabilityOptions)
{
    public override string ActorName => nameof(DownloadLogCommandActor);
    public override string ProjectorName => nameof(DownloadLogEventProjector);
    public override string DurableProcessQueueName => $"{ActorName}.{ProjectorName}.ProcessQueue";
    public override string DurableReplayQueueName => $"{ActorName}.{ProjectorName}.ReplayQueue";
    public override IReadOnlyCollection<Type> ProjectedEventTypes { get; } = ImmutableArray.Create(typeof(MarketDataDownloadLogInsertedEvent));
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors { get; } = ImmutableArray.Create<EventProjectionDescriptor>(
        new EventProjectionDescriptor(typeof(MarketDataDownloadLogInsertedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                var inserted = (MarketDataDownloadLogInsertedEvent)domainEvent;
                await actorContext.DbFactory.MarketDataDb.InsertMarketDataDownloadLogAsync(
                    inserted.Outcome, inserted.CommandId, inserted.PayloadSha256, execution.CancellationToken);
                return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
            },
            e => ((MarketDataDownloadLogInsertedEvent)e).ToCompleteEvent<MarketDataDownloadLogInsertedCompleteEvent, DownloadLogId>(),
            (e, ex) => ((MarketDataDownloadLogInsertedEvent)e).ToFailEvent<MarketDataDownloadLogInsertedFailEvent, DownloadLogId>(ex),
            useDurableReplay: true)
    );
}
