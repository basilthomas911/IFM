using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using Npgsql;
using System.Globalization;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Materializes accepted economic-calendar facts and their country index on the caller's transaction.</summary>
public static class PostgresEconomicCalendarStore
{
    /// <summary>Writes each dated event once under Reject policy, allowing the same accepted command to materialize it again.</summary>
    public static async Task ImportAsync(IEnlistedPostgresTransaction transaction, IReadOnlyList<EconomicCalendarReadModel> rows,
        ImportDuplicatePolicy policy, Guid commandId, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(rows);
        MarketDataDbContextExtensions.ValidateImportPolicy(policy,commandId);
        var ordered = rows.OrderBy(row => row.CountryCode,StringComparer.Ordinal).ThenBy(row => row.EventDate).ThenBy(row => row.EventName,StringComparer.Ordinal).ToArray();
        var identities = new HashSet<(string,DateTime,string)>();
        foreach (var row in ordered)
        {
            var date = MarketDataDbContextExtensions.NormalizeEconomicCalendarTimestamp(row.EventDate);
            var month = MarketDataDbContextExtensions.EconomicCalendarMonthBucket(date);
            if (!identities.Add((row.CountryCode,date,row.EventName)))
                throw new ArgumentException("An import batch contains duplicate economic-calendar identities.",nameof(rows));
            var key = string.Create(CultureInfo.InvariantCulture,$"economic-calendar:{row.CountryCode.Length}:{row.CountryCode}:{date.Ticks}:{row.EventName}");
            await transaction.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended($1,0));",[key],token)
            .ConfigureAwait(false);
            if (policy == ImportDuplicatePolicy.Reject)
            {
                var owners = await transaction.QueryAsync(MarketDataDbSql.GetEconomicCalendarV2CommandId,
                    (NpgsqlParameter[])new GetEconomicCalendarV2CommandId(row.CountryCode,month,date,row.EventName) { PostgreSql = true }.Bind(),reader => reader.IsDBNull(0) ? Guid.Empty : reader.GetGuid(0),token)
            .ConfigureAwait(false);
                if (owners.Count != 0 && owners[0] != commandId)
                    throw new MarketDataImportDuplicateException("Economic-calendar event is owned by another import command.");
            }
            var values = new InsertEconomicCalendarV2(row.CountryCode,month,date,row.EventName,row.Actual,row.Forecast,row.Prior,row.Impact,row.Unit,row.Change,row.ChangePercentage,
                MarketDataDbContextExtensions.NormalizeEconomicCalendarTimestamp(row.CreatedOn),row.CreatedBy,commandId) { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.InsertEconomicCalendarV2,(NpgsqlParameter[])values.Bind(),token)
            .ConfigureAwait(false);
            await transaction.ExecuteAsync(MarketDataDbSql.InsertEconomicCalendarCountryCode,
                (NpgsqlParameter[])new InsertEconomicCalendarCountryCode(MarketDataDbContext.EconomicCalendarLookupId,row.CountryCode) { PostgreSql = true }.Bind(),token)
            .ConfigureAwait(false);
        }
    }
}
