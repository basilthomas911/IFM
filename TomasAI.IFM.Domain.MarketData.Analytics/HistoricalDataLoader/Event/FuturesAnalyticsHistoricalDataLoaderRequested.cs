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
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requested);
        var request = ToApplicationRequest(requested);
        global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write25(logger, null, requested.EntityId.Value, requested.FuturesAnalyticsHistoricalDataLoaderParameters.AutomaticStartupWarmup, requested.ReceivedOn, requested.FuturesAnalyticsHistoricalDataLoaderParameters.AnalyticsTargetContractId);
        try
        {
            if (requested.FuturesAnalyticsHistoricalDataLoaderParameters.AutomaticStartupWarmup
                && requested.ReceivedOn != default
                && DateTime.UtcNow - DateTime.SpecifyKind(requested.ReceivedOn, DateTimeKind.Utc)
                    > AutomaticRequestMaximumAge)
            {
                global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write26(logger, null, requested.EntityId.Value, requested.ReceivedOn);
                return;
            }

            var state = await LoadRequestedAsync(requested, request, context, logger).ConfigureAwait(false);
            var terminal = new FuturesAnalyticsHistoricalDataLoaderCompletedEvent
            {
                Subject = new(ActorType.Event, FuturesAnalyticsHistoricalDataLoaderEventActor.ActorName,
                    FuturesAnalyticsHistoricalDataLoaderCompletedEvent.Verb, requested.EntityId.Format()),
                Id = Guid.NewGuid(),
                EntityId = requested.EntityId,
                CommandId = requested.CommandId,
                AggregateId = requested.EntityId.Format(),
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
            await PublishFailureAsync(requested, context, logger, exception).ConfigureAwait(false);
        }
    }

    /// <summary>Executes either the requested provider acquisition or the explicit automatic coverage warmup.</summary>
    static async ValueTask<HistoricalDataLoaderState> LoadRequestedAsync(FuturesAnalyticsHistoricalDataLoaderRequestedEvent requested,
        MarketDataHistoricalRequest request, IFuturesAnalyticsHistoricalDataLoaderEventContext context,
        ILogger<FuturesAnalyticsHistoricalDataLoaderEventActor> logger)
    {
            HistoricalAnalyticsWarmupResult? warmupResult = null;
            HistoricalDataLoaderState state;
            if (requested.FuturesAnalyticsHistoricalDataLoaderParameters.AutomaticStartupWarmup)
            {
                global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write27(logger, null, requested.EntityId.Value);
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
                global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write28(logger, null, requested.EntityId.Value);
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
                global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write29(logger, null, warmupResult.Outcome, warmupResult.StartDate, warmupResult.EndDate, warmupResult.ValidSessionCount, warmupResult.MissingSessionCount);
            }
            else
            {
                state = await context.DataLoader.ExecuteAsync(
                    request, CancellationToken.None).ConfigureAwait(false);
            }
        return state;
    }

    /// <summary>Records the failed acquisition checkpoint and publishes its existing terminal failure identity.</summary>
    static async ValueTask PublishFailureAsync(FuturesAnalyticsHistoricalDataLoaderRequestedEvent requested,
        IFuturesAnalyticsHistoricalDataLoaderEventContext context, ILogger<FuturesAnalyticsHistoricalDataLoaderEventActor> logger,
        Exception exception)
    {
            global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write30(logger, exception, requested.EntityId.Value);
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
                Id = Guid.NewGuid(),
                EntityId = requested.EntityId,
                CommandId = requested.CommandId,
                AggregateId = requested.EntityId.Format(),
                EventSource = nameof(FuturesAnalyticsHistoricalDataLoaderEventActor),
                ReceivedOn = DateTime.UtcNow,
                ErrorMessage = exception.Message,
                LastCompletedBatchOrdinal = checked((int)(state?.Checkpoint.BatchOrdinal ?? -1)),
                LastCompletedRecordOrdinal = ParseRecordOrdinal(state?.Checkpoint.SourcePosition)
            };
            await context.SendAsync<
                FuturesAnalyticsHistoricalDataLoaderFailedEvent,
                FuturesAnalyticsHistoricalDataLoaderEntityId>(terminal).ConfigureAwait(false);
    }

    /// <summary>Maps the durable acquisition request to the provider-neutral application request.</summary>
    static MarketDataHistoricalRequest ToApplicationRequest(
        FuturesAnalyticsHistoricalDataLoaderRequestedEvent requested) => new()
        {
            DataLoadAttemptId = requested.EntityId.Value,
            Series = requested.FuturesAnalyticsHistoricalDataLoaderParameters.Series.Select(value => new MarketDataHistoricalSeriesRequest
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
                ExactTradesRequired = value.ExactTradesRequired || requested.FuturesAnalyticsHistoricalDataLoaderParameters.ExactVwapRequired
            }).ToArray(),
            StartDate = requested.FuturesAnalyticsHistoricalDataLoaderParameters.StartDate,
            EndDate = requested.FuturesAnalyticsHistoricalDataLoaderParameters.EndDate,
            MaximumCostUsd = requested.FuturesAnalyticsHistoricalDataLoaderParameters.MaximumCostUsd,
            MaximumBytes = requested.FuturesAnalyticsHistoricalDataLoaderParameters.MaximumBytes,
            NormalizationVersion = requested.FuturesAnalyticsHistoricalDataLoaderParameters.NormalizationVersion,
            RequestedBy = requested.FuturesAnalyticsHistoricalDataLoaderParameters.RequestedBy,
            AnalyticsTargetContractId = requested.FuturesAnalyticsHistoricalDataLoaderParameters.AnalyticsTargetContractId
        };

    /// <summary>Parses a source record ordinal, returning minus one when it is unavailable.</summary>
    static long ParseRecordOrdinal(string? sourcePosition) =>
        long.TryParse(sourcePosition,System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : -1;

    /// <summary>Uses the final load state or constructs the completed automatic-warmup state.</summary>
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
