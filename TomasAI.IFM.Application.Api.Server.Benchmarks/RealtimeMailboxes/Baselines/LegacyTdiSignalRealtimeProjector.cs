using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;

public sealed class LegacyTdiSignalRealtimeProjector(
    IDbContextFactory dbFactory,
    ILogger<LegacyTdiSignalRealtimeProjector> logger)
    : BaseRealtimeProjector<FuturesTdiSignalRealtimeActor>(logger)
{
    readonly ImmutableArray<RealtimeProjectionDescriptor> _descriptors =
    [
        Describe<FuturesTdiSignalGeneratedEvent, FuturesTdiSignalGeneratedCompleteEvent,
            FuturesTdiSignalGeneratedFailEvent, FuturesTdiSignalEntityId>(
            (Func<FuturesTdiSignalGeneratedEvent, Task>)(async e =>
            {
                if (e.FuturesTdiSignal.SchemaVersion != FuturesTdiConfiguration.CurrentSchemaVersion)
                    return;
                await dbFactory.MarketDataDb.InsertFuturesTdiSignalAsync(e.FuturesTdiSignal).ConfigureAwait(false);
                RegimeDiscoverySignalCacheAdapter.Publish(e.FuturesTdiSignal);
            }))
    ];

    public override string ActorName => FuturesTdiSignalRealtimeActor.ActorName;
    public override string ProjectorName => nameof(LegacyTdiSignalRealtimeProjector);
    public override IReadOnlyCollection<RealtimeProjectionDescriptor> ProjectionDescriptors => _descriptors;
}
