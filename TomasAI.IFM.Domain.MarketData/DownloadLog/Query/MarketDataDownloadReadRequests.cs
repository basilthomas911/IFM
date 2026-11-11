using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Query;

/// <summary>Qualifies download log selections and interprets projected attempt history.</summary>
public static class MarketDataDownloadReadRequests
{
    internal static void ValidateSelection(MarketDataDownloadPartition partition, int pageSize, MarketDataDownloadCursor? cursor)
    {
        ArgumentNullException.ThrowIfNull(partition);
        partition.Validate();
        if (pageSize is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (cursor is not null && (cursor.ImportCommandId == Guid.Empty || cursor.RequestedAtUtc == default ||
            cursor.RequestedAtUtc != MarketDataDownloadOutcome.MillisecondUtc(cursor.RequestedAtUtc)))
            throw new ArgumentException("Invalid download attempt cursor.", nameof(cursor));
    }

    extension(IMarketDataDbReadContext database)
    {
        /// <summary>Checks one bounded page for a completed attempt and reports the newest attempt independently.</summary>
        public async Task<MarketDataDownloadStatusResult> GetMarketDataDownloadStatusAsync(MarketDataDownloadPartition partition,
            Guid? requiredImportCommandId = null, MarketDataDownloadCursor? cursor = null, CancellationToken cancellationToken = default)
        {
            if (requiredImportCommandId == Guid.Empty)
                throw new ArgumentException("A required import ID cannot be empty.", nameof(requiredImportCommandId));
            ValidateSelection(partition, 100, cursor);
            var first = await database.GetMarketDataDownloadHistoryAsync(partition, 100, null, cancellationToken)
                .ConfigureAwait(false);
            var page = cursor is null ? first : await database.GetMarketDataDownloadHistoryAsync(partition, 100, cursor, cancellationToken)
                .ConfigureAwait(false);
            var selected = page.Attempts.FirstOrDefault(row => requiredImportCommandId.HasValue
                ? row.Outcome.ImportCommandId == requiredImportCommandId : row.Outcome.Status == MarketDataDownloadStatus.Completed);
            var success = selected?.Outcome.Status == MarketDataDownloadStatus.Completed ? selected : null;
            return new(success is not null, first.Attempts.FirstOrDefault(), success, page.Continuation is null,
                selected is null ? page.Continuation : null, requiredImportCommandId.HasValue ? selected : null);
        }
    }
}
