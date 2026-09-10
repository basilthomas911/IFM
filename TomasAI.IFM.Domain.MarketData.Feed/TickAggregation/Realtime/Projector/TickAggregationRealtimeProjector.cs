using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Actor;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Projector;

/// <summary>
/// Stores normalized Databento trade and quote observations once and publishes
/// their realtime source/complete/fail lifecycle.
/// </summary>
public sealed class TickAggregationRealtimeProjector(
    IDbContextFactory dbFactory,
    ILogger<TickAggregationRealtimeProjector> logger,
    LivePipelineEvidence healthEvidence)
    : BaseRealtimeProjector<TickAggregationRealtimeActor>(logger)
{
    readonly ImmutableArray<RealtimeProjectionDescriptor> _descriptors =
    [
        Describe<
            FuturesTickTradeDataInsertedEvent,
            FuturesTickTradeDataInsertedCompleteEvent,
            FuturesTickTradeDataInsertedFailEvent,
            TickDataEntityId>(async (e, _) =>
            {
                try
                {
                    await dbFactory.MarketDataDb.InsertTickTradeDataAsync(e).ConfigureAwait(false);
                    healthEvidence.Record(
                        "Tick storage",
                        e.EntityId.ContractId,
                        "Healthy",
                        "Durable trade tick write completed.",
                        DateTime.UtcNow);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    healthEvidence.Record(
                        "Tick storage",
                        e.EntityId.ContractId,
                        "Unhealthy",
                        $"Durable trade tick write failed ({exception.GetType().Name}).");
                    throw;
                }
            }),
        Describe<
            FuturesTickQuoteDataInsertedEvent,
            FuturesTickQuoteDataInsertedCompleteEvent,
            FuturesTickQuoteDataInsertedFailEvent,
            TickDataEntityId>(async (e, _) =>
            {
                try
                {
                    await dbFactory.MarketDataDb.InsertTickQuoteDataAsync(e).ConfigureAwait(false);
                    healthEvidence.Record(
                        "Tick storage",
                        e.EntityId.ContractId,
                        "Healthy",
                        "Durable quote tick write completed.",
                        DateTime.UtcNow);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    healthEvidence.Record(
                        "Tick storage",
                        e.EntityId.ContractId,
                        "Unhealthy",
                        $"Durable quote tick write failed ({exception.GetType().Name}).");
                    throw;
                }
            })
    ];

    public override string ActorName => TickAggregationRealtimeActor.ActorName;
    public override string ProjectorName => nameof(TickAggregationRealtimeProjector);
    public override IReadOnlyCollection<RealtimeProjectionDescriptor> ProjectionDescriptors =>
        _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
}
