using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Prepares historical acquisition diagnostics for persistence with the checkpoint.</summary>
public static class HistoricalAnalyticsDiagnosticsProjection
{
    /// <summary>Projects an accepted acquisition checkpoint, without exposing its provider details.</summary>
    /// <param name="state">The accepted checkpoint and optional terminal manifest.</param>
    /// <returns>The user-facing progress projection.</returns>
    public static FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel From(HistoricalDataLoaderState state) =>
        new()
        {
            DataLoadAttemptId = state.DataLoadAttemptId,
            RequestSha256 = state.RequestSha256,
            Status = state.Status.ToString(),
            ManifestId = state.Manifest?.ManifestId,
            LastCompletedBatchOrdinal = checked((int)state.Checkpoint.BatchOrdinal),
            LastCompletedRecordOrdinal = long.TryParse(state.Checkpoint.SourcePosition, System.Globalization.CultureInfo.InvariantCulture, out var ordinal) ? ordinal : -1,
            ValidSessionCount = state.Audit?.ValidSessionCount ?? 0,
            GapCount = state.Audit?.Gaps.Count ?? 0,
            RollCount = state.Audit?.Rolls.Count ?? 0,
            ErrorMessage = state.ErrorMessage,
            UpdatedAtUtc = state.UpdatedAtUtc
        };
}
