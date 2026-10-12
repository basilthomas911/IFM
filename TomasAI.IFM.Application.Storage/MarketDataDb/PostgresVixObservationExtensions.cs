using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.Storage;
using System.Globalization;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Owns the direct VX observation transaction; no business event is appended.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared context receiver.")]
internal static class PostgresVixObservationExtensions
{
    /// <summary>Resolves exact replacement facts while holding the owning transaction's contract/date lock.</summary>
    internal static async Task<VixFuturesEodDataReadModel> ResolveSnapshotAsync(IEnlistedPostgresTransaction transaction,
        FuturesTickDataV2ReadModel tick,FuturesSessionStatisticsSnapshot? statistics,CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(tick);
        if (statistics is { } supplied && (!string.Equals(supplied.ContractId,tick.ContractId,StringComparison.Ordinal) || supplied.ValueDate != tick.ValueDate))
            throw new ArgumentException("Session statistics must match the tick contract and value date.",nameof(statistics));
        var key = string.Create(CultureInfo.InvariantCulture,$"market_data.vix:{tick.ContractId}:{tick.ValueDate:yyyyMMdd}");
        await transaction.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended($1,0));",[key],token)
            .ConfigureAwait(false);
        var rows = await transaction.QueryAsync(MarketDataDbSql.GetVixFuturesEodData,[tick.ContractId,tick.ValueDate],
            reader => new VixFuturesEodDataReadModel(reader.GetString(0),reader.GetFieldValue<DateOnly>(1),reader.GetDecimal(2),
                reader.GetDecimal(3),reader.GetDecimal(4),reader.GetDecimal(5),reader.GetInt64(6)),token)
            .ConfigureAwait(false);
        var old = rows.Count == 0 ? null : rows[0];
        var hasPrices = statistics is { HasPriceStatistics: true };
        var open = hasPrices ? statistics!.Value.OpenPrice : old?.OpenPrice ?? tick.Price;
        var high = hasPrices ? statistics!.Value.HighPrice : Math.Max(old?.HighPrice ?? tick.Price,tick.Price);
        var low = hasPrices ? statistics!.Value.LowPrice : Math.Min(old?.LowPrice ?? tick.Price,tick.Price);
        var volume = statistics is { HasVolume: true } ? statistics.Value.Volume : checked((old?.Volume ?? 0) + tick.Size);
        return new(tick.ContractId,tick.ValueDate,open,high,low,tick.Price,volume);
    }

    extension(MarketDataDbContext context)
    {
        /// <summary>Serializes read-modify-write for one VX contract/date. Session statistics replace cumulative values;
        /// otherwise the tick updates high/low/close and adds its size exactly once per call.</summary>
        internal async Task InsertNativeVixTickAsync(FuturesTickDataV2ReadModel tick, FuturesSessionStatisticsSnapshot? statistics)
        {
            ArgumentNullException.ThrowIfNull(tick);
            if (statistics is { } supplied && (!string.Equals(supplied.ContractId,tick.ContractId,StringComparison.Ordinal) || supplied.ValueDate != tick.ValueDate))
                throw new ArgumentException("Session statistics must match the tick contract and value date.", nameof(statistics));
            var transactions = new PostgresEventTransaction(new DbConnectionSettings()
                .Add(EventSourceActorDbContext.EventSourceActorDbConnection,context.ConnectionString,"System.Data.Postgres"));
            await transactions.ExecuteOnceAsync(async (transaction, token) =>
            {
                var value = await ResolveSnapshotAsync(transaction,tick,statistics,token)
            .ConfigureAwait(false);
                await transaction.ExecuteAsync(MarketDataDbSql.InsertVixFuturesEodData,
                    [value.ContractId,value.ValueDate,value.OpenPrice,value.HighPrice,value.LowPrice,value.ClosePrice,value.Volume],token)
            .ConfigureAwait(false);
                return true;
            })
            .ConfigureAwait(false);
        }
    }
}
