using BenchmarkDotNet.Attributes;
using Cassandra;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb.Schema;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Measures actual Scylla raw quote writes and futures trade reads in the dedicated test keyspace.</summary>
[MemoryDiagnoser, ThreadingDiagnoser]
public class TradeTickQuoteContextConventionBenchmarks
{
    const string Keyspace = "ifm_dbcontext_benchmark";
    const int Port = 59042;
    const string Connection = "Contact Points=127.0.0.1;Port=59042;Default Keyspace=ifm_dbcontext_benchmark";
    static readonly DateOnly Date = new(2026, 11, 10);
    static readonly DateTime At = new(2026, 11, 10, 14, 0, 0, DateTimeKind.Utc);
    TradeTickQuoteDbContext database = null!;
    FuturesTickQuoteDataInsertedEvent quote = null!;
    string contract = null!;

    [Params(64, 512)] public int RowCount { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        if (Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_SCYLLA") != Connection)
            throw new InvalidOperationException("Only the dedicated disposable Scylla qualification keyspace is allowed.");
        var settings = new DbConnectionSettings().Add(TradeTickQuoteDbContext.TradeTickQuoteDbConnection,
            Connection, "System.Data.ScyllaDb");
        var logger = NullLogger<DbProvider>.Instance;
        await new TradeTickQuoteSchemaDb(settings, logger).CreateAllAsync().ConfigureAwait(false);
        database = new(settings, logger);
        contract = "ES-BENCH-" + Guid.NewGuid().ToString("N");
        await Parallel.ForEachAsync(Enumerable.Range(1, RowCount), new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (index, _) =>
        {
            var time = At.AddMilliseconds(index);
            var ns = (time.Ticks - DateTime.UnixEpoch.Ticks) * 100;
            await database.InsertTickTradeDataAsync(new FuturesTickTradeDataInsertedEvent
            {
                Id = Guid.NewGuid(), EventId = index, CommandId = Guid.NewGuid(), AggregateId = contract,
                EventSource = "qualification", ReceivedOn = time,
                TickDataId = new(contract, Date, index, time), AssetTypeId = AssetTypeId.Futures,
                Dataset = "GLBX.MDP3", DefinitionDate = Date, PublisherId = 1, InstrumentId = 42,
                TradeData = new((uint)index, ns, ns, 0, 5000000000000, 5000m, 1, (byte)'T', 0, 0)
            }).ConfigureAwait(false);
        }).ConfigureAwait(false);
        var quotes = Enumerable.Range(0, RowCount).Select(index => new FuturesTickQuoteData(
            (uint)index, 1234567890123456789 + index, 1234567890123456799 + index,
            (byte)(index % 8), 6000125000000 + index, 6000.125m + index,
            (uint)(100 + index), (uint)(10 + index), 6000250000000 + index, 6000.25m + index,
            (uint)(200 + index), (uint)(20 + index))).ToArray();
        quote = new()
        {
            Id = Guid.NewGuid(), EventId = 1, CommandId = Guid.NewGuid(), AggregateId = contract,
            EventSource = "qualification", ReceivedOn = At,
            TickDataId = new(contract, Date, 1, At), AssetTypeId = AssetTypeId.Futures,
            Dataset = "GLBX.MDP3", DefinitionDate = Date, PublisherId = 1, InstrumentId = 42,
            EmissionReason = QuoteEmissionReason.BufferFull, QuoteCount = (ushort)RowCount,
            QuoteData = new(quotes, (ushort)RowCount)
        };
        await WriteQuoteSegment().ConfigureAwait(false);
        using var cluster = Cluster.Builder().AddContactPoint("127.0.0.1").WithPort(Port).Build();
        using var session = cluster.Connect(Keyspace);
        using var saved = session.Execute(new SimpleStatement(
            "SELECT quote_count FROM tick_quote_data WHERE asset_type_id=? AND contract_id=? AND value_date=? AND aggregation_time=? AND sequence_id=?;",
            (sbyte)AssetTypeId.Futures, contract, new LocalDate(Date.Year, Date.Month, Date.Day),
            new LocalTime(At.TimeOfDay.Ticks * 100), 1L));
        if (saved.Single().GetValue<short>("quote_count") != RowCount || await ReadTradeWindow().ConfigureAwait(false) != RowCount)
            throw new InvalidOperationException("Scylla qualification fixture did not retain the exact row counts.");
    }

    [Benchmark] public Task WriteQuoteSegment() => database.InsertTickQuoteDataAsync(quote);

    [Benchmark] public async Task<int> ReadTradeWindow()
    {
        var rows = await database.GetRawFuturesTradeWindowAsync(contract, Date,
            TimeOnly.FromDateTime(At), TimeOnly.FromDateTime(At.AddSeconds(1))).ConfigureAwait(false);
        return rows.Count;
    }
}
