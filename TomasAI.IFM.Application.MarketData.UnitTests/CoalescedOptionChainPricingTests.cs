using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class CoalescedOptionChainPricingTests
{
    static DatabentoOptionChainRoute Route(OptionPricingContext c) => new()
    {
        FuturesOptionContractId = c.Contract.ContractId,
        Definition = new()
        {
            Dataset = c.Contract.Dataset, RawSymbol = c.Contract.RawSymbol, Ticker = "ES",
            Underlying = c.Contract.UnderlyingContractId, Instrument = new(c.Contract.PublisherId, c.Contract.InstrumentId),
            StrikePrice = 5000, Right = OptionRightSelection.Call, MaturityDate = new(2026, 10, 2)
        }
    };
    static LastQuoteTickSnapshot Tick(string id, long sequence, DateTimeOffset at) =>
        new(id, DateOnly.FromDateTime(at.UtcDateTime), 99.75m, 10, 1, 100.25m, 10, 1, sequence, at, at);

    [Fact]
    public async Task Burst_quotes_coalesce_and_reuse_IV_with_exact_original_provenance()
    {
        var clock = new ManualClock();
        var c = ReviewedFuturesPricingRoutingTests.Reviewed(OptionExerciseStyle.European);
        var store = new OptionChainPricingInputStore();
        var underlying = Quote("ES-future", 5000);
        store.Set(new(c, underlying));
        await using var scheduler = new CoalescedOptionChainPricing(store, Generation, new(), _ => underlying, clock);
        for (var i = 1; i <= 100; i++) scheduler.EnrichQuote(Route(c), Tick(c.Contract.ContractId, i, At));
        Assert.Equal(100, scheduler.QuoteObservations);
        Assert.Equal(0, scheduler.SelectionCalculations);
        scheduler.ProcessPass();
        var first = scheduler.ReadSelection(c.Contract.ContractId)!;
        Assert.Null(first.Failure);
        Assert.Equal(100, first.Option.Sequence);
        Assert.Equal(1, scheduler.IvSolves);
        clock.Now = At.AddMilliseconds(500);
        underlying = underlying with { EventAtUtc = clock.Now, ReceivedAtUtc = clock.Now, Sequence = 2 };
        scheduler.EnrichQuote(Route(c), Tick(c.Contract.ContractId, 101, clock.Now));
        scheduler.ProcessPass();
        var second = scheduler.ReadSelection(c.Contract.ContractId)!;
        Assert.Null(second.Failure);
        Assert.Equal(101, second.Option.Sequence);
        Assert.Equal(100, second.Iv!.Option.Sequence);
        Assert.Equal(first.Iv, second.Iv);
        Assert.Equal(1, scheduler.IvSolves);
        clock.Now = At.AddSeconds(5);
        underlying = underlying with { EventAtUtc = clock.Now, ReceivedAtUtc = clock.Now, Sequence = 3 };
        scheduler.EnrichQuote(Route(c), Tick(c.Contract.ContractId, 102, clock.Now));
        scheduler.ProcessPass();
        Assert.Equal(2, scheduler.IvSolves);
        Assert.Equal(102, scheduler.ReadSelection(c.Contract.ContractId)!.Iv!.Option.Sequence);
    }

    [Fact]
    public async Task Reference_change_and_stale_underlying_invalidate_selection_and_full_risk()
    {
        var clock = new ManualClock();
        var c = ReviewedFuturesPricingRoutingTests.Reviewed(OptionExerciseStyle.European);
        var store = new OptionChainPricingInputStore();
        var underlying = Quote("ES-future", 5000);
        store.Set(new(c, underlying));
        await using var scheduler = new CoalescedOptionChainPricing(store, Generation, new(), _ => underlying, clock);
        scheduler.EnrichQuote(Route(c), Tick(c.Contract.ContractId, 1, At));
        scheduler.RequireRisk(c.Contract.ContractId, true);
        scheduler.ProcessPass(); scheduler.ProcessPass(true);
        Assert.NotNull(scheduler.ReadRisk(c.Contract.ContractId)!.Value);
        clock.Now = At.AddSeconds(2);
        Assert.Null(scheduler.ReadSelection(c.Contract.ContractId)!.Delta);
        Assert.Null(scheduler.ReadRisk(c.Contract.ContractId)!.Value);
        clock.Now = At;
        store.Set(new(c with { PublicationPolicyVersion = "changed/v2" }, underlying));
        Assert.Null(scheduler.ReadSelection(c.Contract.ContractId)!.Delta);
        Assert.Null(scheduler.ReadRisk(c.Contract.ContractId)!.Value);
        scheduler.Remove(c.Contract.ContractId);
        Assert.Null(scheduler.ReadSelection(c.Contract.ContractId));
    }

    [Fact]
    public async Task Bounded_passes_do_not_starve_quieter_contracts()
    {
        var clock = new ManualClock();
        var c = ReviewedFuturesPricingRoutingTests.Reviewed(OptionExerciseStyle.European);
        var store = new OptionChainPricingInputStore();
        var contexts = Enumerable.Range(0, 3).Select(i => c with { Contract = c.Contract with
        { ContractId = "option-" + i, InstrumentId = (uint)(10 + i) } }).ToArray();
        foreach (var context in contexts) store.Set(new(context, Quote("ES-future", 5000)));
        await using var scheduler = new CoalescedOptionChainPricing(store, Generation,
            new() { MaximumContracts = 3, MaximumContractsPerPass = 1 }, _ => Quote("ES-future", 5000), clock);
        foreach (var context in contexts) scheduler.EnrichQuote(Route(context), Tick(context.Contract.ContractId, 1, At));
        for (var i = 0; i < 3; i++)
        {
            scheduler.EnrichQuote(Route(contexts[0]), Tick(contexts[0].Contract.ContractId, i + 2, At));
            scheduler.ProcessPass();
        }
        Assert.All(contexts, context => Assert.NotNull(scheduler.ReadSelection(context.Contract.ContractId)!.Delta));
        Assert.Equal(3, scheduler.SelectionCalculations);
    }

    [Fact]
    public async Task Cadence_sweeps_the_full_chain_in_yielding_batches_without_waiting_an_interval_per_batch()
    {
        var clock = new ManualClock();
        var c = ReviewedFuturesPricingRoutingTests.Reviewed(OptionExerciseStyle.European);
        var store = new OptionChainPricingInputStore();
        var contexts = Enumerable.Range(0, 512).Select(i => c with { Contract = c.Contract with
        { ContractId = "sweep-" + i, InstrumentId = (uint)(10 + i) } }).ToArray();
        foreach (var context in contexts) store.Set(new(context, Quote("ES-future", 5000)));
        await using var scheduler = new CoalescedOptionChainPricing(store, Generation,
            new() { MaximumContractsPerPass = 16 }, _ => Quote("ES-future", 5000), clock);
        foreach (var context in contexts) scheduler.EnrichQuote(Route(context), Tick(context.Contract.ContractId, 1, At));
        await scheduler.ProcessSweepAsync();
        Assert.All(contexts, context => Assert.NotNull(scheduler.ReadSelection(context.Contract.ContractId)!.Delta));
        Assert.Equal(512, scheduler.SelectionCalculations);
        Assert.Equal(512, scheduler.IvSolves);
        await scheduler.ProcessSweepAsync();
        Assert.Equal(1024, scheduler.SelectionCalculations);
        Assert.Equal(512, scheduler.IvSolves);
    }

    sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = At;
        public override DateTimeOffset GetUtcNow() => Now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new IdleTimer();
        sealed class IdleTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
