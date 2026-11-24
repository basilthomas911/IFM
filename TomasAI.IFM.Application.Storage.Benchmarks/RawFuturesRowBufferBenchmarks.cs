using BenchmarkDotNet.Attributes;
using Cassandra;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb.Schema;
using TomasAI.IFM.Testing;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Production writer, including original event decoding, metadata binding and pooled quote CQL ownership.</summary>
[MemoryDiagnoser]
public class RawFuturesRowBufferBenchmarks
{
    ICluster cluster = null!; ISession session = null!; RawFuturesRowBatchStore writer = null!;
    string keyspace = null!; long next;
    [Params(32, 64)] public int Count { get; set; }
    [Params(false, true)] public bool MixedQuotes { get; set; }
    [GlobalSetup]
    public async Task Setup()
    {
        cluster = Cluster.Builder().AddContactPoint("localhost").Build(); session = await cluster.ConnectAsync();
        keyspace = "ifm_raw_bdn_" + Guid.NewGuid().ToString("N");
        using (await session.ExecuteAsync(new SimpleStatement($"CREATE KEYSPACE {keyspace} WITH replication={{'class':'SimpleStrategy','replication_factor':1}};"))) { }
        session.ChangeKeyspace(keyspace);
        foreach (var sql in new[] { TradeTickQuoteSchemaSql.CreateTickQuoteItemType, TradeTickQuoteSchemaSql.CreateTickTradeDataTable, TradeTickQuoteSchemaSql.CreateTickQuoteDataTable })
        { using var result = await session.ExecuteAsync(new SimpleStatement(sql)); }
        writer = await RawFuturesRowBatchStore.PrepareAsync(session);
        await GroupedRows();
    }
    [Benchmark(Baseline = true)]
    public Task IndividualRequests() => writer.WriteIndividuallyAsync(RawFuturesJournalFixture.Batch(Count, MixedQuotes, Interlocked.Add(ref next, Count), "ES-bdn-rows"));
    [Benchmark]
    public Task GroupedRows() => writer.WriteAsync(RawFuturesJournalFixture.Batch(Count, MixedQuotes, Interlocked.Add(ref next, Count), "ES-bdn-rows"));
    [GlobalCleanup]
    public async Task Cleanup()
    {
        if (session is not null && keyspace.StartsWith("ifm_raw_bdn_", StringComparison.Ordinal))
        { using var result = await session.ExecuteAsync(new SimpleStatement($"DROP KEYSPACE {keyspace};")); }
        session?.Dispose(); cluster?.Dispose();
    }
}
