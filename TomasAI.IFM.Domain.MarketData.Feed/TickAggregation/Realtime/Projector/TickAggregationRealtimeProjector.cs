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
public sealed class TickAggregationRealtimeProjector
    : BaseRealtimeProjector<TickAggregationRealtimeActor>
{
    readonly IDbContextFactory dbFactory;
    readonly LivePipelineEvidence healthEvidence;
    TickStorageGenerationEvidence? generationEvidence;
    readonly ImmutableArray<RealtimeProjectionDescriptor> _descriptors;

    /// <summary>Creates the production projector without opt-in recovery evidence.</summary>
    public TickAggregationRealtimeProjector(IDbContextFactory dbFactory,
        ILogger<TickAggregationRealtimeProjector> logger, LivePipelineEvidence healthEvidence)
        : this(dbFactory, logger, healthEvidence, null) { }

    /// <summary>Injects bounded generation evidence for isolated recovery qualification only.</summary>
    internal TickAggregationRealtimeProjector(IDbContextFactory dbFactory,
        ILogger<TickAggregationRealtimeProjector> logger, LivePipelineEvidence healthEvidence,
        TickStorageGenerationEvidence? generationEvidence) : base(logger)
    {
        this.dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        this.healthEvidence = healthEvidence ?? throw new ArgumentNullException(nameof(healthEvidence));
        this.generationEvidence = generationEvidence;
        _descriptors =
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
                    if (e.SourceDataset == e.Dataset && e.SourceGenerationId != Guid.Empty)
                        Volatile.Read(ref this.generationEvidence)?.Record(e.SourceDataset, e.SourceGenerationId);
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
                    if (e.SourceDataset == e.Dataset && e.SourceGenerationId != Guid.Empty)
                        Volatile.Read(ref this.generationEvidence)?.Record(e.SourceDataset, e.SourceGenerationId);
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
    }

    public override string ActorName => TickAggregationRealtimeActor.ActorName;
    public override string ProjectorName => nameof(TickAggregationRealtimeProjector);

    /// <summary>Attaches one bounded recovery observer before candidate qualification starts.</summary>
    public void AttachRecoveryEvidence(TickStorageGenerationEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (Interlocked.CompareExchange(ref generationEvidence, evidence, null) is { } existing
            && !ReferenceEquals(existing, evidence))
            throw new InvalidOperationException("Tick-storage recovery evidence is already owned by another observer.");
    }
    public override IReadOnlyCollection<RealtimeProjectionDescriptor> ProjectionDescriptors =>
        _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
}
