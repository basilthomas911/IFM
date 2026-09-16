using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Cassandra;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Measures a prepared native-CQL quote write to an isolated Scylla test keyspace.</summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class TickQuoteScyllaWriteBenchmarks
{
    private ICluster _cluster = null!;
    private ISession _session = null!;
    private PreparedStatement _statement = null!;
    private PreparedStatement _readStatement = null!;
    private FuturesTickQuoteDataSegment _segment;
    private string _contractId = null!;
    private long _sequenceId;
    private readonly DateOnly _valueDate = new(2026, 8, 7);
    private readonly DateTime _timestamp = new(2026, 8, 7, 20, 15, 30, DateTimeKind.Utc);

    [Params(64, 96, 112, 120, 124, 128, 512, 4096)]
    public int QuoteCount { get; set; }

    [GlobalSetup]
    /// <summary>Connects to the test keyspace and prepares the quote insert.</summary>
    public void Setup()
    {
        var quotes = new FuturesTickQuoteData[QuoteCount];
        for (var index = 0; index < quotes.Length; index++)
            quotes[index] = new FuturesTickQuoteData(
                (uint)index, 1_000_000_000L + index, 2_000_000_000L + index,
                (byte)(index % 8), 50_000_000_000L + index,
                index % 2 == 0 ? 5.25m + index / 100m : null,
                (uint)(100 + index), (uint)(10 + index), 51_000_000_000L + index,
                index % 2 == 0 ? null : 5.35m + index / 100m,
                (uint)(200 + index), (uint)(20 + index));
        _segment = new FuturesTickQuoteDataSegment(quotes, (ushort)quotes.Length);
        _contractId = "ES-BDN-QUOTES-" + Guid.NewGuid().ToString("N");
        _cluster = Cluster.Builder().AddContactPoint("localhost").Build();
        _session = _cluster.Connect("market_data_test_db");
        _statement = _session.Prepare(MarketDataDbCql.InsertTickQuoteData);
        _readStatement = _session.Prepare(
            "SELECT quote_count, quote_data FROM tick_quote_data WHERE asset_type_id=? AND contract_id=? AND value_date=? AND aggregation_time=? AND sequence_id=?;");
        var initial = BuildValues(0);
        using var initialOwner = initial.Owner;
        using var initialRow = _session.Execute(_statement.Bind(initial.Values));
        using var readback = _session.Execute(_readStatement.Bind(
            (sbyte)AssetTypeId.Futures, _contractId,
            new LocalDate(_valueDate.Year, _valueDate.Month, _valueDate.Day),
            new LocalTime(_timestamp.TimeOfDay.Ticks * 100), 0L));
        var row = readback.Single();
        if (row.GetValue<short>("quote_count") != QuoteCount
            || row.GetValue<object>("quote_data") is null)
            throw new InvalidOperationException("Prepared quote benchmark row failed its readback.");
    }

    [GlobalCleanup]
    /// <summary>Closes the Scylla test session and cluster.</summary>
    public void Cleanup()
    {
        _session?.Dispose();
        _cluster?.Dispose();
    }

    [Benchmark]
    /// <summary>Uses the retained CQL buffer, then executes a prepared Scylla write.</summary>
    public Task PooledCqlWrite() => WriteAsync();

    [Benchmark]
    /// <summary>Reads and decodes one stored native UDT-list row of the configured size.</summary>
    public async Task<object> NativeListRead()
    {
        using var rows = await _session.ExecuteAsync(_readStatement.Bind(
            (sbyte)AssetTypeId.Futures, _contractId,
            new LocalDate(_valueDate.Year, _valueDate.Month, _valueDate.Day),
            new LocalTime(_timestamp.TimeOfDay.Ticks * 100), 0L)).ConfigureAwait(false);
        return rows.Single().GetValue<object>("quote_data");
    }

    private async Task WriteAsync()
    {
        var bound = BuildValues(Interlocked.Increment(ref _sequenceId));
        using var owner = bound.Owner;
        using var rowSet = await _session.ExecuteAsync(
            _statement.Bind(bound.Values)).ConfigureAwait(false);
    }

    private (object?[] Values, TickQuoteEncodedStorageCollection Owner) BuildValues(long sequenceId)
    {
        var owner = new TickQuoteEncodedStorageCollection(_segment);
        var quoteValue = owner.Resolve(_session, _statement);
        object?[] values =
        [
            (sbyte)AssetTypeId.Futures, _contractId, new LocalDate(_valueDate.Year, _valueDate.Month, _valueDate.Day),
            new LocalTime(_timestamp.TimeOfDay.Ticks * 100), sequenceId,
            _timestamp, _timestamp.Ticks, (short)1, "GLBX.MDP3",
            new LocalDate(_valueDate.Year, _valueDate.Month, _valueDate.Day),
            1, 42L, Guid.Empty, 0L, Guid.Empty, _contractId,
            "benchmark", _timestamp, (short)QuoteEmissionReason.BufferFull,
            (short)QuoteCount, quoteValue
        ];
        return (values, owner);
    }
}
