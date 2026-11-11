using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.MarketData.Persistence;

/// <summary>Owns projection maintenance and reconciliation; Storage applies only typed row operations.</summary>
public static class MarketDataProjectionMaintenance
{
    extension(IMarketDataDbContext context)
    {
        /// <summary>Repairs and verifies canonical derived indexes without touching raw Scylla ticks.</summary>
        public Task<MarketDataProjectionBackfillReadModel> BackfillQueryProjectionsV2Async(int batchSize = 256,
            CancellationToken cancellationToken = default, DateTime? staleOperationCutoffUtc = null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
            if (staleOperationCutoffUtc is { Kind: not DateTimeKind.Utc })
                throw new ArgumentException("The cutoff must use DateTimeKind.Utc.", nameof(staleOperationCutoffUtc));
            if (staleOperationCutoffUtc > TimeProvider.System.GetUtcNow().UtcDateTime)
                throw new ArgumentOutOfRangeException(nameof(staleOperationCutoffUtc));
            cancellationToken.ThrowIfCancellationRequested();
            return Transactions(context).ExecuteAsync(
                (transaction, token) => ApplyCanonicalIndexesAsync(context, transaction, token), cancellationToken);
        }

        /// <summary>Reconciles exact Treasury date and year lookup identities before committing.</summary>
        public Task<FmpQueryProjectionBackfillReadModel> BackfillFmpQueryProjectionsAsync(int batchSize = 256,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
            return Transactions(context).ExecuteAsync(async (transaction, token) =>
                VerifyYieldCurveIndexes(await context.DbWriter.RefreshYieldCurveLookupRowsAsync(transaction, token)
                    .ConfigureAwait(false)), cancellationToken);
        }

        /// <summary>Quarantines malformed facts and replaces lookup rows without changing the canonical source.</summary>
        public Task<FuturesTradeSignalRepairReadModel> RepairFuturesTradeSignalLookupAsync(int batchSize = 256,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
            return Transactions(context).ExecuteAsync(
                (transaction, token) => RepairTradeSignalIndexesAsync(context, transaction, TimeProvider.System, token),
                cancellationToken);
        }
    }

