using BenchmarkDotNet.Attributes;
using TomasAI.IFM.TestSupport;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Realtime.Actor;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

/// <summary>Actual scheduler and actors, same command stub; includes settlement and payload disposal.</summary>
[MemoryDiagnoser]
public class IndicatorSettlementBenchmarks
{
    [Params("Adx", "Atr", "Ema", "Macd", "Rsi", "Vwap", "VxTermStructure")]
    public string Kind { get; set; } = "Adx";
    [Params(false, true)] public bool Refused { get; set; }
    IndicatorDeliveryFixture before = null!, after = null!;
    [GlobalSetup]
    public async Task Setup()
    {
        before = new(Kind, new() { Fail = Refused }, Legacy);
        after = new(Kind, new() { Fail = Refused });
        await before.Start(); await after.Start();
        if (!await before.Deliver(before.Input(1)) || await after.Deliver(after.Input(1)) == Refused)
            throw new InvalidDataException("Benchmark settlement does not match the frozen/repaired contracts.");
    }
    public static IActor Legacy(string kind, IEventActorContext context) => kind switch
    {
        "Adx" => new LegacyFuturesAdxSignalRealtimeActor((IFuturesAdxSignalRealtimeContext)context),
        "Atr" => new LegacyFuturesAtrSignalRealtimeActor((IFuturesAtrSignalRealtimeContext)context),
        "Ema" => new LegacyFuturesEmaSignalRealtimeActor((IFuturesEmaSignalRealtimeContext)context),
        "Macd" => new LegacyFuturesMacdSignalRealtimeActor((IFuturesMacdSignalRealtimeContext)context),
        "Rsi" => new LegacyFuturesRsiSignalRealtimeActor((IFuturesRsiSignalRealtimeContext)context),
        "Vwap" => new LegacyFuturesVwapSignalRealtimeActor((IFuturesVwapSignalRealtimeContext)context),
        "VxTermStructure" => new LegacyFuturesVxTermStructureSignalRealtimeActor((IFuturesVxTermStructureSignalRealtimeContext)context),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    [IterationSetup] public void ResetMeasurement() { before.ResetMeasurement(); after.ResetMeasurement(); }
    [Benchmark(Baseline = true)] public Task<bool> BeforeDiscardedOutcome() => before.Deliver(before.Input(1));
    [Benchmark] public Task<bool> AfterRequiredSettlement() => after.Deliver(after.Input(1));
    [GlobalCleanup] public async Task Cleanup() { await before.DisposeAsync(); await after.DisposeAsync(); }
}
