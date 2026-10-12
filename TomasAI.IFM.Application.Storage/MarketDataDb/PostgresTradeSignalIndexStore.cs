using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using Npgsql;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Rebuilds trade-signal indexes and quarantines invalid identities in one explicit maintenance transaction.</summary>
public static class PostgresTradeSignalIndexStore
{
    /// <summary>Reads canonical rows without CQL JSON, preserves them, and replaces only the derived lookup entries.</summary>
    public static async Task<FuturesTradeSignalRepairReadModel> RebuildAsync(IEnlistedPostgresTransaction transaction,CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        await transaction.ExecuteAsync("LOCK TABLE market_data.futures_trade_signal,market_data.futures_trade_signal_lookup_by_scope,market_data.futures_trade_signal_quarantine IN SHARE ROW EXCLUSIVE MODE;",[],token)
            .ConfigureAwait(false);
        var payloads = await transaction.QueryAsync("SELECT row_to_json(t)::text FROM market_data.futures_trade_signal t ORDER BY sequenceId,contractId,valueDate,timePeriod;",[],reader => reader.GetString(0),token)
            .ConfigureAwait(false);
        var latest = new Dictionary<string,FuturesTradeSignalRepairRow>(StringComparer.Ordinal);
        var daily = new Dictionary<(string,DateOnly,string),FuturesTradeSignalRepairRow>();
        long valid = 0,quarantined = 0;
        foreach (var payload in payloads)
        {
            var parsed = MarketDataDbContextExtensions.ParseFuturesTradeSignalRepairRow(payload);
            if (parsed.Row is not { } row)
            {
                quarantined++;
                var values = new InsertFuturesTradeSignalQuarantine(MarketDataDbContextExtensions.Fingerprint(payload),payload,parsed.Error ?? "Malformed identity",DateTime.UtcNow) { PostgreSql = true };
                await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesTradeSignalQuarantine,(NpgsqlParameter[])values.Bind(),token)
            .ConfigureAwait(false);
                continue;
            }
            valid++;
            if (!latest.TryGetValue(row.TimePeriod,out var current) || MarketDataDbContextExtensions.IsNewer(row,current)) latest[row.TimePeriod] = row;
            var key = (row.TimePeriod,row.ValueDate,row.ContractId);
            if (!daily.TryGetValue(key,out var dated) || MarketDataDbContextExtensions.IsNewer(row,dated)) daily[key] = row;
        }
        await transaction.ExecuteAsync("DELETE FROM market_data.futures_trade_signal_lookup_by_scope;",[],token)
            .ConfigureAwait(false);
        var indexes = latest.Values.Select(row => new InsertFuturesTradeSignalIndex($"latest:{row.TimePeriod}","latest",row.SequenceId,row.ContractId,row.ValueDate,row.TimePeriod) { PostgreSql = true })
            .Concat(daily.Values.Select(row => new InsertFuturesTradeSignalIndex($"date:{row.TimePeriod}:{row.ValueDate.DayNumber}",row.ContractId,row.SequenceId,row.ContractId,row.ValueDate,row.TimePeriod) { PostgreSql = true })).ToArray();
        foreach (var index in indexes)
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesTradeSignalIndex,(NpgsqlParameter[])index.Bind(),token)
            .ConfigureAwait(false);
        return new(payloads.Count,valid,quarantined,indexes.Length);
    }
}
