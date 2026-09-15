using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Event.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Event;

/// <summary>Handles one historical acquisition requested event.</summary>
public static class FuturesAnalyticsHistoricalDataLoaderRequested
{
    static readonly TimeSpan AutomaticRequestMaximumAge = TimeSpan.FromMinutes(5);

    /// <summary>Executes one requested historical acquisition and publishes its terminal event.</summary>
    public static async ValueTask ExecuteAsync(
        this FuturesAnalyticsHistoricalDataLoaderRequestedEvent requested,
        IFuturesAnalyticsHistoricalDataLoaderEventContext context,
        ILogger<FuturesAnalyticsHistoricalDataLoaderEventActor> logger)
    {
        var request = ToApplicationRequest(requested);
        logger.LogInformation(
            "Received historical Analytics request {AttemptId}; automatic={AutomaticStartupWarmup}, received={ReceivedOnUtc}, target={AnalyticsTargetContractId}.",
            requested.EntityId.Value,
            requested.Parameters.AutomaticStartupWarmup,
            requested.ReceivedOn,
            requested.Parameters.AnalyticsTargetContractId);
        try
        {
            if (requested.Parameters.AutomaticStartupWarmup
                && requested.ReceivedOn != default
                && DateTime.UtcNow - DateTime.SpecifyKind(requested.ReceivedOn, DateTimeKind.Utc)
                    > AutomaticRequestMaximumAge)
            {
                logger.LogInformation(
                    "Ignoring stale automatic historical Analytics request {AttemptId} received at {ReceivedOnUtc}.",
                    requested.EntityId.Value,
                    requested.ReceivedOn);
                return;
            }

            HistoricalAnalyticsWarmupResult? warmupResult = null;
            HistoricalDataLoaderState state;
            if (requested.Parameters.AutomaticStartupWarmup)
            {
                logger.LogInformation(
                    "Writing historical Analytics coverage-scan checkpoint for {AttemptId}.",
                    requested.EntityId.Value);
                await context.DataLoaderStore.SaveAsync(new HistoricalDataLoaderState
                {
                    DataLoadAttemptId = requested.EntityId.Value,
                    RequestSha256 = $"coverage-scan:{requested.EntityId.Value:D}",
                    Status = HistoricalDataLoaderStatus.Processing,
                    Checkpoint = new HistoricalAcquisitionCheckpoint
                    {
                        DataLoadAttemptId = requested.EntityId.Value,
                        Stage = HistoricalAcquisitionStage.None
                    },
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                }, CancellationToken.None).ConfigureAwait(false);
                logger.LogInformation(
                    "Historical Analytics coverage-scan checkpoint written for {AttemptId}.",
                    requested.EntityId.Value);
                warmupResult = await context.WarmupService.EnsureAsync(
                    request, CancellationToken.None).ConfigureAwait(false);
                state = ToState(requested, warmupResult);
                await context.DataLoaderStore.SaveAsync(new HistoricalDataLoaderState
                {
                    DataLoadAttemptId = requested.EntityId.Value,
                    RequestSha256 = $"automatic:{requested.EntityId.Value:D}:{warmupResult.Outcome}:{warmupResult.StartDate:O}:{warmupResult.EndDate:O}",
                    Status = HistoricalDataLoaderStatus.Completed,
                    Checkpoint = new HistoricalAcquisitionCheckpoint
                    {
                        DataLoadAttemptId = requested.EntityId.Value,
                        Stage = HistoricalAcquisitionStage.Completed
                    },
                    Audit = new HistoricalDataLoaderAudit(
                        warmupResult.ValidSessionCount,
                        [],
                        []),
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                }, CancellationToken.None).ConfigureAwait(false);
                logger.LogInformation(
                    "Historical Analytics warm-up {Outcome} for {StartDate} through {EndDate}; valid ES sessions {ValidSessionCount}, initially missing sessions {MissingSessionCount}.",
                    warmupResult.Outcome,
                    warmupResult.StartDate,
                    warmupResult.EndDate,
                    warmupResult.ValidSessionCount,
                    warmupResult.MissingSessionCount);
            }
            else
            {
                state = await context.DataLoader.ExecuteAsync(
                    request, CancellationToken.None).ConfigureAwait(false);
            }
            var terminal = new FuturesAnalyticsHistoricalDataLoaderCompletedEvent
            {
                Subject = new(ActorType.Event, FuturesAnalyticsHistoricalDataLoaderEventActor.ActorName,
                    FuturesAnalyticsHistoricalDataLoaderCompletedEvent.Verb, requested.EntityId.Format()),
                Id = Guid.NewGuid(), EntityId = requested.EntityId,
                CommandId = requested.CommandId, AggregateId = requested.EntityId.Format(),
                EventSource = nameof(FuturesAnalyticsHistoricalDataLoaderEventActor),
                ReceivedOn = DateTime.UtcNow,
                ManifestId = state.Manifest?.ManifestId ?? Guid.Empty,
                ValidSessionCount = state.Audit?.ValidSessionCount ?? 0,
                GapCount = state.Audit?.Gaps.Count ?? 0,
                RollCount = state.Audit?.Rolls.Count ?? 0,
                RequestSha256 = state.RequestSha256,
                CompletedAtUtc = DateTime.UtcNow
            };
            await context.SendAsync<
                FuturesAnalyticsHistoricalDataLoaderCompletedEvent,
                FuturesAnalyticsHistoricalDataLoaderEntityId>(terminal).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Historical Analytics request {AttemptId} failed before terminal publication.",
                requested.EntityId.Value);
            var state = await context.DataLoaderStore.GetAsync(
                requested.EntityId.Value, CancellationToken.None).ConfigureAwait(false);
            if (state is not null)
            {
                state = state with
                {
                    Status = HistoricalDataLoaderStatus.Failed,
                    ErrorMessage = exception.Message,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                };
                await context.DataLoaderStore.SaveAsync(state, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            var terminal = new FuturesAnalyticsHistoricalDataLoaderFailedEvent
            {
                Subject = new(ActorType.Event, FuturesAnalyticsHistoricalDataLoaderEventActor.ActorName,
                    FuturesAnalyticsHistoricalDataLoaderFailedEvent.Verb, requested.EntityId.Format()),
                Id = Guid.NewGuid(), EntityId = requested.EntityId,
                CommandId = requested.CommandId, AggregateId = requested.EntityId.Format(),
                EventSource = nameof(FuturesAnalyticsHistoricalDataLoaderEventActor),
                ReceivedOn = DateTime.UtcNow, ErrorMessage = exception.Message,
                LastCompletedBatchOrdinal = checked((int)(state?.Checkpoint.BatchOrdinal ?? -1)),
                LastCompletedRecordOrdinal = ParseRecordOrdinal(state?.Checkpoint.SourcePosition)
            };
            await context.SendAsync<
                FuturesAnalyticsHistoricalDataLoaderFailedEvent,
                FuturesAnalyticsHistoricalDataLoaderEntityId>(terminal).ConfigureAwait(false);
        }
    }

    static MarketDataHistoricalRequest ToApplicationRequest(
        FuturesAnalyticsHistoricalDataLoaderRequestedEvent requested) => new()
    {
        DataLoadAttemptId = requested.EntityId.Value,
        Series = requested.Parameters.Series.Select(value => new MarketDataHistoricalSeriesRequest
        {
            SeriesIdentity = value.MarketSeriesIdentity,
            ContractId = value.ContractId,
            Schema = value.Schema switch
            {
                FuturesAnalyticsHistoricalSchema.OhlcvOneMinute => HistoricalDataSchema.OhlcvOneMinute,
                FuturesAnalyticsHistoricalSchema.Trades => HistoricalDataSchema.Trades,
                FuturesAnalyticsHistoricalSchema.OhlcvDaily => HistoricalDataSchema.OhlcvDaily,
                _ => throw new InvalidOperationException($"Unsupported historical schema {value.Schema}.")
            },
            ExactTradesRequired = value.ExactTradesRequired || requested.Parameters.ExactVwapRequired
        }).ToArray(),
        StartDate = requested.Parameters.StartDate,
        EndDate = requested.Parameters.EndDate,
        MaximumCostUsd = requested.Parameters.MaximumCostUsd,
        MaximumBytes = requested.Parameters.MaximumBytes,
        NormalizationVersion = requested.Parameters.NormalizationVersion,
        RequestedBy = requested.Parameters.RequestedBy,
        AnalyticsTargetContractId = requested.Parameters.AnalyticsTargetContractId
    };

    static long ParseRecordOrdinal(string? sourcePosition) =>
        long.TryParse(sourcePosition, out var value) ? value : -1;

    static HistoricalDataLoaderState ToState(
        FuturesAnalyticsHistoricalDataLoaderRequestedEvent requested,
        HistoricalAnalyticsWarmupResult result)
        => result.LastLoadState ?? new HistoricalDataLoaderState
        {
            DataLoadAttemptId = requested.EntityId.Value,
            RequestSha256 = $"automatic:{result.Outcome}:{result.StartDate:O}:{result.EndDate:O}",
            Status = HistoricalDataLoaderStatus.Completed,
            Checkpoint = new HistoricalAcquisitionCheckpoint
            {
                DataLoadAttemptId = requested.EntityId.Value,
                Stage = HistoricalAcquisitionStage.Completed
            },
            Audit = new HistoricalDataLoaderAudit(result.ValidSessionCount, [], []),
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

}
