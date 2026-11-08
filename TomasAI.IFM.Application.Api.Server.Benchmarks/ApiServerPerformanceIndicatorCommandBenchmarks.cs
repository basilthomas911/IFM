using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>Five real indicator command handlers and PostgreSQL repositories, with stream resolution, ordered per-aggregate writes and identical source bars. Excludes NATS/mailbox overhead.</summary>
[MemoryDiagnoser, ThreadingDiagnoser, Config(typeof(ApiServerPerformanceConfig)), InvocationCount(1)]
public class ApiServerPerformanceIndicatorCommandBenchmarks
{
    readonly ApiServerPerformanceDatabaseFixture database = new();
    FuturesRsiSignalStateRepository rsi = null!;
    FuturesAdxSignalStateRepository adx = null!;
    FuturesAtrSignalStateRepository atr = null!;
    FuturesMacdSignalStateRepository macd = null!;
    Func<Task>[] work = null!;
    string contract = null!;
    readonly DateOnly date = new(2026, 11, 6);

    [GlobalSetup]
    public async Task Setup()
    {
        await database.InitializeAsync();
        rsi = database.CreateIndicatorRepository<FuturesRsiSignalStateRepository, FuturesRsiSignalCommandState, FuturesRsiSignalCommandActor>();
        adx = database.CreateIndicatorRepository<FuturesAdxSignalStateRepository, FuturesAdxSignalCommandState, FuturesAdxSignalCommandActor>();
        atr = database.CreateIndicatorRepository<FuturesAtrSignalStateRepository, FuturesAtrSignalCommandState, FuturesAtrSignalCommandActor>();
        macd = database.CreateIndicatorRepository<FuturesMacdSignalStateRepository, FuturesMacdSignalCommandState, FuturesMacdSignalCommandActor>();
    }

    [IterationSetup]
    public void Prepare()
    {
        database.ClearCallHistory();
        contract = "ES-init-" + Guid.NewGuid().ToString("N");
        var series = MarketSeriesIdentity.ForContract(contract);
        var start = new DateTimeOffset(2026, 11, 6, 15, 0, 0, TimeSpan.Zero);
        var bars = Enumerable.Range(1, 48).Select(index => new FuturesTradeSessionBarReadModel
        {
            MarketSeriesIdentity = series, ContractId = contract, ValueDate = date, TimeFrame = TimeFrameType.OneMinute,
            ObservationId = FuturesTradeSessionBarId.Create(series, TimeFrameType.OneMinute, start.AddMinutes(index), index),
            IntervalStartUtc = start.AddMinutes(index - 1), IntervalEndUtc = start.AddMinutes(index),
            Open = 5000, High = 5002, Low = 4998, Close = 5000 + index / 100m, Volume = 1, TradeCount = 1, PriceVolumeSum = 5000 + index / 100m,
            FirstSourceSequence = index, LastSourceSequence = index, FirstMarketEventUtc = start.AddMinutes(index - 1).AddSeconds(1),
            LastMarketEventUtc = start.AddMinutes(index).AddMilliseconds(-1), CalculatedAtUtc = start.AddMinutes(index),
            IsComplete = true, IsValid = true, SchemaVersion = 1, CalculationVersion = "api-performance-v1", CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate
        }).ToArray();
        var commands = new List<Func<Task>>();
        foreach (var period in new[] { 13, 14 })
        {
            var id = new FuturesRsiSignalEntityId(contract, date, TimeFrameType.OneMinute, period);
            var command = new StartFuturesRsiSignalCommand(id) { CommandId = Guid.NewGuid(),
                Subject = new(ActorType.Command, StartFuturesRsiSignalCommand.Actor, StartFuturesRsiSignalCommand.Verb, id.Format()),
                HistoricalSeed = new(48, bars[^1].IntervalEndUtc, bars, "RSI.STORED_TRADES_SEED"), ForceHistoricalInitialization = true };
            commands.Add(async () => { var state = await rsi.LoadStateAsync(command); Require(command.Execute(state, new CmeFuturesMarketSessionCalendar())); await rsi.SaveStateAsync(database.Context, state, command); });
        }
        var adxId = new FuturesAdxSignalEntityId(contract, date, TimeFrameType.OneMinute, 14);
        var adxCommand = new StartFuturesAdxSignalCommand(adxId) { CommandId = Guid.NewGuid(), HistoricalSeed = bars,
            Subject = new(ActorType.Command, StartFuturesAdxSignalCommand.Actor, StartFuturesAdxSignalCommand.Verb, adxId.Format()) };
        commands.Add(async () => { var state = await adx.LoadStateAsync(adxCommand); Require(adxCommand.Execute(state).Success); await adx.SaveStateAsync(database.Context, state, adxCommand); });
        var atrId = new FuturesAtrSignalEntityId(contract, date, TimeFrameType.OneMinute, 14);
        var atrCommand = new StartFuturesAtrSignalCommand(atrId) { CommandId = Guid.NewGuid(), HistoricalSeed = bars,
            Subject = new(ActorType.Command, StartFuturesAtrSignalCommand.Actor, StartFuturesAtrSignalCommand.Verb, atrId.Format()) };
        commands.Add(async () => { var state = await atr.LoadStateAsync(atrCommand); Require(atrCommand.Execute(state).Success); await atr.SaveStateAsync(database.Context, state, atrCommand); });
        var macdId = new FuturesMacdSignalEntityId(contract, date, TimeFrameType.OneMinute);
        var macdCommand = new StartFuturesMacdSignalCommand(macdId) { CommandId = Guid.NewGuid(), HistoricalSeed = bars,
            Subject = new(ActorType.Command, StartFuturesMacdSignalCommand.Actor, StartFuturesMacdSignalCommand.Verb, macdId.Format()) };
        commands.Add(async () => { var state = await macd.LoadStateAsync(macdCommand); Require(macdCommand.Execute(state).Success); await macd.SaveStateAsync(database.Context, state, macdCommand); });
        work = commands.ToArray();
    }

