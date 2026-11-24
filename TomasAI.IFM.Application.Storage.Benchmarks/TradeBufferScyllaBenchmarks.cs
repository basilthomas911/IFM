using BenchmarkDotNet.Attributes;
using Cassandra;
using Cassandra.Serialization;
using TomasAI.IFM.Framework.Storage.ScyllaDb;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb.Schema;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Paired raw-storage measurements; one invocation writes or reads 64 source trades.</summary>
[MemoryDiagnoser]
public class TradeBufferScyllaBenchmarks
{
    ICluster cluster = null!;
    ISession session = null!;
    PreparedStatement insert = null!, read = null!, fullRead = null!;
    string keyspace = null!;
    long sequence;
    TradeBatchScyllaStore store = null!;
    TradeStorageBatch batch = null!;
    readonly LocalDate date = new(2026, 11, 23);
    static readonly DateTime At = new(2026, 11, 23, 20, 59, 59, DateTimeKind.Utc);
    readonly LocalTime time = new(At.TimeOfDay.Ticks * 100);
    const int Count = 64;

    [GlobalSetup]
    public async Task Setup()
    {
        cluster = Cluster.Builder().AddContactPoint("localhost")
            .WithTypeSerializers(new TypeSerializerDefinitions().Define(new ScyllaRawTradeUdtSerializer())).Build();
        session = await cluster.ConnectAsync().ConfigureAwait(false);
        keyspace = "ifm_trade_buffer_bdn_" + Guid.NewGuid().ToString("N")[..24];
        using (await session.ExecuteAsync(new SimpleStatement($"CREATE KEYSPACE {keyspace} WITH replication = {{'class':'SimpleStrategy','replication_factor':1}};")).ConfigureAwait(false)) { }
        session.ChangeKeyspace(keyspace);
        using (await session.ExecuteAsync(new SimpleStatement(TradeTickQuoteSchemaSql.CreateTickTradeDataTable)).ConfigureAwait(false)) { }
        foreach (var sql in new[] { TradeBatchScyllaSchema.CreateItem, TradeBatchScyllaSchema.CreateManifest, TradeBatchScyllaSchema.CreateChunks })
        { using var result = await session.ExecuteAsync(new SimpleStatement(sql)).ConfigureAwait(false); }
        insert = await session.PrepareAsync(TradeTickQuoteDbSql.InsertTickTradeData).ConfigureAwait(false);
        read = await session.PrepareAsync("SELECT source_sequence,source_event_timestamp_ns,source_receive_timestamp_ns,price,size FROM tick_trade_data WHERE asset_type_id=? AND contract_id=? AND value_date=? AND aggregation_time>=? AND aggregation_time<=?;").ConfigureAwait(false);
        fullRead = await session.PrepareAsync("SELECT * FROM tick_trade_data WHERE asset_type_id=? AND contract_id=? AND value_date=? AND aggregation_time>=? AND aggregation_time<=?;").ConfigureAwait(false);
        for (var index = 0; index < Count; index++)
        { using var rows = await session.ExecuteAsync(insert.Bind(Values("ES-READ", index))).ConfigureAwait(false); }
        if (await LegacyRangeRead().ConfigureAwait(false) != Count) throw new InvalidOperationException("Raw trade fixture did not round trip.");
        store = await TradeBatchScyllaStore.PrepareAsync(session).ConfigureAwait(false);
        var records = Enumerable.Range(0, Count).Select(index => new TradeStorageRecord(
            new((uint)index, 1_795_467_599_000_000_000L + index, 1_795_467_599_000_001_000L + index,
                0, 6_000_000_000_000L, 6000m, 1, (byte)'T', (byte)'B', 0), index, At.Ticks,
            Guid.NewGuid(), Guid.NewGuid(), index, "source", "benchmark", At.Ticks, index)).ToArray();
        batch = new(1, Guid.NewGuid(), AssetTypeId.Futures, "ES-BATCH-READ", new(2026, 11, 23), "GLBX.MDP3",
            new(2026, 11, 23), 1, 42, Guid.NewGuid(), new(records, Count));
        await store.WriteAsync(batch).ConfigureAwait(false);
        var decoded = await store.ReadAsync(batch.AssetType, batch.ContractId, batch.ValueDate,
            1_795_467_599_000_000_000L, 1_795_467_599_000_000_064L).ConfigureAwait(false);
        if (!records.SequenceEqual(decoded)) throw new InvalidOperationException("Trade chunk fields did not round trip.");
    }

