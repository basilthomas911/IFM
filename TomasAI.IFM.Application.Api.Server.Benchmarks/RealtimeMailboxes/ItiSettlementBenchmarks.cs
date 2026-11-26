using BenchmarkDotNet.Attributes;
using TomasAI.IFM.TestSupport;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

/// <summary>Same synthetic ES samples and command service; durable path includes physical input/completion flushes.</summary>
[MemoryDiagnoser]
public class ItiSettlementBenchmarks
{
    ItiDeliveryFixture before = null!, volatileAfter = null!, durable = null!, refusedBefore = null!, refusedAfter = null!, pendingRetry = null!;
    FuturesItiIngressRecovery retryRecovery = null!;
    FuturesItiIngressRecovery recovery = null!;
    long sequence = 1;
    [GlobalSetup]
    public async Task Setup()
    {
        before = new(); volatileAfter = new();
        refusedBefore = new(effects: new() { Fail = true }); refusedAfter = new(effects: new() { Fail = true });
        retryRecovery = new(Path.Combine(Path.GetTempPath(), "IFM-iti-retry-benchmark-" + Guid.NewGuid().ToString("N")));
        pendingRetry = new(retryRecovery, new() { Fail = true });
        if (!await ItiDeliveryFixture.Source(1).ExecuteLegacyAsync(refusedBefore.Context)
            || await refusedAfter.Receive(ItiDeliveryFixture.Source(1))
            || await pendingRetry.Receive(ItiDeliveryFixture.Source(1))) throw new InvalidDataException("Failure benchmark outcome changed.");
        recovery = new(Path.Combine(Path.GetTempPath(), "IFM-iti-benchmark-" + Guid.NewGuid().ToString("N")));
        durable = new(recovery);
        if (!await durable.Receive(ItiDeliveryFixture.Source(1))) throw new InvalidDataException("Initial receipt did not complete.");
    }
    [IterationSetup] public void Reset() { before.ResetMeasurement(); volatileAfter.ResetMeasurement(); durable.ResetMeasurement(); refusedBefore.ResetMeasurement(); refusedAfter.ResetMeasurement(); pendingRetry.ResetMeasurement(); }
    [Benchmark(Baseline = true)] public ValueTask<bool> BeforeVolatileSuccess()
        => ItiDeliveryFixture.Source(++sequence).ExecuteLegacyAsync(before.Context);
    [Benchmark] public ValueTask<bool> AfterVolatileSuccess()
        => volatileAfter.Receive(ItiDeliveryFixture.Source(++sequence));
    [Benchmark] public ValueTask<bool> AfterDurableNewInput()
        => durable.Receive(ItiDeliveryFixture.Source(++sequence));
    [Benchmark] public ValueTask<bool> AfterCompletedReplay()
        => durable.Receive(ItiDeliveryFixture.Source(1));
    [Benchmark] public ValueTask<bool> BeforeRefused()
        => ItiDeliveryFixture.Source(1).ExecuteLegacyAsync(refusedBefore.Context);
    [Benchmark] public ValueTask<bool> AfterVolatileRefused()
        => refusedAfter.Receive(ItiDeliveryFixture.Source(1));
    [Benchmark] public ValueTask<bool> AfterDurablePendingRetry()
        => pendingRetry.Receive(ItiDeliveryFixture.Source(1));
    [GlobalCleanup] public async Task Cleanup()
    { await before.DisposeAsync(); await volatileAfter.DisposeAsync(); await durable.DisposeAsync(); await refusedBefore.DisposeAsync(); await refusedAfter.DisposeAsync(); await pendingRetry.DisposeAsync(); recovery.Dispose(); retryRecovery.Dispose(); }
}
