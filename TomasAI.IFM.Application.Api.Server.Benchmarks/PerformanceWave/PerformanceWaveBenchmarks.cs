using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using MessagePack;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using TomasAI.IFM.Application.Api.Server.Benchmarks.PerformanceWave.Baselines;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Projector;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Telemetry.Metrics;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.PerformanceWave;

[MemoryDiagnoser, ThreadingDiagnoser]
public class OriginWaveBenchmarks
{
    [Benchmark(OperationsPerInvoke = 4000)]
    public void BeforeConcurrent() => Parallel.For(0, 4, _ => Before());
    [Benchmark(OperationsPerInvoke = 4000)]
    public void AfterConcurrent() => Parallel.For(0, 4, _ => After());
    [Benchmark(Baseline = true, OperationsPerInvoke = 1000)]
    public long Before()
    {
        long total = 0;
        for (var n = 0; n < 1000; n++)
        {
            var value = new FuturesTickTradeDataChangedEvent { ReceivedOn = DateTime.UnixEpoch };
            BaselineMessageOrigins.Capture(value, TimeProvider.System);
            for (var hop = 0; hop < 4; hop++)
            {
                var headers = BaselineMessageOriginHeaders.Add(null, BaselineMessageOrigins.Get(value));
                value = BaselineMessageOrigins.Restore(value with { }, BaselineMessageOriginHeaders.Read(headers));
            }
            total += BaselineMessageOrigins.Get(value)!.OriginatedAtUtc.Ticks;
        }
        return total;
    }
    [Benchmark(OperationsPerInvoke = 1000)]
    public long After()
    {
        long total = 0;
        for (var n = 0; n < 1000; n++)
        {
            var value = new FuturesTickTradeDataChangedEvent { ReceivedOn = DateTime.UnixEpoch };
            MessageOrigins.Capture(value, TimeProvider.System);
            for (var hop = 0; hop < 4; hop++)
            {
                var headers = MessageOriginHeaders.Add(null, MessageOrigins.Get(value));
                value = MessageOrigins.Restore(value with { }, MessageOriginHeaders.Read(headers));
            }
            total += MessageOrigins.Get(value)!.OriginatedAtUtc.Ticks;
        }
        return total;
    }
}

