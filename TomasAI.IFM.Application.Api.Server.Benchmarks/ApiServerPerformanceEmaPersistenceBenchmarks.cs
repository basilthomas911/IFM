using BenchmarkDotNet.Attributes;
using Npgsql;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>Real PostgreSQL load, compute, event/projection transaction and stream resolution on both paths. Fixture generation is excluded.</summary>
[MemoryDiagnoser, ThreadingDiagnoser, Config(typeof(ApiServerPerformanceConfig)), InvocationCount(1)]
public class ApiServerPerformanceEmaPersistenceBenchmarks
{
    [Params(21, 48, 64)] public int Bars { get; set; }
    readonly ApiServerPerformanceDatabaseFixture database = new();
    SeedFuturesEmaSignalsCommand command = null!;
    GenerateFuturesEmaSignalCommand[] individual = null!;

    [GlobalSetup] public Task Setup() => database.InitializeAsync();

    [IterationSetup]
    public void Prepare()
    {
        database.ClearCallHistory();
        var contract = "ES-bench-" + Guid.NewGuid().ToString("N");
        var series = MarketSeriesIdentity.ForContract(contract);
        var id = new FuturesTradeSessionBarEntityId(series, TimeFrameType.OneMinute);
        var start = new DateTimeOffset(2026, 11, 6, 15, 0, 0, TimeSpan.Zero);
        command = new() { CommandId = Guid.NewGuid(), EntityId = id,
            Subject = new(ActorType.Command, SeedFuturesEmaSignalsCommand.Actor, SeedFuturesEmaSignalsCommand.Verb, id.Format()),
            Observations = Enumerable.Range(1, Bars).Select(index => new FuturesTradeSessionBarReadModel
            {
                MarketSeriesIdentity = series, ContractId = contract, ValueDate = new(2026, 11, 6), TimeFrame = TimeFrameType.OneMinute,
                ObservationId = FuturesTradeSessionBarId.Create(series, TimeFrameType.OneMinute, start.AddMinutes(index), index),
                IntervalStartUtc = start.AddMinutes(index - 1), IntervalEndUtc = start.AddMinutes(index),
                Open = 5000, High = 5002, Low = 4998, Close = 5000 + index / 100m, Volume = 1, TradeCount = 1, PriceVolumeSum = 5000 + index / 100m,
                FirstSourceSequence = index, LastSourceSequence = index, FirstMarketEventUtc = start.AddMinutes(index - 1).AddSeconds(1),
                LastMarketEventUtc = start.AddMinutes(index).AddMilliseconds(-1), CalculatedAtUtc = start.AddMinutes(index),
                IsComplete = true, IsValid = true, SchemaVersion = 1, CalculationVersion = "api-performance-v1",
                CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate
            }).ToArray() };
        individual = command.Observations.Select(bar => new GenerateFuturesEmaSignalCommand
        { CommandId = Guid.NewGuid(), EntityId = id, Observation = bar,
            Subject = new(ActorType.Command, GenerateFuturesEmaSignalCommand.Actor, GenerateFuturesEmaSignalCommand.Verb, id.Format()) }).ToArray();
    }

    [Benchmark(Baseline = true), BenchmarkCategory("17-EmaPostgresPersistence")]
    public async Task PerBarBefore()
    {
        foreach (var item in individual)
        {
            var state = await database.Ema.LoadStateAsync(item);
            if (!item.Execute(state).Success) throw new InvalidOperationException("EMA calculation rejected.");
            await database.Ema.SaveStateAsync(database.Context, state, item);
        }
    }

    [Benchmark, BenchmarkCategory("17-EmaPostgresPersistence")]
    public async Task AtomicBatchAfter()
    {
        var state = await database.Ema.LoadStateAsync(command);
        if (!command.Execute(state).Success) throw new InvalidOperationException("EMA batch rejected.");
        await database.Ema.SaveStateAsync(database.Context, state, command);
    }

    [IterationCleanup]
    public void VerifyPersistedRows() => VerifyAsync().GetAwaiter().GetResult(); // Benchmark lifecycle only; excluded from measured repository work.

    async Task VerifyAsync()
    {
        var state = await database.Ema.LoadStateAsync(command);
        if (state.FuturesEmaCheckpoint?.Count != Bars || state.CommittedStreamVersion != Bars)
            throw new InvalidOperationException("Latest EMA snapshot did not reconstruct the complete seed.");
        var stream = await database.Events.GetEventStreamIdAsync(command.StreamId, default);
        await using var connection = new NpgsqlConnection(database.AuthenticatedPostgres); await connection.OpenAsync();
        await using var events = new NpgsqlCommand("SELECT count(*) FROM event_log WHERE EventStreamId = $1", connection);
        events.Parameters.Add(new NpgsqlParameter { Value = stream });
        if ((long)(await events.ExecuteScalarAsync())! != Bars) throw new InvalidOperationException("EMA source event count differs.");
        await using var projection = new NpgsqlCommand("SELECT count(*) FROM market_data.futures_ema_signal WHERE contractId = $1", connection);
        projection.Parameters.Add(new NpgsqlParameter { Value = command.Observations[0].ContractId });
        if ((long)(await projection.ExecuteScalarAsync())! != Bars) throw new InvalidOperationException("EMA projected row count differs.");
    }

    [GlobalCleanup] public async Task Cleanup() => await database.DisposeAsync();
}
