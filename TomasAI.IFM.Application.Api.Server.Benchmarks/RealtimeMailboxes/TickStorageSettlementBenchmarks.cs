using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

/// <summary>Decode, real actor/scheduler, substituted projection and completion; quotes release pooled leases.</summary>
[MemoryDiagnoser]
public class TickStorageSettlementBenchmarks
{
    [Params("Trade", "Quote", "Quote512")] public string Kind { get; set; } = "Trade";
    [Params(false, true)] public bool Refused { get; set; }
    TickStorageDeliveryFixture before = null!, after = null!;
    byte[] bytes = [];
    [GlobalSetup]
    public async Task Setup()
    {
        before = new(false, context => new LegacyTickAggregationRealtimeActor(context)) { Failure = Refused ? "refused" : "none" };
        after = new(false) { Failure = Refused ? "refused" : "none" };
        var input = TickStorageDeliveryFixture.Input(Kind, 1);
        if (Kind == "Quote512")
        {
            var quote = (FuturesTickQuoteDataChangedEvent)input;
            input = quote with { QuoteCount = 512, QuoteData = new FuturesTickQuoteDataSegment(
                Enumerable.Range(0, 512).Select(i => quote.QuoteData.Items[0] with { SourceSequence = (uint)(i + 1) }).ToArray(), 512) };
        }
        bytes = TickStorageDeliveryFixture.Encode(input);
        await before.Start(); await after.Start();
        if (!await before.Deliver(TickStorageDeliveryFixture.Decode(Kind, bytes)) || await after.Deliver(TickStorageDeliveryFixture.Decode(Kind, bytes)) != !Refused)
            throw new InvalidDataException("Frozen/repaired tick settlement contracts were not reproduced.");
    }
    [IterationSetup] public void Reset() { before.Reset(); after.Reset(); }
    [Benchmark(Baseline = true)] public Task<bool> BeforeDiscardedResult() => before.Deliver(TickStorageDeliveryFixture.Decode(Kind, bytes));
    [Benchmark] public Task<bool> AfterRequiredSettlement() => after.Deliver(TickStorageDeliveryFixture.Decode(Kind, bytes));
    [GlobalCleanup] public async Task Cleanup() { await before.DisposeAsync(); await after.DisposeAsync(); }
}
