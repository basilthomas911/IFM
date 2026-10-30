using Npgsql;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Recovery;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

/// <summary>Operational subscription intent; no market ticks, orders or financial events are replayed.</summary>
public sealed class TradeFeedRecoveryStore(IDbConnectionSettings settings) : ITradeFeedRecoveryStore
{
    async Task<NpgsqlConnection> OpenAsync(CancellationToken token)
    {
        var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(
            new NpgsqlConnectionStringBuilder(settings[TradeDbContext.TradeDbConnection].ConnectionString)
            { CommandTimeout = 120 }.ConnectionString);
        try { await connection.OpenAsync(token); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }
    public async Task<TradeFeedRecoveryRegistration[]> ReadAsync(DateOnly date, CancellationToken token)
    {
        await using var connection = await OpenAsync(token);
        await using var command = new NpgsqlCommand("SELECT trade_id, array_agg(contract_id ORDER BY contract_id), max(updated_at) FROM trade.live_feed_recovery WHERE value_date=$1 GROUP BY trade_id ORDER BY trade_id", connection);
        PostgresValueBinding.AddParameter(command, date);
        await using var reader = await command.ExecuteReaderAsync(token);
        var rows = new List<TradeFeedRecoveryRegistration>();
        while (await reader.ReadAsync(token)) rows.Add(new(reader.GetString(0), date, reader.GetFieldValue<string[]>(1), reader.GetFieldValue<DateTimeOffset>(2)));
        return rows.ToArray();
    }
    public async Task SaveAsync(TradeFeedRecoveryRegistration registration, CancellationToken token)
    {
        _ = TradeEntityId.Parse(registration.TradeId);
        if (registration.ValueDate == default || registration.OptionContracts.Length == 0 || registration.OptionContracts.Any(string.IsNullOrWhiteSpace)
            || registration.OptionContracts.Distinct(StringComparer.Ordinal).Count() != registration.OptionContracts.Length)
            throw new ArgumentException("Recovery requires a value date and distinct option contracts.");
        await using var connection = await OpenAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1,0))", connection, transaction);
        PostgresValueBinding.AddParameter(command, registration.TradeId + ":" + registration.ValueDate.ToString("yyyy-MM-dd"));
        await command.ExecuteNonQueryAsync(token);
        command.CommandText = "DELETE FROM trade.live_feed_recovery WHERE trade_id=$1 AND value_date=$2";
        command.Parameters.Clear(); PostgresValueBinding.AddParameter(command, registration.TradeId); PostgresValueBinding.AddParameter(command, registration.ValueDate);
        await command.ExecuteNonQueryAsync(token);
        command.CommandText = "INSERT INTO trade.live_feed_recovery (trade_id,value_date,contract_id,updated_at) SELECT $1,$2,unnest($3::text[]),$4";
        PostgresValueBinding.AddParameter(command, registration.OptionContracts); PostgresValueBinding.AddParameter(command, registration.UpdatedAtUtc);
        await command.ExecuteNonQueryAsync(token);
        await transaction.CommitAsync(token);
    }
    public async Task RemoveAsync(string tradeId, DateOnly date, CancellationToken token)
    {
        _ = TradeEntityId.Parse(tradeId);
        await using var connection = await OpenAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1,0))", connection, transaction);
        PostgresValueBinding.AddParameter(command, tradeId + ":" + date.ToString("yyyy-MM-dd")); await command.ExecuteNonQueryAsync(token);
        command.CommandText = "DELETE FROM trade.live_feed_recovery WHERE trade_id=$1 AND value_date=$2";
        command.Parameters.Clear(); PostgresValueBinding.AddParameter(command, tradeId); PostgresValueBinding.AddParameter(command, date);
        await command.ExecuteNonQueryAsync(token); await transaction.CommitAsync(token);
    }
    public async Task CloseAsync(DateOnly date, CancellationToken token)
    {
        await using var connection = await OpenAsync(token);
        await using var command = new NpgsqlCommand("DELETE FROM trade.live_feed_recovery WHERE value_date <= $1", connection);
        PostgresValueBinding.AddParameter(command, date); await command.ExecuteNonQueryAsync(token);
    }
}