    object[] Values(string contract, long ordinal) =>
    [
        (sbyte)1, contract, date, time, ordinal, At, At.Ticks, (short)1, "GLBX.MDP3", date,
        1, 42L, Guid.Empty, 0L, Guid.Empty, contract, "benchmark", At, ordinal,
        1_795_467_599_000_000_000L + ordinal, 1_795_467_599_000_001_000L + ordinal,
        (short)0, 6_000_000_000_000L, 6000m, 1L, (short)'T', (short)'B', (short)0
    ];

    [Benchmark]
    public async Task LegacyWrites()
    {
        for (var index = 0; index < Count; index++)
        { using var rows = await session.ExecuteAsync(insert.Bind(Values("ES-WRITE", Interlocked.Increment(ref sequence)))).ConfigureAwait(false); }
    }

    [Benchmark]
    public async Task<int> LegacyRangeRead()
    {
        using var rows = await session.ExecuteAsync(read.Bind((sbyte)1, "ES-READ", date, time, time)).ConfigureAwait(false);
        var count = 0;
        foreach (var row in rows)
        {
            _ = row.GetValue<long>("source_sequence");
            _ = row.GetValue<long>("source_event_timestamp_ns");
            _ = row.GetValue<long>("source_receive_timestamp_ns");
            _ = row.GetValue<decimal>("price");
            _ = row.GetValue<long>("size");
            count++;
        }
        return count;
    }

    [Benchmark]
    public Task ArrayWrites() => store.WriteAsync(batch with { BatchId = Guid.NewGuid(), ContractId = "ES-BATCH-WRITE" });

    [Benchmark]
    public async Task IndividualRowBatchWrites()
    {
        var statements = new BatchStatement().SetBatchType(BatchType.Unlogged);
        for (var index = 0; index < Count; index++)
            statements.Add(insert.Bind(Values("ES-ROW-BATCH-WRITE", Interlocked.Increment(ref sequence))));
        using var result = await session.ExecuteAsync(statements).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task<int> ArrayRangeRead() => (await store.ReadAsync(batch.AssetType, batch.ContractId, batch.ValueDate,
        1_795_467_599_000_000_000L, 1_795_467_599_000_000_064L).ConfigureAwait(false)).Count;

    [Benchmark]
    public async Task<int> LegacyFullRecordRead()
    {
        using var rows = await session.ExecuteAsync(fullRead.Bind((sbyte)1, "ES-READ", date, time, time)).ConfigureAwait(false);
        var records = new List<TradeStorageRecord>(Count);
        foreach (var row in rows)
            records.Add(new(new(checked((uint)row.GetValue<long>("source_sequence")), row.GetValue<long>("source_event_timestamp_ns"),
                row.GetValue<long>("source_receive_timestamp_ns"), checked((byte)row.GetValue<short>("header_flags")),
                row.GetValue<long>("price_raw"), row.GetValue<decimal>("price"), checked((uint)row.GetValue<long>("size")),
                checked((byte)row.GetValue<short>("action")), checked((byte)row.GetValue<short>("side")), checked((byte)row.GetValue<short>("dbn_flags"))),
                row.GetValue<long>("sequence_id"), row.GetValue<long>("aggregation_timestamp_utc_ticks"), row.GetValue<Guid>("actor_event_id"),
                row.GetValue<Guid>("command_id"), row.GetValue<long>("actor_event_log_id"), row.GetValue<string>("aggregate_id"),
                row.GetValue<string>("event_source"), row.GetValue<DateTimeOffset>("received_on").UtcTicks, row.GetValue<long>("sequence_id")));
        return records.Count;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        if (session is not null && keyspace?.StartsWith("ifm_trade_buffer_bdn_", StringComparison.Ordinal) == true)
        { using var rows = await session.ExecuteAsync(new SimpleStatement($"DROP KEYSPACE {keyspace};")).ConfigureAwait(false); }
        session?.Dispose();
        cluster?.Dispose();
    }
}
