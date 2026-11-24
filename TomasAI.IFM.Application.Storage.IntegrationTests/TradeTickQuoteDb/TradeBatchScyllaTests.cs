using Cassandra;
using Cassandra.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb.Schema;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.ScyllaDb;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeTickQuoteDb;

public sealed class TradeBatchScyllaTests
{
    [Fact]
    public async Task Additive_schema_exact_ranges_late_arrivals_retry_conflicts_and_restart()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("tradebuffer");
        await infrastructure.StartAsync();
        var settings = new DbConnectionSettings().Add(TradeTickQuoteDbContext.TradeTickQuoteDbConnection,
            infrastructure.CqlConnectionString, "System.Data.ScyllaDb");
        var schema = new TradeTickQuoteSchemaDb(settings, NullLogger<DbProvider>.Instance);
        await schema.CreateAllAsync();
        using var cluster = Cluster.Builder().AddContactPoint("localhost").WithPort(infrastructure.CqlPort)
            .WithTypeSerializers(new TypeSerializerDefinitions().Define(new ScyllaRawTradeUdtSerializer())).Build();
        using var session = await cluster.ConnectAsync(infrastructure.CqlKeyspace);
        Assert.StartsWith("ifm_tradebuffer_", session.Keyspace, StringComparison.Ordinal);
        using (await session.ExecuteAsync(new SimpleStatement("""
            INSERT INTO tick_trade_data(asset_type_id,contract_id,value_date,aggregation_time,sequence_id,price)
            VALUES (1,'ES-legacy',?,?,1,6000.25);
            """, new LocalDate(2026, 11, 23), new LocalTime(0)))) { }
        // Recreate the pre-upgrade state in this disposable keyspace, retaining a populated legacy table.
        foreach (var sql in new[] { "DROP TABLE tick_trade_batch_data_v1;", "DROP TABLE tick_trade_batch_manifest_v1;", "DROP TYPE tick_trade_item_v1;" })
        { using var result = await session.ExecuteAsync(new SimpleStatement(sql)); }
        await schema.CreateAllAsync();
        using (var preserved = await session.ExecuteAsync(new SimpleStatement("SELECT price FROM tick_trade_data WHERE asset_type_id=1 AND contract_id='ES-legacy';")))
            Assert.Equal(6000.25m, preserved.Single().GetValue<decimal>("price"));
        var store = await TradeBatchScyllaStore.PrepareAsync(session);
        var date = new DateOnly(2026, 11, 23);
        var at = new DateTime(2026, 11, 23, 20, 59, 59, DateTimeKind.Utc);
        var origin = checked((at.Ticks - DateTime.UnixEpoch.Ticks) * 100);
        TradeStorageRecord Record(int index, long offset) => new(new((uint)index, origin + offset,
            origin + offset + 7, 4, 6_000_250_000_000L, 6000.25m, (uint)(index + 1), (byte)'T', (byte)'B', 8),
            index, at.Ticks, Guid.NewGuid(), Guid.NewGuid(), index, "source", "test-\u03bb", at.Ticks, index);
        var records = new[] { Record(1, 800_000_000), Record(2, 900_000_003), Record(3, 900_000_001),
            Record(4, 1_000_000_000), Record(5, 1_200_000_000), Record(6, 1_200_000_000) };
        var batch = new TradeStorageBatch(1, Guid.NewGuid(), AssetTypeId.Futures, "ES-exact", date,
            "GLBX.MDP3", date, 1, 42, Guid.NewGuid(), new(records, records.Length));
        await store.WriteAsync(batch);
        await schema.CreateAllAsync(); // populated upgrade/reapplication
        var expected = records.OrderBy(row => row.Trade.EventTimestampNanoseconds).ThenBy(row => row.AcceptanceOrdinal).ToArray();
        Assert.Equal(expected, await store.ReadAsync(batch.AssetType, batch.ContractId, date, origin, origin + 2_000_000_000));
        Assert.Equal(expected.Where(row => row.Trade.EventTimestampNanoseconds >= origin + 900_000_001
            && row.Trade.EventTimestampNanoseconds < origin + 1_200_000_000),
            await store.ReadAsync(batch.AssetType, batch.ContractId, date, origin + 900_000_001, origin + 1_200_000_000));
        Assert.Empty(await store.ReadAsync(batch.AssetType, batch.ContractId, date, origin, origin));
        await store.WriteAsync(batch);
        Assert.Equal(expected, await store.ReadAsync(batch.AssetType, batch.ContractId, date, origin, origin + 2_000_000_000));
        var altered = records.ToArray(); altered[0] = altered[0] with { Trade = altered[0].Trade with { Price = 6001m } };
        await Assert.ThrowsAsync<InvalidDataException>(() => store.WriteAsync(batch with { Records = new(altered, altered.Length) }));
        // Simulate a crash after the manifest and first second were committed.
        using (await session.ExecuteAsync(new SimpleStatement(
            "DELETE FROM tick_trade_batch_data_v1 WHERE asset_type_id=? AND contract_id=? AND value_date=? AND utc_hour=? AND event_second=?;",
            (sbyte)batch.AssetType, batch.ContractId, new LocalDate(2026, 11, 23),
            checked((int)((origin / 1_000_000_000 + 1) / 3600)), origin / 1_000_000_000 + 1))) { }
        store = await TradeBatchScyllaStore.PrepareAsync(session); // fresh reader/writer, no local state
        await store.WriteAsync(batch);
        Assert.Equal(expected, await store.ReadAsync(batch.AssetType, batch.ContractId, date, origin, origin + 2_000_000_000));
        var late = Record(7, 850_000_000);
        await store.WriteAsync(batch with { BatchId = Guid.NewGuid(), Records = new([late], 1) });
        Assert.Equal(expected.Append(late).OrderBy(row => row.Trade.EventTimestampNanoseconds).ThenBy(row => row.AcceptanceOrdinal),
            await store.ReadAsync(batch.AssetType, batch.ContractId, date, origin, origin + 2_000_000_000));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.WriteAsync(batch, cancelled.Token));
    }
}
