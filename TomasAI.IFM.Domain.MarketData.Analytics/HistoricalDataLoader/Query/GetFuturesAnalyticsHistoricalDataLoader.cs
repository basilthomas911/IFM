using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Query.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Query;

/// <summary>Handles one historical data-load diagnostics query.</summary>
public static class GetFuturesAnalyticsHistoricalDataLoader
{
    /// <summary>Reads durable attempt diagnostics and sends the typed reply.</summary>
    public static async ValueTask ExecuteAsync(
        this GetFuturesAnalyticsHistoricalDataLoaderQuery query,
        IQueryActorContext<FuturesAnalyticsHistoricalDataLoaderQueryActor> context,
        CancellationToken cancellationToken)
    {
        var state = await context.DataLoaderStore.GetAsync(
            query.DataLoadAttemptId, cancellationToken).ConfigureAwait(false);
        var result = state is null ? null : new FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel
        {
            DataLoadAttemptId = state.DataLoadAttemptId,
            RequestSha256 = state.RequestSha256,
            Status = state.Status.ToString(),
            ManifestId = state.Manifest?.ManifestId,
            LastCompletedBatchOrdinal = checked((int)state.Checkpoint.BatchOrdinal),
            LastCompletedRecordOrdinal = long.TryParse(state.Checkpoint.SourcePosition, out var ordinal) ? ordinal : -1,
            ValidSessionCount = state.Audit?.ValidSessionCount ?? 0,
            GapCount = state.Audit?.Gaps.Count ?? 0,
            RollCount = state.Audit?.Rolls.Count ?? 0,
            ErrorMessage = state.ErrorMessage,
            UpdatedAtUtc = state.UpdatedAtUtc
        };
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId,
            GetFuturesAnalyticsHistoricalDataLoaderQuery.Verb,
            new ServiceResult<FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel?>(result)).ConfigureAwait(false);
    }
}