    static PostgresEventTransaction Transactions(IMarketDataDbContext context) =>
        new(new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,
            context.ConnectionString, "System.Data.Postgres"));

    /// <summary>Applies and qualifies the physical index changes on the caller's transaction.</summary>
    public static async Task<MarketDataProjectionBackfillReadModel> ApplyCanonicalIndexesAsync(IMarketDataDbContext context,
        IEnlistedPostgresTransaction transaction, CancellationToken token)
    {
        var rows = await context.DbWriter.RefreshCanonicalQueryIndexRowsAsync(transaction, token).ConfigureAwait(false);
        var inventory = rows.ToDictionary(row => row.Kind);
        var eod = inventory["eod"]; var vix = inventory["vix"]; var iti = inventory["iti"];
        var readiness = await context.DbWriter.ReadCanonicalQueryReadinessAsync(transaction, token).ConfigureAwait(false);
        return new(0, 0, "not-applicable:TradeTickQuoteDb", "not-applicable:TradeTickQuoteDb",
            eod.Count, eod.Count, eod.Fingerprint, eod.Fingerprint,
            inventory["vix_rows"].Count, vix.Count, vix.Count, vix.Fingerprint, vix.Fingerprint,
            iti.Count, iti.Count, iti.Count, iti.Count, iti.Fingerprint, iti.Fingerprint,
            iti.Fingerprint, iti.Fingerprint, readiness.IsReady) { RawTicksManagedByTradeTickQuoteDb = true };
    }

    static FmpQueryProjectionBackfillReadModel VerifyYieldCurveIndexes(YieldCurveLookupInventory inventory)
    {
        var source = new ProjectionIdentityBuilder();
        var target = new ProjectionIdentityBuilder();
        foreach (var row in inventory.Canonical) source.Add(GetYieldCurveProjectionIdentity(row));
        foreach (var row in inventory.DateIndex) target.Add(GetYieldCurveProjectionIdentity(row));
        var original = source.Build();
        var copied = target.Build();
        var years = inventory.Canonical.Select(row => row.ValueDate.Year).Distinct().ToArray();
        var sourceYears = BuildIntegerSetIdentity(years);
        var targetYears = BuildIntegerSetIdentity(inventory.YearIndex);
        if (inventory.Canonical.Count != inventory.DateIndex.Count
            || !string.Equals(original.Fingerprint, copied.Fingerprint, StringComparison.Ordinal)
            || !string.Equals(sourceYears.Fingerprint, targetYears.Fingerprint, StringComparison.Ordinal))
            throw new InvalidDataException("Treasury index reconstruction failed verification; all index changes will roll back.");
        return new(inventory.Canonical.Count, inventory.DateIndex.Count, original.Fingerprint, copied.Fingerprint,
            years.Length, inventory.YearIndex.Count, sourceYears.Fingerprint, targetYears.Fingerprint);
    }

    static async Task<FuturesTradeSignalRepairReadModel> RepairTradeSignalIndexesAsync(IMarketDataDbContext context,
        IEnlistedPostgresTransaction transaction, TimeProvider clock, CancellationToken token)
    {
        var payloads = await context.DbWriter.ReadLockedTradeSignalPayloadsAsync(transaction, token).ConfigureAwait(false);
        var latest = new Dictionary<string, FuturesTradeSignalRepairRow>(StringComparer.Ordinal);
        var daily = new Dictionary<(string, DateOnly, string), FuturesTradeSignalRepairRow>();
        long valid = 0, quarantined = 0;
        foreach (var payload in payloads)
        {
            var parsed = ParseFuturesTradeSignalRepairRow(payload);
            if (parsed.Row is not { } row)
            {
                quarantined++;
                var quarantine = new FuturesTradeSignalQuarantineRow(
                    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))), payload,
                    parsed.Error ?? "Malformed identity", clock.GetUtcNow().UtcDateTime);
                await context.DbWriter.StoreTradeSignalQuarantineRowAsync(transaction, quarantine, token).ConfigureAwait(false);
                continue;
            }
            valid++;
            if (!latest.TryGetValue(row.TimePeriod, out var current) || IsNewer(row, current)) latest[row.TimePeriod] = row;
            var key = (row.TimePeriod, row.ValueDate, row.ContractId);
            if (!daily.TryGetValue(key, out var dated) || IsNewer(row, dated)) daily[key] = row;
        }
        var indexes = latest.Values.Select(row => new FuturesTradeSignalLookupRow($"latest:{row.TimePeriod}", "latest",
                row.SequenceId, row.ContractId, row.ValueDate, row.TimePeriod))
            .Concat(daily.Values.Select(row => new FuturesTradeSignalLookupRow($"date:{row.TimePeriod}:{row.ValueDate.DayNumber}",
                row.ContractId, row.SequenceId, row.ContractId, row.ValueDate, row.TimePeriod))).ToArray();
        await context.DbWriter.ReplaceTradeSignalLookupRowsAsync(transaction, indexes, token).ConfigureAwait(false);
        return new(payloads.Count, valid, quarantined, indexes.Length);
    }

    static bool IsNewer(FuturesTradeSignalRepairRow candidate, FuturesTradeSignalRepairRow current) =>
        (candidate.ValueDate.DayNumber, candidate.Timestamp.Ticks, candidate.SequenceId)
        .CompareTo((current.ValueDate.DayNumber, current.Timestamp.Ticks, current.SequenceId)) > 0;

    static string GetString(JsonElement root, string name)
    {
        if (!TryGetProperty(root, name, out var property) || property.ValueKind == JsonValueKind.Null) return string.Empty;
        return property.ValueKind == JsonValueKind.String ? property.GetString() ?? string.Empty : property.ToString();
    }

    static long GetInt64(JsonElement root, string name)
    {
        if (!TryGetProperty(root, name, out var property)) return -1;
        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var number)) return number;
        return long.TryParse(property.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ? number : -1;
    }

    static bool TryGetProperty(JsonElement root, string name, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        value = default;
        return false;
    }

    internal static FuturesTradeSignalRepairParseResult ParseFuturesTradeSignalRepairRow(string payload)
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
            if (string.IsNullOrWhiteSpace(contractId) || contractId.Contains(',', System.StringComparison.Ordinal))
                errors.Add("invalid contractId");
            var hasValidDate = DateOnly.TryParseExact(valueDateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var valueDate);
            if (!hasValidDate || valueDate == DateOnly.MinValue || valueDate == DateOnly.MaxValue)
                errors.Add("invalid valueDate");
            if (!Enum.TryParse<TimeFrameType>(timePeriodText, true, out var timePeriod) || !Enum.IsDefined(timePeriod))
                errors.Add("invalid timePeriod");
            if (!TimeOnly.TryParse(timestampText, CultureInfo.InvariantCulture, out var timestamp))
                errors.Add("invalid timestamp");
            if (sequenceId < 0)
                errors.Add("invalid sequenceId");
            if (errors.Count != 0)
                return new FuturesTradeSignalRepairParseResult(null, string.Join(", ", errors));
            return new FuturesTradeSignalRepairParseResult(new FuturesTradeSignalRepairRow(contractId, valueDate, timePeriod.ToStringFast(), timestamp, sequenceId), null);
        }
        catch (JsonException exception)
        {
            return new FuturesTradeSignalRepairParseResult(null, $"invalid JSON: {exception.Message}");
        }
    }



    static ulong GetYieldCurveProjectionIdentity(YieldCurveRateReadModel row)
    {
        var hash = MarketDataProjectionHash.Add(MarketDataProjectionHash.Start(), row.ValueDate);
        hash = MarketDataProjectionHash.Add(hash, row.OneMonth);
        hash = MarketDataProjectionHash.Add(hash, row.TwoMonth);
        hash = MarketDataProjectionHash.Add(hash, row.ThreeMonth);
        hash = MarketDataProjectionHash.Add(hash, row.SixMonth);
        hash = MarketDataProjectionHash.Add(hash, row.OneYear);
        hash = MarketDataProjectionHash.Add(hash, row.TwoYear);
        hash = MarketDataProjectionHash.Add(hash, row.ThreeYear);
        hash = MarketDataProjectionHash.Add(hash, row.FiveYear);
        hash = MarketDataProjectionHash.Add(hash, row.SevenYear);
        hash = MarketDataProjectionHash.Add(hash, row.TenYear);
        hash = MarketDataProjectionHash.Add(hash, row.TwentyYear);
        return MarketDataProjectionHash.Add(hash, row.ThirtyYear);
    }

    static ProjectionIdentity BuildIntegerSetIdentity(IEnumerable<int> values)
    {
        var identity = new ProjectionIdentityBuilder();
        foreach (var value in values)
            identity.Add(MarketDataProjectionHash.Add(MarketDataProjectionHash.Start(), value));
        return identity.Build();
    }

}

internal sealed record FuturesTradeSignalRepairParseResult(FuturesTradeSignalRepairRow? Row, string? Error);

/// <summary>Stored trade-signal identity used to rebuild derived lookup entries.</summary>
/// <param name="ContractId">The futures contract identifier.</param>
/// <param name="ValueDate">The signal's value date.</param>
/// <param name="TimePeriod">The persisted signal period.</param>
/// <param name="Timestamp">The signal's time within its value date.</param>
/// <param name="SequenceId">The sequence used to order signal identities.</param>
internal sealed record FuturesTradeSignalRepairRow(
    string ContractId,
    DateOnly ValueDate,
    string TimePeriod,
    TimeOnly Timestamp,
    long SequenceId);
