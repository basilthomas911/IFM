using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using BenchmarkDotNet.Attributes;
using NSubstitute;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

[MemoryDiagnoser]
public class EodWorkerSettlementBenchmarks
{
    [Params(1,64)] public int Count { get; set; }
    LegacyEodTradeWorker before = null!;
    FuturesEodTradeWorker after = null!;
    readonly FuturesTickTradeDataInsertedEvent input = new() { Id = Guid.NewGuid() };
    int beforeCount, afterCount;
    [GlobalSetup] public void Setup()
    {
        before = new(128, _ => { beforeCount++; return ValueTask.FromResult(true); }, NullLogger.Instance);
        after = new(128, _ => { afterCount++; return ValueTask.FromResult(true); }, NullLogger.Instance);
    }
    [Benchmark(Baseline=true)] public async Task BeforeAdmissionThenDrain()
    {
        var previous = beforeCount;
        for (var n=0;n<Count;n++) await before.EnqueueAsync(input);
        await before.DrainAsync();
        if (beforeCount-previous!=Count) throw new InvalidDataException("Baseline work changed.");
    }
    [Benchmark] public async Task AfterCompletedEachInput()
    {
        var previous=afterCount;
        for (var n=0;n<Count;n++) await after.EnqueueAsync(input);
        if(afterCount-previous!=Count || after.Pending!=0) throw new InvalidDataException("Completion work changed.");
    }
    [GlobalCleanup] public async Task Cleanup() { await before.StopAsync(); await after.StopAsync(); }
}
[MemoryDiagnoser]
public class EodInlineSettlementBenchmarks
{
    EodDeliveryFixture before = null!, after = null!;
    [GlobalSetup] public void Setup() { before = new(false); after = new(false); }
    [IterationSetup] public void Reset() { before.Attempts.Clear(); before.Committed.Clear(); before.ProjectionIds.Clear(); before.Context.ClearReceivedCalls(); before.Context.Projector.ClearReceivedCalls(); after.Attempts.Clear(); after.Committed.Clear(); after.ProjectionIds.Clear(); after.Context.ClearReceivedCalls(); after.Context.Projector.ClearReceivedCalls(); }
    [Benchmark(Baseline=true)] public async Task BeforeIgnoredProjectionResult()
    {
        before.Current=EodDeliveryFixture.Initial();
        _ = await EodDeliveryFixture.Source(1).ExecuteAsync(before.Context,before.Context.Projector,before.Context.Logger);
    }
    [Benchmark] public async Task AfterValidatedOrderedProjection()
    {
        after.Current=EodDeliveryFixture.Initial();
        await after.Context.DispatchStatisticsAsync(after.Context,EodDeliveryFixture.Source(1));
    }
    [GlobalCleanup] public async Task Cleanup() { await before.DisposeAsync(); await after.DisposeAsync(); }
}
