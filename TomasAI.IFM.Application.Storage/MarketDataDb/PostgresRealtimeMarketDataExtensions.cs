using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Owns the standalone PostgreSQL transaction for direct session observations.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared context receiver.")]
internal static class PostgresRealtimeMarketDataExtensions
{
    extension(MarketDataDbContext context)
    {
        /// <summary>Commits history, current snapshots, and lookup copies together without a business event append.</summary>
        internal async Task PersistNativeFuturesEodBatchAsync(IReadOnlyList<BufferedFuturesEodRow> rows)
        {
            var latest = rows.GroupBy(row => (row.Snapshot.ContractId, row.Snapshot.ValueDate, row.Snapshot.Symbol))
                .Select(group => group.Last().Snapshot).ToArray();
            var history = rows.Where(row => row.AppendHistory).Select(row =>
            {
                var e = row.Snapshot;
                return new InsertFuturesIntraDayData(e.ContractId, e.ValueDate,
                    row.HistorySequenceId ?? throw new InvalidOperationException("History sequence must be allocated before persistence."),
                    e.Symbol, e.OpenPrice, e.HighPrice, e.LowPrice, e.ClosePrice, e.Volume,
                    e.DailyPercentChange, e.DailyStdDev, e.DailyStdDevAmount, e.UpperBand, e.Mean,
                    e.LowerBand, e.MarketDirection.ToStringFast(), e.MarketVolatility.ToStringFast(),
                    e.PriceDirection.ToStringFast(), e.PriceVolatility.ToStringFast(), e.MarketDirectionIndicator,
                    e.WindowSize) { PostgreSql = true };
            }).ToArray();
            var commands = context.CreateNativeFuturesEodCommands(latest);
            if (history.Length > 0)
                commands.Add(context.Use(nameof(MarketDataDbSql.InsertFuturesIntraDayData), MarketDataDbSql.InsertFuturesIntraDayData)
                    .SetParameters(history)
            .QueueCommand());
            await context.ExecuteQueuedCommandsAsync(commands, useTransaction: true)
            .ConfigureAwait(false);
        }

        /// <summary>Atomically imports current EOD values and their lookup copies.</summary>
        internal Task InsertNativeFuturesEodBatchAsync(ICollection<FuturesEodDataV2ReadModel> rows)
        {
            MarketDataDbContextExtensions.EnsureDistinctFuturesEodWrites(rows);
            return context.ExecuteQueuedCommandsAsync(context.CreateNativeFuturesEodCommands(rows), useTransaction: true);
        }

        /// <summary>Deletes both current EOD materializations atomically; retained observation history is unchanged.</summary>
        internal Task DeleteNativeFuturesEodAsync(string contractId, DateOnly valueDate)
        {
            List<object> commands =
            [
                context.Use(nameof(MarketDataDbSql.DeleteFuturesEodData), MarketDataDbSql.DeleteFuturesEodData)
                    .SetParameters(new DeleteFuturesEodData(contractId, valueDate) { PostgreSql = true })
            .QueueCommand(),
                context.Use(nameof(MarketDataDbSql.DeleteFuturesEodDataByMonth), MarketDataDbSql.DeleteFuturesEodDataByMonth)
                    .SetParameters(new DeleteFuturesEodDataByMonth(MarketDataDbContextExtensions.ToYearMonth(valueDate), valueDate, contractId) { PostgreSql = true })
            .QueueCommand()
            ];
            return context.ExecuteQueuedCommandsAsync(commands, useTransaction: true);
        }

        /// <summary>Builds typed native commands; no CQL translation occurs at execution.</summary>
        internal List<object> CreateNativeFuturesEodCommands(ICollection<FuturesEodDataV2ReadModel> rows)
        {
            return
            [
                context.Use(nameof(MarketDataDbSql.InsertFuturesEodData), MarketDataDbSql.InsertFuturesEodData)
                    .SetParameters(rows.Select(e => MarketDataDbContextExtensions.CreateFuturesEodDataParameters(e, e.OpenPrice) with { PostgreSql = true }))
            .QueueCommand(),
                context.Use(nameof(MarketDataDbSql.InsertFuturesEodDataByMonth), MarketDataDbSql.InsertFuturesEodDataByMonth)
                    .SetParameters(rows.Select(e => MarketDataDbContextExtensions.CreateFuturesEodDataByMonthParameters(e, e.OpenPrice) with { PostgreSql = true }))
            .QueueCommand(),
                context.Use(nameof(MarketDataDbSql.InsertFuturesEodDataIndex), MarketDataDbSql.InsertFuturesEodDataIndex)
                    .SetParameters(rows.Select(e => new InsertFuturesEodDataIndex(e.ValueDate, e.ContractId) { PostgreSql = true }).Distinct())
            .QueueCommand()
            ];
        }
    }
}
