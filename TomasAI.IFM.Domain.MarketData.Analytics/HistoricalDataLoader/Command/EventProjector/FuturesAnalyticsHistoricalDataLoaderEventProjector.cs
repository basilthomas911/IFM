using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;

using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.EventProjector;

/// <summary>Durably publishes accepted data load Requested events to the data load Event actor.</summary>
public sealed class FuturesAnalyticsHistoricalDataLoaderEventProjector(
    IDbContextFactory databases,
    IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext eventSource,
    ILogger<FuturesAnalyticsHistoricalDataLoaderEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesAnalyticsHistoricalDataLoaderCommandActor>(
        durableReplayQueue, eventSource, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> descriptors =
    [
        DescribeNotification<FuturesAnalyticsHistoricalDataLoaderRequestedEvent, FuturesAnalyticsHistoricalDataLoaderEntityId>(
            (fact, execution) => ProjectAsync(databases, Requested(fact), execution.EventId, execution.CancellationToken),
            publishProcessingAfterApply: true)
            .WithReadModelRebuild((fact, token) => ProjectAsync(databases, Requested((FuturesAnalyticsHistoricalDataLoaderRequestedEvent)fact), fact.EventId, token)),
        DescribeNotification<FuturesAnalyticsHistoricalDataLoaderCompletedEvent, FuturesAnalyticsHistoricalDataLoaderEntityId>(
            (fact, execution) => ProjectAsync(databases, Completed(fact), execution.EventId, execution.CancellationToken))
            .WithReadModelRebuild((fact, token) => ProjectAsync(databases, Completed((FuturesAnalyticsHistoricalDataLoaderCompletedEvent)fact), fact.EventId, token)),
        DescribeNotification<FuturesAnalyticsHistoricalDataLoaderFailedEvent, FuturesAnalyticsHistoricalDataLoaderEntityId>(
            (fact, execution) => ProjectAsync(databases, Failed(fact), execution.EventId, execution.CancellationToken))
            .WithReadModelRebuild((fact, token) => ProjectAsync(databases, Failed((FuturesAnalyticsHistoricalDataLoaderFailedEvent)fact), fact.EventId, token))
    ];

    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;

    static Task ProjectAsync(IDbContextFactory db, FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel diagnostics,
        long eventId, CancellationToken token) =>
        db.MarketDataDb.UpsertHistoricalAnalyticsAttemptProjectionAsync(diagnostics, eventId, token);

    static FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel Requested(FuturesAnalyticsHistoricalDataLoaderRequestedEvent fact) => new()
    {
        DataLoadAttemptId = fact.EntityId.Value, Status = "Requested",
        LastCompletedBatchOrdinal = -1, LastCompletedRecordOrdinal = -1,
        UpdatedAtUtc = Utc(fact.ReceivedOn)
    };

    static FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel Completed(FuturesAnalyticsHistoricalDataLoaderCompletedEvent fact) => new()
    {
        DataLoadAttemptId = fact.EntityId.Value, Status = "Completed", RequestSha256 = fact.RequestSha256,
        ManifestId = fact.ManifestId == Guid.Empty ? null : fact.ManifestId,
        ValidSessionCount = fact.ValidSessionCount, GapCount = fact.GapCount, RollCount = fact.RollCount,
        LastCompletedBatchOrdinal = fact.LastCompletedBatchOrdinal, LastCompletedRecordOrdinal = fact.LastCompletedRecordOrdinal,
        UpdatedAtUtc = Utc(fact.CompletedAtUtc)
    };

    static FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel Failed(FuturesAnalyticsHistoricalDataLoaderFailedEvent fact) => new()
    {
        DataLoadAttemptId = fact.EntityId.Value, Status = "Failed", ErrorMessage = fact.ErrorMessage,
        LastCompletedBatchOrdinal = fact.LastCompletedBatchOrdinal, LastCompletedRecordOrdinal = fact.LastCompletedRecordOrdinal,
        UpdatedAtUtc = Utc(fact.ReceivedOn)
    };

    static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