[MemoryDiagnoser, ThreadingDiagnoser]
public class WalWaveBenchmarks
{
    string root = null!;
    BaselineFuturesItiInputWal before = null!;
    FuturesItiInputWal after = null!;
    readonly byte[] payload = new byte[512];
    [Params(1, 64)] public int Count { get; set; }
    [GlobalSetup]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "ifm-perf-wal-" + Guid.NewGuid().ToString("N"));
        before = new(Path.Combine(root, "before")); after = new(Path.Combine(root, "after"));
    }
    [Benchmark(Baseline = true)] public Task Before() => Task.WhenAll(Enumerable.Range(0, Count).Select(_ => before.AppendAsync(payload, default).AsTask()));
    [Benchmark] public Task After() => Task.WhenAll(Enumerable.Range(0, Count).Select(_ => after.AppendAsync(payload, default).AsTask()));
    [GlobalCleanup] public async Task Cleanup()
    {
        await before.DisposeAsync(); await after.DisposeAsync();
        if (!root.StartsWith(Path.Combine(Path.GetTempPath(), "ifm-perf-wal-"), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
        Directory.Delete(root, true);
    }
}

[MemoryDiagnoser]
public class PositionWaveBenchmarks
{
    public EstablishedTradeDefinition Trade { get; private set; } = null!;
    public StrategyPositionSnapshot[] History { get; private set; } = null!;
    [Params(40, 1000)] public int Count { get; set; }
    [GlobalSetup]
    public void Setup()
    {
        var legs = Enumerable.Range(0, 4).Select(n => new TradeLegDefinition { TradeLegId = Guid.NewGuid(), ContractId = "ES" + n,
            SignedQuantity = n % 2 == 0 ? -1 : 1, CashMultiplier = 50, Expiry = new(2026, 12, 18) }).ToArray();
        Trade = new() { Id = new(1, 2, 3, 4), StrategyKind = TradeStrategyKind.IronCondor, EstablishedAtUtc = new(2026, 11, 6, 15, 0, 0, DateTimeKind.Utc),
            OpeningCommission = 2.6m, Legs = legs, OriginalFills = legs.Select(leg => new ExecutionFillEvidence { TradeLegId = leg.TradeLegId,
                SignedQuantity = leg.SignedQuantity, Price = 10, FilledAtUtc = new(2026, 11, 6, 15, 0, 0, DateTimeKind.Utc) }).ToArray() };
        History = Enumerable.Range(0, Count).Select(n => new StrategyPositionSnapshot { Id = StrategyPositionId.Create(Trade.Id, Trade.StrategyKind),
            StrategyKind = Trade.StrategyKind, ValueDate = new(2026, 11, 6), AsOfUtc = Trade.EstablishedAtUtc.AddSeconds(n), PositionSequence = n,
            Phase = StrategyPositionPhase.MarkToMarket, ContractCashMultiplier = 50, OpeningCommission = 2.6m, OpeningValueDate = new(2026, 11, 6),
            UnrealizedPnl = n / 100m, DailyPnlPoints = n / 100m,
            Legs = legs.Select(leg => new StrategyPositionLeg { TradeLegId = leg.TradeLegId, ContractId = leg.ContractId,
                SignedQuantity = leg.SignedQuantity, CurrentPrice = 10 + n / 100m, CashMultiplier = 50 }).ToArray() }).ToArray();
    }
    [Benchmark(Baseline = true)] public StrategyPositionSnapshot[] Before() => BaselineStrategyPositionHistoryProjection.EnrichHistory(History, Trade);
    [Benchmark] public StrategyPositionSnapshot[] After() => StrategyPositionHistoryProjection.EnrichHistory(History, Trade);
}

public sealed record BinaryReadFixture(Guid Id, byte[] Payload);

[MemoryDiagnoser]
public class PostgresPayloadWaveBenchmarks
{
    IsolatedIntegrationInfrastructure infrastructure = null!;
    NpgsqlConnection connection = null!;
    public BinaryReadFixture Expected { get; private set; } = null!;
    [GlobalSetup]
    public async Task Setup()
    {
        infrastructure = new("payloadwave"); await infrastructure.StartAsync();
        connection = new(new NpgsqlConnectionStringBuilder(infrastructure.PostgresConnectionString)
            { Username = infrastructure.PostgresUser, Password = infrastructure.PostgresPassword }.ConnectionString); await connection.OpenAsync();
        var random = new byte[128_000]; Random.Shared.NextBytes(random); Expected = new(Guid.NewGuid(), random);
        await using (var schema = new NpgsqlCommand("CREATE TABLE payload_wave(id int PRIMARY KEY,payload bytea NOT NULL)", connection)) await schema.ExecuteNonQueryAsync();
        await using var command = new NpgsqlCommand("INSERT INTO payload_wave VALUES(1,$1)", connection);
        command.Parameters.AddWithValue(MessagePackBinarySerializer.Shared.Serialize(Expected)!); await command.ExecuteNonQueryAsync();
    }
    [Benchmark(Baseline = true)] public async Task<BinaryReadFixture?> Before()
    {
        await using var command = new NpgsqlCommand("SELECT payload FROM payload_wave WHERE id=1", connection);
        await using var reader = await command.ExecuteReaderAsync(); await reader.ReadAsync();
        return MessagePackBinarySerializer.Shared.Deserialize<BinaryReadFixture>(reader.GetFieldValue<byte[]>(0));
    }
    [Benchmark] public async Task<BinaryReadFixture?> After()
    {
        await using var command = new NpgsqlCommand("SELECT payload FROM payload_wave WHERE id=1", connection);
        await using var reader = await command.ExecuteReaderAsync(); await reader.ReadAsync();
        return PooledPostgresPayload.Deserialize<BinaryReadFixture>(reader, 0);
    }
    [GlobalCleanup] public async Task Cleanup() { if (connection is not null) await connection.DisposeAsync(); if (infrastructure is not null) await infrastructure.DisposeAsync(); }
}

[MemoryDiagnoser]
public class MetricWaveBenchmarks
{
    readonly string[] names = Enumerable.Range(0, 32).Select(n => "ifm.process.gc.wave." + n).ToArray();
    [Benchmark(Baseline = true)] public int Before() => Run(false);
    [Benchmark] public int After() => Run(true);
    public int Run(bool bounded)
    {
        using var meter = new Meter(ProcessGcStatisticsRecorder.MeterName);
        var counters = names.Select(name => meter.CreateCounter<long>(name)).ToArray();
        var exporter = new MetricCountExporter();
        var builder = Sdk.CreateMeterProviderBuilder().AddMeter(meter.Name);
        if (bounded) builder.AddIfmBoundedMetricViews();
        using var provider = builder.AddReader(new PeriodicExportingMetricReader(exporter, 600_000)).Build();
        foreach (var counter in counters) for (var n = 0; n < 3; n++) counter.Add(1, new KeyValuePair<string, object?>("generation", n));
        provider.ForceFlush();
        if (exporter.Sum != 96 || exporter.Points != 96) throw new InvalidDataException("Metric export changed.");
        return exporter.Points;
    }
    sealed class MetricCountExporter : BaseExporter<Metric>
    {
        public long Sum; public int Points;
        public override ExportResult Export(in Batch<Metric> batch)
        {
            Sum = 0; Points = 0;
            foreach (var metric in batch) foreach (ref readonly var point in metric.GetMetricPoints()) { Sum += point.GetSumLong(); Points++; }
            return ExportResult.Success;
        }
    }
}

[MemoryDiagnoser]
public class EodSpoolWaveBenchmarks
{
    string root = null!;
    BaselineEodHistorySpool before = null!; EodHistorySpool after = null!;
    [GlobalSetup] public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "ifm-perf-eod-" + Guid.NewGuid().ToString("N"));
        before = new(Path.Combine(root, "before")); after = new(Path.Combine(root, "after"));
    }
    internal static EodHistoryEntry Entry() => EodHistoryEntry.Create(new FuturesEodDataInsertedEvent { Id = Guid.NewGuid(),
        ReceivedOn = new(2026, 11, 23, 21, 0, 0, DateTimeKind.Utc) },
        new BufferedFuturesEodRow(new FuturesEodDataV2ReadModel { ContractId = "ES-perf", Symbol = "ES",
            ValueDate = new(2026, 11, 23), OpenPrice = 6000, HighPrice = 6010, LowPrice = 5995, ClosePrice = 6005 }, true)
            { HistorySequenceId = 123 }, null, 1);
    [Benchmark(Baseline = true)] public async Task<int> Before()
    {
        for (var n = 0; n < 16; n++) await before.AppendAsync(Entry());
        var batch = await before.ReadBatchAsync(16);
        foreach (var entry in batch) { entry.Row!.HistorySequenceId = 456; await BaselineEodHistorySpool.SaveAsync(entry); }
        foreach (var entry in batch) before.Acknowledge(entry); return batch.Count;
    }
    [Benchmark] public async Task<int> After()
    {
        for (var n = 0; n < 16; n++) await after.AppendAsync(Entry());
        var batch = await after.ReadBatchAsync(16);
        foreach (var entry in batch) entry.Row!.HistorySequenceId = 456;
        await EodHistorySpool.SaveBatchAsync(batch);
        foreach (var entry in batch) after.Acknowledge(entry); return batch.Count;
    }
    [GlobalCleanup] public void Cleanup()
    {
        before.Dispose(); after.Dispose();
        if (!root.StartsWith(Path.Combine(Path.GetTempPath(), "ifm-perf-eod-"), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
        Directory.Delete(root, true);
    }
}
