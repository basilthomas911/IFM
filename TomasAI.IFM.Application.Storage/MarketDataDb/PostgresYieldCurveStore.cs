using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using Npgsql;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using System.Globalization;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Materializes accepted Treasury curve facts and import ownership on a caller-owned PostgreSQL transaction.</summary>
/// <remarks>No provider requests, event publication or source-event writes occur here. The source transaction may enlist this store.</remarks>
public static class PostgresYieldCurveStore
{
    /// <summary>Rebuilds the date and year indexes from canonical curves under one table-locking maintenance transaction.</summary>
    public static async Task<FmpQueryProjectionBackfillReadModel> RebuildIndexesAsync(IEnlistedPostgresTransaction transaction, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        await transaction.ExecuteAsync("LOCK TABLE market_data.yield_curve_rates,market_data.yield_curve_rate_by_date,market_data.yield_curve_rate_year IN SHARE ROW EXCLUSIVE MODE;",[],token)
            .ConfigureAwait(false);
        const string columns = "valueDate,oneMonth,twoMonth,threeMonth,sixMonth,oneYear,twoYear,threeYear,fiveYear,sevenYear,tenYear,twentyYear,thirtyYear";
        var sourceRows = await transaction.QueryAsync("SELECT " + columns + " FROM market_data.yield_curve_rates WHERE id=1 ORDER BY valueDate;",[],
            reader => MarketDataDbContext.MapToYieldCurveRate(new TomasAI.IFM.Framework.Storage.AdoNetDataRecord().SetReader(reader)),token)
            .ConfigureAwait(false);
        await transaction.ExecuteAsync("DELETE FROM market_data.yield_curve_rate_by_date WHERE lookupId=1;",[],token)
            .ConfigureAwait(false);
        await transaction.ExecuteAsync("DELETE FROM market_data.yield_curve_rate_year WHERE lookupId=1;",[],token)
            .ConfigureAwait(false);
        await transaction.ExecuteAsync("INSERT INTO market_data.yield_curve_rate_by_date(lookupId," + columns + ") SELECT 1," + columns + " FROM market_data.yield_curve_rates WHERE id=1;",[],token)
            .ConfigureAwait(false);
        await transaction.ExecuteAsync("INSERT INTO market_data.yield_curve_rate_year(lookupId,rateYear) SELECT DISTINCT 1,EXTRACT(YEAR FROM valueDate)::integer FROM market_data.yield_curve_rates WHERE id=1;",[],token)
            .ConfigureAwait(false);
        var targetRows = await transaction.QueryAsync("SELECT " + columns + " FROM market_data.yield_curve_rate_by_date WHERE lookupId=1 ORDER BY valueDate;",[],
            reader => MarketDataDbContext.MapToYieldCurveRate(new TomasAI.IFM.Framework.Storage.AdoNetDataRecord().SetReader(reader)),token)
            .ConfigureAwait(false);
        var targetYears = await transaction.QueryAsync("SELECT rateYear FROM market_data.yield_curve_rate_year WHERE lookupId=1 ORDER BY rateYear;",[],reader => reader.GetInt32(0),token)
            .ConfigureAwait(false);
        var sourceIdentity = new ProjectionIdentityBuilder();
        var targetIdentity = new ProjectionIdentityBuilder();
        foreach (var row in sourceRows) sourceIdentity.Add(MarketDataDbContextExtensions.GetYieldCurveProjectionIdentity(row));
        foreach (var row in targetRows) targetIdentity.Add(MarketDataDbContextExtensions.GetYieldCurveProjectionIdentity(row));
        var source = sourceIdentity.Build();
        var target = targetIdentity.Build();
        var sourceYears = sourceRows.Select(row => row.ValueDate.Year).Distinct().ToArray();
        var sourceYearIdentity = MarketDataDbContextExtensions.BuildIntegerSetIdentity(sourceYears);
        var targetYearIdentity = MarketDataDbContextExtensions.BuildIntegerSetIdentity(targetYears);
        if (sourceRows.Count != targetRows.Count || !string.Equals(source.Fingerprint,target.Fingerprint,StringComparison.Ordinal) || !string.Equals(sourceYearIdentity.Fingerprint,targetYearIdentity.Fingerprint,StringComparison.Ordinal))
            throw new InvalidDataException("Treasury index reconstruction failed verification; all index changes will roll back.");
        return new(sourceRows.Count,targetRows.Count,source.Fingerprint,target.Fingerprint,sourceYears.Length,targetYears.Count,sourceYearIdentity.Fingerprint,targetYearIdentity.Fingerprint);
    }

