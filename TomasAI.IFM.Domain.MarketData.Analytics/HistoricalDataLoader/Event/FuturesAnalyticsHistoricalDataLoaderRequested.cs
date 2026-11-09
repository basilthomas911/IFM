using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;
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

    extension(FuturesAnalyticsHistoricalDataLoaderRequestedEvent requested)
    {
        /// <summary>Executes one requested historical acquisition and publishes its terminal event.</summary>
        public async ValueTask ExecuteAsync(
            IFuturesAnalyticsHistoricalDataLoaderEventContext context,
            ILogger<FuturesAnalyticsHistoricalDataLoaderEventActor> logger)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(requested);
            var request = ToApplicationRequest(requested);
            global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write25(logger, null, requested.EntityId.Value, requested.FuturesAnalyticsHistoricalDataLoaderParameters.AutomaticStartupWarmup, requested.ReceivedOn, requested.FuturesAnalyticsHistoricalDataLoaderParameters.AnalyticsTargetContractId);
            var existing = await context.DataLoaderStore.GetAsync(
                requested.EntityId.Value, CancellationToken.None).ConfigureAwait(false);
            if (existing?.Status != HistoricalDataLoaderStatus.Completed
                && requested.FuturesAnalyticsHistoricalDataLoaderParameters.AutomaticStartupWarmup
                && requested.ReceivedOn != default
                && (context.Container?.Resolve<TimeProvider>() ?? TimeProvider.System).GetUtcNow().UtcDateTime - DateTime.SpecifyKind(requested.ReceivedOn, DateTimeKind.Utc)
                    > AutomaticRequestMaximumAge)
            {
                global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write26(logger, null, requested.EntityId.Value, requested.ReceivedOn);
                return;
            }

            HistoricalDataLoaderState state;
            try
            {
                state = existing?.Status == HistoricalDataLoaderStatus.Completed
                    ? existing
                    : await LoadRequestedAsync(requested, request, context, logger).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await PublishFailureAsync(requested, context, logger, exception).ConfigureAwait(false);
                return;
            }

            var completion = new CompleteFuturesAnalyticsHistoricalDataLoaderCommand
            {
                Subject = new(ActorType.Command, CompleteFuturesAnalyticsHistoricalDataLoaderCommand.Actor,
                    CompleteFuturesAnalyticsHistoricalDataLoaderCommand.Verb, requested.EntityId.Format()),
                EntityId = requested.EntityId,
                CommandId = OutcomeCommandId(requested.CommandId, 1),
                ManifestId = state.Manifest?.ManifestId ?? Guid.Empty,
                ValidSessionCount = state.Audit?.ValidSessionCount ?? 0,
                GapCount = state.Audit?.Gaps.Count ?? 0,
                RollCount = state.Audit?.Rolls.Count ?? 0,
                RequestSha256 = state.Manifest?.RequestSha256 ?? state.RequestSha256,
                LastCompletedBatchOrdinal = checked((int)state.Checkpoint.BatchOrdinal),
                LastCompletedRecordOrdinal = ParseRecordOrdinal(state.Checkpoint.SourcePosition),
                CompletedAtUtc = state.UpdatedAtUtc.UtcDateTime
            };
            RequireAccepted(await context.RequestAsync<CompleteFuturesAnalyticsHistoricalDataLoaderCommand,
                FuturesAnalyticsHistoricalDataLoaderEntityId>(completion).ConfigureAwait(false));
        }
    }

    /// <summary>Executes either the requested provider acquisition or the explicit automatic coverage warmup.</summary>
    static async ValueTask<HistoricalDataLoaderState> LoadRequestedAsync(FuturesAnalyticsHistoricalDataLoaderRequestedEvent requested,
        MarketDataHistoricalRequest request, IFuturesAnalyticsHistoricalDataLoaderEventContext context,
        ILogger<FuturesAnalyticsHistoricalDataLoaderEventActor> logger)
    {
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
                UpdatedAtUtc = (context.Container?.Resolve<TimeProvider>() ?? TimeProvider.System).GetUtcNow()
            }, CancellationToken.None).ConfigureAwait(false);
            global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write28(logger, null, requested.EntityId.Value);
            var warmupResult = await context.WarmupService.EnsureAsync(
                request, CancellationToken.None).ConfigureAwait(false);
            var lastLoad = warmupResult.LastLoadState;
            state = new HistoricalDataLoaderState
            {
                DataLoadAttemptId = requested.EntityId.Value,
                RequestSha256 = $"automatic:{requested.EntityId.Value:D}:{warmupResult.Outcome}:{warmupResult.StartDate:O}:{warmupResult.EndDate:O}",
                Status = HistoricalDataLoaderStatus.Completed,
                Checkpoint = (lastLoad?.Checkpoint ?? new HistoricalAcquisitionCheckpoint
                {
                    DataLoadAttemptId = requested.EntityId.Value
                }) with { DataLoadAttemptId = requested.EntityId.Value, Stage = HistoricalAcquisitionStage.Completed },
                Manifest = lastLoad?.Manifest,
                Audit = lastLoad?.Audit ?? new HistoricalDataLoaderAudit(warmupResult.ValidSessionCount, [], []),
                UpdatedAtUtc = (context.Container?.Resolve<TimeProvider>() ?? TimeProvider.System).GetUtcNow()
            };
            await context.DataLoaderStore.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);
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
                UpdatedAtUtc = (context.Container?.Resolve<TimeProvider>() ?? TimeProvider.System).GetUtcNow()
            };
            await context.DataLoaderStore.SaveAsync(state, CancellationToken.None)
                .ConfigureAwait(false);
        }
        var failure = new FailFuturesAnalyticsHistoricalDataLoaderCommand
        {
            Subject = new(ActorType.Command, FailFuturesAnalyticsHistoricalDataLoaderCommand.Actor,
                FailFuturesAnalyticsHistoricalDataLoaderCommand.Verb, requested.EntityId.Format()),
            EntityId = requested.EntityId,
            CommandId = OutcomeCommandId(requested.CommandId, 2),
            ErrorMessage = exception.Message,
            LastCompletedBatchOrdinal = checked((int)(state?.Checkpoint.BatchOrdinal ?? -1)),
            LastCompletedRecordOrdinal = ParseRecordOrdinal(state?.Checkpoint.SourcePosition)
        };
        RequireAccepted(await context.RequestAsync<FailFuturesAnalyticsHistoricalDataLoaderCommand,
            FuturesAnalyticsHistoricalDataLoaderEntityId>(failure).ConfigureAwait(false));
    }

    /// <summary>Derives a stable command identity for one outcome of the accepted request.</summary>
    static Guid OutcomeCommandId(Guid requestId, byte outcome)
    {
        Span<byte> identity = stackalloc byte[17];
        requestId.TryWriteBytes(identity);
        identity[16] = outcome;
        return new Guid(SHA256.HashData(identity).AsSpan(0, 16));
    }

    /// <summary>Raises a retryable processing failure when the owning command actor cannot accept the outcome.</summary>
    static void RequireAccepted(ServiceResult<GuidResult>? result)
    {
        if (result?.Success != true)
            throw new InvalidOperationException(result?.ErrorMessage ?? "Historical load outcome was not acknowledged.");
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
        long.TryParse(sourcePosition, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : -1;
}
