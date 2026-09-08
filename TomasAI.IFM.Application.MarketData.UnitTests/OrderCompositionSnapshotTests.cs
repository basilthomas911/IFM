using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class OrderCompositionSnapshotTests
{
    static CompositionSnapshotRequest Request(bool options = true) => new(Guid.Parse("ab364d11-fb45-4804-b1fe-c467b7dd8e32"), "fixture-scope",
        "Daily", Generation, At, At.AddSeconds(2), options);
    static CompositionMarketInstrument Instrument(string id = "ES-option-call")
    {
        var context = Context() with { Contract = Contract() with { ContractId = id } };
        return new(id, Quote(id, 100), context, 5000, true, Quote("ES-future", 5000));
    }
    static MarketCompositionSnapshotProvider Provider(params CompositionMarketPage[] pages) => new(new Pages(pages), new Clock(At));

    [Fact]
    public async Task All_pages_are_consumed_before_a_deterministic_snapshot_is_returned()
    {
        var a = new CompositionMarketPage("scope/v1", Generation, 2, [Instrument("a")], "next");
        var b = new CompositionMarketPage("scope/v1", Generation, 2, [Instrument("b")], null);
        var first = await Provider(a, b).CaptureAsync(Request(), default);
        var shuffled = await Provider(b with { Continuation = "next" }, a with { Continuation = null }).CaptureAsync(Request(), default);
        Assert.Null(first.Failure); Assert.Null(shuffled.Failure);
        Assert.Equal(2, first.Snapshot!.Instruments.Length);
        Assert.Equal(first.Snapshot.Digest, shuffled.Snapshot!.Digest);
    }

    [Theory]
    [InlineData("incomplete", "IncompleteChain")]
    [InlineData("generation", "Recovering")]
    [InlineData("scope", "IncompleteChain")]
    [InlineData("duplicate", "ConflictingDefinition")]
    [InlineData("overflow", "SnapshotLimit")]
    public async Task Partial_or_incoherent_scope_cannot_produce_a_candidate(string change, string code)
    {
        var a = new CompositionMarketPage("scope/v1", Generation, 2, [Instrument("a")], "next");
        var b = new CompositionMarketPage("scope/v1", Generation, 2, [Instrument("b")], null);
        if (change == "incomplete") a = a with { Continuation = null };
        if (change == "generation") b = b with { GenerationId = Guid.NewGuid() };
        if (change == "scope") b = b with { ScopeToken = "scope/v2" };
        if (change == "duplicate") b = b with { Instruments = [Instrument("a")] };
        if (change == "overflow") a = a with { TotalContracts = 513 };
        var result = await Provider(a, b).CaptureAsync(Request(), default);
        Assert.Null(result.Snapshot); Assert.Equal(code, result.Failure!.Code);
    }

    [Fact]
    public async Task A_required_pricing_failure_is_not_silently_removed_from_ranking_scope()
    {
        var bad = Instrument("b") with { Quote = Quote("b", 100) with { EventAtUtc = At.AddSeconds(-10) } };
        var result = await Provider(new CompositionMarketPage("scope/v1", Generation, 2, [Instrument("a"), bad], null)).CaptureAsync(Request(), default);
        Assert.Null(result.Snapshot); Assert.Equal("StaleData", result.Failure!.Code);
    }

    [Theory]
    [InlineData("Daily")] [InlineData("Weekly")] [InlineData("Monthly")]
    public async Task Futures_snapshot_needs_no_treasury_options_or_greeks_on_any_horizon(string horizon)
    {
        var future = new CompositionMarketInstrument("ES-future", Quote("ES-future", 5000), null, null, null, null,
            new("ES-future", "ES", "GLBX.MDP3", "XCME", "USD", At.AddDays(30), 50, .25m, new string('b', 64)));
        var result = await Provider(new CompositionMarketPage("future/v1", Generation, 1, [future], null)).CaptureAsync(Request(false) with { Horizon = horizon }, default);
        Assert.Null(result.Failure);
        Assert.Null(Assert.Single(result.Snapshot!.Instruments).Valuation);
    }

    [Fact]
    public async Task Complete_empty_scope_is_distinct_from_a_failed_scope()
    {
        var result = await Provider(new CompositionMarketPage("empty/v1", Generation, 0, [], null)).CaptureAsync(Request(), default);
        Assert.Null(result.Failure); Assert.Empty(result.Snapshot!.Instruments);
    }

    [Fact]
    public async Task Snapshot_preserves_effective_limits_and_expires_at_the_stricter_quote_age()
    {
        var original = Instrument();
        var instrument = original with { Pricing = original.Pricing! with { MaximumQuoteAgeMilliseconds = 500, MaximumQuoteSkewMilliseconds = 100 } };
        var result = await Provider(new CompositionMarketPage("scope/v1", Generation, 1, [instrument], null)).CaptureAsync(Request(), default);
        Assert.Null(result.Failure);
        Assert.Equal(At.AddMilliseconds(500), result.Snapshot!.ValidUntilUtc);
        Assert.Equal(100, Assert.Single(result.Snapshot.Instruments).Instrument.Pricing!.MaximumQuoteSkewMilliseconds);
        var wider = original with { Pricing = original.Pricing! with { MaximumQuoteAgeMilliseconds = 5000 } };
        result = await Provider(new CompositionMarketPage("scope/v1", Generation, 1, [wider], null)).CaptureAsync(Request(), default);
        Assert.Equal(1000, Assert.Single(result.Snapshot!.Instruments).Instrument.Pricing!.MaximumQuoteAgeMilliseconds);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_a_success_or_domain_failure()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Provider(new CompositionMarketPage("empty/v1", Generation, 0, [], null)).CaptureAsync(Request(), cancellation.Token));
    }

    [Fact]
    public async Task European_universe_excludes_known_american_and_fails_unknown_scope()
    {
        var european = Contract();
        var american = Contract() with { ContractId = "american", RawSymbol = "american", InstrumentId = 11, ExerciseStyle = OptionExerciseStyle.American };
        var store = new Conventions([european, american]);
        var universe = new EuropeanOptionUniverse(store);
        var result = await universe.QualifyAsync([Candidate(american), Candidate(european)], true, At, default);
        Assert.Null(result.Failure); Assert.Single(result.Definitions); Assert.Single(result.Exclusions);
        store.Values["american"] = american with { ExerciseStyle = OptionExerciseStyle.Unknown };
        result = await universe.QualifyAsync([Candidate(american), Candidate(european)], true, At, default);
        Assert.Equal("ContractMetadataUnavailable", result.Failure!.Code);
        Assert.Empty(result.Definitions);
        Assert.Equal("IncompleteChain", (await universe.QualifyAsync([], false, At, default)).Failure!.Code);
    }

    [Fact]
    public void Real_chain_enricher_prices_quotes_and_never_uses_trade_price_as_iv_mark()
    {
        var input = new OptionChainPricingInputStore();
        input.Set(new(Context(), Quote("ES-future", 5000)));
        var enricher = new Black76OptionChainGreeksEnricher(input, Generation, new Clock(At));
        var route = new DatabentoOptionChainRoute { FuturesOptionContractId = Contract().ContractId, Definition = Candidate(Contract()).Definition };
        var tick = new LastQuoteTickSnapshot(Contract().ContractId, new(2026, 9, 8), 99.75m, 10, 1, 100.25m, 10, 1, 1, At, At);
        var quoted = enricher.EnrichQuote(route, tick);
        Assert.True(quoted.IsValid); Assert.NotNull(quoted.PricingContextDigest);
        var trade = enricher.EnrichTrade(route, new(Contract().ContractId, new(2026, 9, 8), 5000, 1, 2, At, At));
        Assert.Equal(quoted.ImpliedVolatility, trade.ImpliedVolatility);
        Assert.Equal(100m, trade.OptionMarkPrice);
        input.Set(new(Context() with { GenerationId = Guid.NewGuid() }, Quote("ES-future", 5000)));
        Assert.Equal("Recovering", enricher.EnrichQuote(route, tick).PricingFailure!.Code);
        input.Remove(Contract().ContractId);
        var missing = enricher.EnrichQuote(route, tick);
        Assert.False(missing.IsValid); Assert.Null(missing.Delta); Assert.NotNull(missing.PricingFailure);
    }

    static OptionDefinitionCandidate Candidate(OptionPricingConvention c) => new(c.ContractId, c.MappingVersion, c.DefinitionDigest,
        new OptionContractDefinition
        {
            Dataset = c.Dataset, RawSymbol = c.RawSymbol, Ticker = "ES", Underlying = "ES-future",
            Instrument = new(c.PublisherId, c.InstrumentId), Right = OptionRightSelection.Call, StrikePrice = 5000,
            MaturityDate = DateOnly.FromDateTime(c.ExpirationUtc.UtcDateTime),
            ExpirationTimestampNanoseconds = checked((ulong)(c.ExpirationUtc - DateTimeOffset.UnixEpoch).Ticks * 100)
        });
    sealed class Pages(CompositionMarketPage[] pages) : ICompositionMarketSource
    {
        int next;
        public Task<CompositionMarketPage> ReadAsync(CompositionSnapshotRequest request, string? continuation, CancellationToken cancellationToken)
            => Task.FromResult(pages[next++]);
    }
    sealed class Conventions(IEnumerable<OptionPricingConvention> values) : IOptionPricingConventionStore
    {
        public Dictionary<string, OptionPricingConvention> Values { get; } = values.ToDictionary(x => x.ContractId);
        public Task<OptionPricingConvention?> GetAsync(string id, string version, CancellationToken cancellationToken) => Task.FromResult(Values.GetValueOrDefault(id));
    }
}