    /// <summary>Atomically writes canonical curves, date lookups and year indexes. Reject policy permits the same owning command
    /// to materialize its accepted rows again, and rejects existing rows or ownership from a different command.</summary>
    public static async Task ImportAsync(IEnlistedPostgresTransaction transaction, IReadOnlyList<YieldCurveRateReadModel> rows,
        ImportDuplicatePolicy policy, Guid commandId, DateTime acceptedAtUtc, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(rows);
        MarketDataDbContextExtensions.ValidateImportPolicy(policy,commandId);
        if (rows.Select(row => row.ValueDate).Distinct().Count() != rows.Count)
            throw new ArgumentException("An import batch must contain one Treasury curve per value date.",nameof(rows));
        // Stable lock order includes empty dates and serializes overwrite imports with reject-policy imports.
        foreach (var row in rows.OrderBy(row => row.ValueDate))
        {
            var key = row.ValueDate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
            await transaction.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended($1,0));",["market_data.treasury-curve:" + key],token)
            .ConfigureAwait(false);
            if (policy == ImportDuplicatePolicy.Reject)
            {
                var owners = await transaction.QueryAsync("SELECT commandId,mayWrite FROM market_data.market_data_import_ownership WHERE dataset=$1 AND logicalKey=$2;",
                    ["treasury-curve",key],reader => (CommandId:reader.GetGuid(0),MayWrite:reader.GetBoolean(1)),token)
            .ConfigureAwait(false);
                if (owners.Count > 0)
                {
                    if (owners[0].CommandId != commandId || !owners[0].MayWrite)
                        throw new MarketDataImportDuplicateException($"Treasury curve {key} is owned by another import command.");
                }
                else
                {
                    var exists = (bool)(await transaction.ScalarAsync("SELECT EXISTS(SELECT 1 FROM market_data.yield_curve_rates WHERE id=1 AND valueDate=$1);",[row.ValueDate],token)
            .ConfigureAwait(false))!;
                    if (exists) throw new MarketDataImportDuplicateException($"Treasury curve {key} already exists.");
                    await transaction.ExecuteAsync("INSERT INTO market_data.market_data_import_ownership(dataset,logicalKey,commandId,mayWrite,createdOn) VALUES($1,$2,$3,$4,$5);",
                        [Text("treasury-curve"),Text(key),Uuid(commandId),Boolean(true),TimestampTz(acceptedAtUtc)],token)
            .ConfigureAwait(false);
                }
            }
            var values = new InsertYieldCurveRate(1,row.ValueDate,row.OneMonth,row.TwoMonth,row.ThreeMonth,row.SixMonth,row.OneYear,
                row.TwoYear,row.ThreeYear,row.FiveYear,row.SevenYear,row.TenYear,row.TwentyYear,row.ThirtyYear) { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.InsertYieldCurveRate,(NpgsqlParameter[])values.Bind(),token)
            .ConfigureAwait(false);
            await transaction.ExecuteAsync(MarketDataDbSql.InsertYieldCurveRateByDate,(NpgsqlParameter[])values.Bind(),token)
            .ConfigureAwait(false);
            await transaction.ExecuteAsync(MarketDataDbSql.InsertYieldCurveRateYear,[Integer(1),Integer(row.ValueDate.Year)],token)
            .ConfigureAwait(false);
        }
    }
}
