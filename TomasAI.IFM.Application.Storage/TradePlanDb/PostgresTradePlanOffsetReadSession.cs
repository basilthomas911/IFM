using Npgsql;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage.Postgres;
using static TomasAI.IFM.Shared.Storage.BindValues;

namespace TomasAI.IFM.Application.Storage.TradePlanDb;

/// <summary>Owns the connection and repeatable read transaction for a history page and optional chart.</summary>
internal sealed class PostgresTradePlanOffsetReadSession(
    NpgsqlConnection connection, NpgsqlTransaction transaction, string table, object?[] scope, StrategyTradePlanHistoryPage page)
    : ITradePlanOffsetReadSession
{
    public StrategyTradePlanHistoryPage Page => page;

    internal static async Task<ITradePlanOffsetReadSession> OpenAsync(string connectionString, string table, StrategyPositionId position,
        DateOnly date, int offset, int size, long? maximumRevision, CancellationToken token)
    {
        var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
        NpgsqlTransaction? transaction = null;
        try
        {
            await connection.OpenAsync(token)
            .ConfigureAwait(false);
            transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, token)
            .ConfigureAwait(false);
            var scope = (object?[])new GetTradePlan(position.Trade.PortfolioId, position.Trade.FundId,
                position.Trade.OrderId, position.Trade.TradeId, position.PositionId, date).Bind();
            await using var count = new NpgsqlCommand(TradePlanDbSql.OffsetHistoryMetadata(table), connection, transaction);
            PostgresValueBinding.AddParameters(count, [.. scope, Bigint(maximumRevision)]);
            long total, anchor;
            DateTime? chartEnd;
            await using (var reader = await count.ExecuteReaderAsync(token)
            .ConfigureAwait(false))
            {
                await reader.ReadAsync(token)
            .ConfigureAwait(false);
                total = reader.GetInt64(0);
                anchor = maximumRevision ?? reader.GetInt64(1);
                chartEnd = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
            }
            await using var command = new NpgsqlCommand(TradePlanDbSql.OffsetHistoryRows(table), connection, transaction);
            PostgresValueBinding.AddParameters(command, [.. scope, anchor, offset, size]);
            var items = new List<StrategyTradePlanSnapshot>(size);
            await using (var reader = await command.ExecuteReaderAsync(token)
            .ConfigureAwait(false))
                while (await reader.ReadAsync(token)
            .ConfigureAwait(false))
                    items.Add(MessagePackBinarySerializer.Shared.Deserialize<StrategyTradePlanSnapshot>(reader.GetFieldValue<byte[]>(0)));
            var page = new StrategyTradePlanHistoryPage(items.ToArray(), null)
            {
                Offset = offset, TotalCount = total, MaximumRevision = anchor, ChartEndUtc = chartEnd
            };
            return new PostgresTradePlanOffsetReadSession(connection, transaction, table, scope, page);
        }
        catch
        {
            try { if (transaction is not null) await transaction.DisposeAsync()
            .ConfigureAwait(false); }
            finally { await connection.DisposeAsync()
            .ConfigureAwait(false); }
            throw;
        }
    }

    public async Task<StrategyTradePlanChartPoint[]> ReadChartAsync(DateTime startUtc, DateTime endUtc, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(TradePlanDbSql.OffsetHistoryChart(table), connection, transaction);
        PostgresValueBinding.AddParameters(command, [.. scope, page.MaximumRevision, startUtc, endUtc]);
        var samples = new List<StrategyTradePlanChartPoint>();
        await using var reader = await command.ExecuteReaderAsync(token)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(token)
            .ConfigureAwait(false))
            samples.Add(new(reader.GetDateTime(0), reader.GetDecimal(1)) { ForwardSpread = reader.IsDBNull(2) ? null : reader.GetDecimal(2) });
        return samples.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        try { await transaction.DisposeAsync()
            .ConfigureAwait(false); }
        finally { await connection.DisposeAsync()
            .ConfigureAwait(false); }
    }
}
