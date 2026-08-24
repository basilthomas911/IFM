using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public partial class MarketDataDbContext
{
    /// <summary>
    /// Copies malformed legacy rows to an idempotent quarantine table and rebuilds
    /// lookup entries from valid rows. Canonical source rows are never deleted.
    /// </summary>
    public async Task<FuturesTradeSignalRepairResult> RepairFuturesTradeSignalLookupAsync(
        int batchSize = 256,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        var db = _dbFactory.MarketDataDb;
        var latestRows = new Dictionary<string, FuturesTradeSignalRepairRow>(StringComparer.Ordinal);
        var dateRows = new Dictionary<(string TimePeriod, DateOnly ValueDate, string ContractId), FuturesTradeSignalRepairRow>();
        List<InsertFuturesTradeSignalQuarantine> quarantinedRows = [];
        long rowsScanned = 0;
        long validRowCount = 0;
        long quarantinedRowCount = 0;

        await foreach (var payload in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTradeSignalJsonAll)}", MarketDataDbCql.GetFuturesTradeSignalJsonAll)
            .ExecuteStreamAsync(MapJsonPayload, cancellationToken))
        {
            rowsScanned++;
            var parsed = ParseFuturesTradeSignalRepairRow(payload);
            if (parsed.Row is { } row)
            {
                validRowCount++;
                if (!latestRows.TryGetValue(row.TimePeriod, out var latest) || IsNewer(row, latest))
                    latestRows[row.TimePeriod] = row;
                var dateKey = (row.TimePeriod, row.ValueDate, row.ContractId);
                if (!dateRows.TryGetValue(dateKey, out var dateLatest) || IsNewer(row, dateLatest))
                    dateRows[dateKey] = row;
                continue;
            }

            quarantinedRowCount++;
            quarantinedRows.Add(new InsertFuturesTradeSignalQuarantine(
                Fingerprint(payload),
                payload,
                parsed.Error ?? "Malformed Futures Trade Signal row",
                DateTime.UtcNow));
            if (quarantinedRows.Count >= batchSize)
                await FlushQuarantineAsync().ConfigureAwait(false);
        }

        await FlushQuarantineAsync().ConfigureAwait(false);

        var latestLookupRows = latestRows.Values
            .Select(static row => new InsertFuturesTradeSignalIndex(
                $"latest:{row.TimePeriod}",
                "latest",
                row.SequenceId,
                row.ContractId,
                row.ValueDate,
                row.TimePeriod));
        var dateLookupRows = dateRows.Values
            .Select(static row => new InsertFuturesTradeSignalIndex(
                $"date:{row.TimePeriod}:{row.ValueDate.DayNumber}",
                row.ContractId,
                row.SequenceId,
                row.ContractId,
                row.ValueDate,
                row.TimePeriod));
        var lookupRows = latestLookupRows.Concat(dateLookupRows).ToArray();
        for (var offset = 0; offset < lookupRows.Length; offset += batchSize)
        {
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTradeSignalIndex)}", MarketDataDbCql.InsertFuturesTradeSignalIndex)
                .SetParameters(lookupRows.Skip(offset).Take(batchSize))
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return new FuturesTradeSignalRepairResult(
            rowsScanned,
            validRowCount,
            quarantinedRowCount,
            lookupRows.Length);

        async Task FlushQuarantineAsync()
        {
            if (quarantinedRows.Count == 0)
                return;
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTradeSignalQuarantine)}", MarketDataDbCql.InsertFuturesTradeSignalQuarantine)
                .SetParameters(quarantinedRows)
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
            quarantinedRows.Clear();
        }
    }

    static bool IsNewer(FuturesTradeSignalRepairRow candidate, FuturesTradeSignalRepairRow current)
        => (candidate.ValueDate.DayNumber, candidate.Timestamp.Ticks, candidate.SequenceId)
            .CompareTo((current.ValueDate.DayNumber, current.Timestamp.Ticks, current.SequenceId)) > 0;

    internal static FuturesTradeSignalRepairParseResult ParseFuturesTradeSignalRepairRow(
        string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var contractId = GetString(root, "contractid");
            var valueDateText = GetString(root, "valuedate");
            var timePeriodText = GetString(root, "timeperiod");
            var timestampText = GetString(root, "timestamp");
            var sequenceId = GetInt64(root, "sequenceid");
            List<string> errors = [];

            if (string.IsNullOrWhiteSpace(contractId) || contractId.Contains(','))
                errors.Add("invalid contractId");
            var hasValidDate = DateOnly.TryParseExact(
                    valueDateText,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var valueDate);
            if (!hasValidDate || valueDate == DateOnly.MinValue || valueDate == DateOnly.MaxValue)
                errors.Add("invalid valueDate");
            if (!Enum.TryParse<TimeFrameType>(timePeriodText, true, out var timePeriod) ||
                !Enum.IsDefined(timePeriod))
                errors.Add("invalid timePeriod");
            if (!TimeOnly.TryParse(timestampText, CultureInfo.InvariantCulture, out var timestamp))
                errors.Add("invalid timestamp");
            if (sequenceId < 0)
                errors.Add("invalid sequenceId");

            if (errors.Count != 0)
                return new FuturesTradeSignalRepairParseResult(null, string.Join(", ", errors));

            return new FuturesTradeSignalRepairParseResult(
                new FuturesTradeSignalRepairRow(
                    contractId,
                    valueDate,
                    timePeriod.ToStringFast(),
                    timestamp,
                    sequenceId),
                null);
        }
        catch (JsonException exception)
        {
            return new FuturesTradeSignalRepairParseResult(
                null,
                $"invalid JSON: {exception.Message}");
        }
    }

    static string MapJsonPayload<TDataRecord>(TDataRecord row)
        where TDataRecord : IObjectDataRecord => row.GetString(0);

    static string Fingerprint(string payload)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

    static string GetString(JsonElement root, string name)
    {
        if (!TryGetProperty(root, name, out var property) || property.ValueKind == JsonValueKind.Null)
            return string.Empty;
        return property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : property.ToString();
    }

    static long GetInt64(JsonElement root, string name)
    {
        if (!TryGetProperty(root, name, out var property))
            return -1;
        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var number))
            return number;
        return long.TryParse(property.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
            ? number
            : -1;
    }

    static bool TryGetProperty(JsonElement root, string name, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}

internal sealed record FuturesTradeSignalRepairParseResult(
    FuturesTradeSignalRepairRow? Row,
    string? Error);

internal sealed record FuturesTradeSignalRepairRow(
    string ContractId,
    DateOnly ValueDate,
    string TimePeriod,
    TimeOnly Timestamp,
    long SequenceId);