    [Benchmark(Baseline = true), BenchmarkCategory("16-PostgresIndependentIndicatorCommands")]
    public async Task SerialBefore() { foreach (var initialize in work) await initialize(); }

    [Benchmark, BenchmarkCategory("16-PostgresIndependentIndicatorCommands")]
    public async Task BoundedAfter()
    {
        using var gate = new SemaphoreSlim(2, 2);
        await Task.WhenAll(work.Select(async initialize =>
        {
            await gate.WaitAsync();
            try { await initialize(); } finally { gate.Release(); }
        }));
    }

    [IterationCleanup] public void VerifyWarmModels() => VerifyAsync().GetAwaiter().GetResult();
    async Task VerifyAsync()
    {
        var db = database.Market;
        Require((await db.GetLastFuturesRsiSignalAsync(contract, date, TimeFrameType.OneMinute, 13, default)) is { IsWarm: true });
        Require((await db.GetLastFuturesRsiSignalAsync(contract, date, TimeFrameType.OneMinute, 14, default)) is { IsWarm: true });
        Require((await db.GetLastFuturesAdxSignalAsync(contract, date, TimeFrameType.OneMinute, 14, default)) is { IsWarm: true });
        Require((await db.GetLastFuturesAtrSignalAsync(contract, date, TimeFrameType.OneMinute, 14, default)) is { IsWarm: true, AtrRatio: not null });
        Require((await db.GetLastFuturesMacdSignalAsync(contract, date, TimeFrameType.OneMinute, 9, 12, 26, default)) is { IsWarm: true });
    }
    static void Require(bool accepted) { if (!accepted) throw new InvalidOperationException("Indicator initialization or qualified projection failed."); }
    static void Require(ServiceResult<GuidResult> result) { if (!result.Success) throw new InvalidOperationException(result.ErrorMessage); }
    [GlobalCleanup] public async Task Cleanup() => await database.DisposeAsync();
}
