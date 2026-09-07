using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Extensions;
namespace TomasAI.IFM.Application.Storage.MarketDataDb;
public partial class MarketDataDbContext
{
    const string DownloadLogSelect = "SELECT dataset, provider, scope, value_date, requested_at_utc, import_command_id, log_command_id, source_terminal_event_id, schema_version, status, started_at_utc, finished_at_utc, elapsed_milliseconds, downloaded_record_count, persisted_record_count, error_code, error_message, payload_sha256, projected_at_utc FROM market_data_download_log WHERE dataset = :Dataset AND provider = :Provider AND scope = :Scope AND value_date = :ValueDate";
    const string DownloadLogInsert = "INSERT INTO market_data_download_log (dataset, provider, scope, value_date, requested_at_utc, import_command_id, log_command_id, source_terminal_event_id, schema_version, status, started_at_utc, finished_at_utc, elapsed_milliseconds, downloaded_record_count, persisted_record_count, error_code, error_message, payload_sha256, projected_at_utc) VALUES (:Dataset, :Provider, :Scope, :ValueDate, :RequestedAtUtc, :ImportCommandId, :LogCommandId, :SourceTerminalEventId, :SchemaVersion, :Status, :StartedAtUtc, :FinishedAtUtc, :ElapsedMilliseconds, :DownloadedRecordCount, :PersistedRecordCount, :ErrorCode, :ErrorMessage, :PayloadSha256, :ProjectedAtUtc);";
    public async Task InsertMarketDataDownloadLogAsync(MarketDataDownloadOutcome outcome, Guid logCommandId, string payloadSha256, CancellationToken cancellationToken = default)
    {
        var command = new InsertMarketDataDownloadLogCommand(outcome);
        if (command.CommandId != logCommandId || command.PayloadSha256 != payloadSha256)
            throw new ArgumentException("DownloadLog projection identity/hash mismatch.");
        await _dbFactory.MarketDataDb.Use("DownloadLog.Insert", DownloadLogInsert)
            .SetParameters(new DownloadLogParameters(
outcome.Dataset.ToString(), outcome.Provider, outcome.Scope, outcome.ValueDate, outcome.RequestedAtUtc, outcome.ImportCommandId, logCommandId, outcome.SourceTerminalEventId, outcome.SchemaVersion, outcome.Status.ToString(), outcome.StartedAtUtc, outcome.FinishedAtUtc, outcome.ElapsedMilliseconds, outcome.DownloadedRecordCount, outcome.PersistedRecordCount, outcome.ErrorCode, outcome.ErrorMessage, payloadSha256, DateTime.UtcNow))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<MarketDataDownloadLogResult> GetMarketDataDownloadLogAsync(MarketDataDownloadPartition partition, MarketDataDownloadCursor attempt, CancellationToken cancellationToken = default)
    {
        partition.Validate(); ValidateDownloadCursor(attempt);
        var rows = await _dbFactory.MarketDataDb.Use("DownloadLog.Exact", DownloadLogSelect + " AND requested_at_utc = :RequestedAtUtc AND import_command_id = :ImportCommandId LIMIT 1;")
            .SetParameters(new DownloadLogReadParameters(partition.Dataset.ToString(), partition.Provider, partition.Scope, partition.ValueDate, attempt.RequestedAtUtc, attempt.ImportCommandId, 1, true))
            .ExecuteQueryAsync(MapDownloadLog, cancellationToken).ConfigureAwait(false);
        return new(rows.FirstOrDefault());
    }

    public async Task<MarketDataDownloadHistoryResult> GetMarketDataDownloadHistoryAsync(MarketDataDownloadPartition partition, int pageSize = 100, MarketDataDownloadCursor? cursor = null, CancellationToken cancellationToken = default)
    {
        partition.Validate();
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (cursor is not null) ValidateDownloadCursor(cursor);
        var cql = DownloadLogSelect + (cursor is null ? "" : " AND (requested_at_utc, import_command_id) < (:RequestedAtUtc, :ImportCommandId)") + " LIMIT :RowLimit;";
        var rows = await _dbFactory.MarketDataDb.Use(cursor is null ? "DownloadLog.History" : "DownloadLog.HistoryAfter", cql)
            .SetParameters(new DownloadLogReadParameters(partition.Dataset.ToString(), partition.Provider, partition.Scope, partition.ValueDate, cursor?.RequestedAtUtc ?? DateTime.UnixEpoch, cursor?.ImportCommandId ?? Guid.Empty, pageSize + 1))
            .ExecuteQueryAsync(MapDownloadLog, cancellationToken).ConfigureAwait(false);
        var items = rows.Take(pageSize).ToArray();
        var last = items.LastOrDefault()?.Outcome;
        return new(items, rows.Count > pageSize && last is not null ? new(last.RequestedAtUtc, last.ImportCommandId) : null);
    }

    public async Task<MarketDataDownloadStatusResult> GetMarketDataDownloadStatusAsync(MarketDataDownloadPartition partition, Guid? requiredImportCommandId = null, MarketDataDownloadCursor? cursor = null, CancellationToken cancellationToken = default)
    {
        if (requiredImportCommandId == Guid.Empty) throw new ArgumentException("A required import ID cannot be empty.");
        // Each call is bounded; consumers may continue explicitly. Always keep the newest attempt separate.
        var first = await GetMarketDataDownloadHistoryAsync(partition, 100, null, cancellationToken).ConfigureAwait(false);
        var page = cursor is null ? first : await GetMarketDataDownloadHistoryAsync(partition, 100, cursor, cancellationToken).ConfigureAwait(false);
        var selected = page.Attempts.FirstOrDefault(r => requiredImportCommandId.HasValue
            ? r.Outcome.ImportCommandId == requiredImportCommandId
            : r.Outcome.Status == MarketDataDownloadStatus.Completed);
        var success = selected?.Outcome.Status == MarketDataDownloadStatus.Completed ? selected : null;
        return new(success is not null, first.Attempts.FirstOrDefault(), success, page.Continuation is null,
            selected is null ? page.Continuation : null, requiredImportCommandId.HasValue ? selected : null);
    }

    static void ValidateDownloadCursor(MarketDataDownloadCursor cursor)
    {
        if (cursor.ImportCommandId == Guid.Empty || cursor.RequestedAtUtc == default
            || cursor.RequestedAtUtc != MarketDataDownloadOutcome.MillisecondUtc(cursor.RequestedAtUtc))
            throw new ArgumentException("Invalid download attempt cursor.");
    }

    static MarketDataDownloadLogReadModel MapDownloadLog(IObjectDataRecord row)
    {
        var outcome = new MarketDataDownloadOutcome
        {
            Dataset = Enum.Parse<MarketDataDownloadDataset>(row.GetString(0)),
            Provider = row.GetString(1),
            Scope = row.GetString(2),
            ValueDate = row.GetDateOnly(3),
            RequestedAtUtc = DateTime.SpecifyKind(row.GetDateTime(4), DateTimeKind.Utc),
            ImportCommandId = row.GetGuid(5),
            SourceTerminalEventId = row.GetGuid(7),
            SchemaVersion = row.GetShort(8),
            Status = Enum.Parse<MarketDataDownloadStatus>(row.GetString(9)),
            StartedAtUtc = DateTime.SpecifyKind(row.GetDateTime(10), DateTimeKind.Utc),
            FinishedAtUtc = DateTime.SpecifyKind(row.GetDateTime(11), DateTimeKind.Utc),
            ElapsedMilliseconds = row.GetLong(12),
            DownloadedRecordCount = row.IsNull(13) ? null : row.GetLong(13),
            PersistedRecordCount = row.IsNull(14) ? null : row.GetLong(14),
            ErrorCode = row.IsNull(15) ? null : row.GetString(15),
            ErrorMessage = row.IsNull(16) ? null : row.GetString(16),
        };
        outcome.Validate();
        var result = new MarketDataDownloadLogReadModel(outcome, row.GetGuid(6), row.GetString(17), DateTime.SpecifyKind(row.GetDateTime(18), DateTimeKind.Utc));
        if (result.LogCommandId != MarketDataDownloadOutcome.LoggingCommandId(outcome.ImportCommandId) || result.PayloadSha256 != outcome.ComputeHash())
            throw new InvalidOperationException("DownloadLog read model failed integrity validation.");
        return result;
    }
    readonly record struct DownloadLogReadParameters(string Dataset, string Provider, string Scope, DateOnly ValueDate, DateTime RequestedAtUtc, Guid ImportCommandId, int RowLimit, bool Exact = false) : IBindValue
    {
        public object Bind() => Exact
            ? new object[] { Dataset, Provider, Scope, ValueDate, RequestedAtUtc, ImportCommandId }
            : ImportCommandId == Guid.Empty
                ? new object[] { Dataset, Provider, Scope, ValueDate, RowLimit }
                : new object[] { Dataset, Provider, Scope, ValueDate, RequestedAtUtc, ImportCommandId, RowLimit };
    }
    readonly record struct DownloadLogParameters(string Dataset, string Provider, string Scope, DateOnly ValueDate, DateTime RequestedAtUtc, Guid ImportCommandId, Guid LogCommandId, Guid SourceTerminalEventId, short SchemaVersion, string Status, DateTime StartedAtUtc, DateTime FinishedAtUtc, long ElapsedMilliseconds, long? DownloadedRecordCount, long? PersistedRecordCount, string? ErrorCode, string? ErrorMessage, string PayloadSha256, DateTime ProjectedAtUtc) : IBindValue
    {
        public object Bind() => new object?[] { Dataset, Provider, Scope, ValueDate, RequestedAtUtc, ImportCommandId, LogCommandId, SourceTerminalEventId, SchemaVersion, Status, StartedAtUtc, FinishedAtUtc, ElapsedMilliseconds, DownloadedRecordCount, PersistedRecordCount, ErrorCode, ErrorMessage, PayloadSha256, ProjectedAtUtc };
    }
}
