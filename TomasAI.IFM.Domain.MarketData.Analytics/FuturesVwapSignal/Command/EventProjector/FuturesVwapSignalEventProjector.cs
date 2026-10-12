using TomasAI.IFM.Domain.MarketData.Analytics.Persistence;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.EventProjector;

/// <summary>
/// Projects rebuildable VWAP transitions to the configured read model.
/// VWAP is recovered from the authoritative trade stream, so individual derived values do not use durable replay.
/// </summary>
public sealed class FuturesVwapSignalEventProjector(
    IDbContextFactory dbFactory,
    IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource,
    IBlackboardService blackboardService,
    ILogger<FuturesVwapSignalEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null,
    IPostgresEventTransaction? transactions = null)
    : ConventionalEventProjector<FuturesVwapSignalCommandActor>(
        durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> descriptors =
    [
        Describe<FuturesVwapSignalUpdatedEvent,
            FuturesVwapSignalUpdatedCompleteEvent,
            FuturesVwapSignalUpdatedFailEvent,
            FuturesVwapSignalEntityId>(value =>
                string.Equals(dbFactory.MarketDataDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? Task.CompletedTask : dbFactory.MarketDataDb.InsertFuturesVwapSignalAsync(value.FuturesVwapSignal),
                useDurableReplay: false)
            .WithReadModelRebuild((source,token) => AcceptedSignalRebuild.MaterializeAsync(dbFactory,transactions,source,token))
    ];

    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    /// <inheritdoc />
}
