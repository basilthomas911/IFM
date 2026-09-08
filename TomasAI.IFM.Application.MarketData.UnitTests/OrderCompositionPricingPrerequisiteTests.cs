using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Framework.OptionPricer.Black76;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class OrderCompositionPricingPrerequisiteTests
{
    internal static readonly DateTimeOffset At = new(2026, 9, 8, 16, 0, 0, TimeSpan.Zero);
    internal static readonly Guid Generation = Guid.Parse("29a8cd30-5aeb-4b87-bfdf-b12cfd8b9dd9");
    internal static TreasuryRateConversionPolicy Conversion => new("FinancialModelingPrep", "fixture:CMT", TreasuryRateConvention.UsTreasuryCmtNominalSemiannual, "test/v1", "synthetic-evidence");
    internal static TreasuryCurveSnapshot Curve(DateOnly? date = null) => new(date ?? new(2026, 9, 8),
        [new(TreasuryTenor.OneMonth, 5m), new(TreasuryTenor.TwoMonth, 5.1m), new(TreasuryTenor.ThreeMonth, 5.2m)], At, "FinancialModelingPrep");
    internal static OptionPricingConvention Contract() => new()
    {
        ContractId = "ES-option-call", Dataset = "GLBX.MDP3", PublisherId = 1, InstrumentId = 10,
        RawSymbol = "fixture-call", Root = "ES", Exchange = "XCME", Currency = "USD", UnderlyingContractId = "ES-future",
        ExerciseStyle = OptionExerciseStyle.European, SettlementStyle = OptionSettlementStyle.DeliveryOfFuture,
        ExpirationUtc = At.AddDays(24), LastTradingUtc = At.AddDays(24), DayCount = PricingDayCount.Actual365Fixed,
        CalendarVersion = "fixture-calendar/v1", Multiplier = 50, TickSize = 0.25m, TickRuleVersion = "fixture-tick/v1",
        DefinitionDigest = new('a', 64), MappingVersion = "fixture-mapping/v1", EvidenceId = "synthetic-series",
        EffectiveFromUtc = At.AddDays(-10), EffectiveUntilUtc = At.AddDays(40)
    };
    internal static OptionPricingCalendar Calendar() => new("fixture-calendar/v1", "America/New_York",
        new(2026, 1, 1), new(2026, 12, 31), new(18, 0), Enumerable.Range(0, 365).Select(i => new DateOnly(2026, 1, 1).AddDays(i))
            .Where(x => x.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).ToImmutableArray());
    internal static TreasuryPublicationPolicy Publication() => new("fixture-publication/v1", At.AddDays(-2), At.AddDays(2),
        [new(new(2026, 9, 4), At.AddDays(-4)), new(new(2026, 9, 8), At.AddHours(-1)), new(new(2026, 9, 9), At.AddDays(1))]);
    internal static OptionPricingContext Context() => new(Contract(), Calendar(), TreasuryRateConversion.Convert(Curve(), TreasuryTenor.OneMonth, Conversion).Value!,
        At.AddHours(1), Generation, OptionCalculator.EngineVersion, 1000, 250, "fixture-publication/v1");
    internal static OptionPricingQuote Quote(string id, decimal mid) => new(id, mid - 0.25m, mid + 0.25m, 10, 10, At, At, 1, Generation);
    static OptionPricingPassResult Price(OptionPricingContext? context = null, OptionPricingQuote? option = null, OptionPricingQuote? underlying = null, DateTimeOffset? at = null) =>
        Black76PricingModel.Calculate(context ?? Context(), underlying ?? Quote("ES-future", 5000), option ?? Quote("ES-option-call", 100), 5000, true, at ?? At);

    [Theory]
    [InlineData(-1, 0)] [InlineData(0, 1)] [InlineData(29, 1)] [InlineData(30, 2)]
    [InlineData(59, 2)] [InlineData(60, 3)] [InlineData(89, 3)] [InlineData(90, 0)]
    public void Trading_day_buckets_have_exact_boundaries(int days, int expected) =>
        Assert.Equal(expected, (int?)TreasuryRateConversion.SelectTenor(days) ?? 0);

    [Fact]
    public void Continuous_rate_preserves_discount_identity_and_percentage_units()
    {
        var result = TreasuryRateConversion.Convert(Curve(), TreasuryTenor.OneMonth, Conversion);
        Assert.True(result.Succeeded);
        Assert.Equal(0.0493852251807428, result.Value!.AnnualContinuousRate, 14);
        Assert.Equal(Math.Pow(1.025, -0.5), Math.Exp(-result.Value.AnnualContinuousRate * .25), 14);
        Assert.Equal(5m, result.Value.RatePercent);
        Assert.Equal(.05m, Curve().Rates[0].DecimalRate);
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(5)]
    public void Published_zero_and_negative_rates_are_not_clamped(int percent)
    {
        var curve = Curve() with { Rates = [new(TreasuryTenor.OneMonth, percent)] };
        var value = TreasuryRateConversion.Convert(curve, TreasuryTenor.OneMonth, Conversion).Value!;
        Assert.Equal(2 * double.LogP1(percent / 200d), value.AnnualContinuousRate);
    }

    [Fact]
    public void Missing_tenor_unknown_convention_and_invalid_domain_never_fall_back()
    {
        Assert.Equal("TreasuryTenorMissing", TreasuryRateConversion.Convert(Curve() with { Rates = [new(TreasuryTenor.TwoMonth, 5m)] }, TreasuryTenor.OneMonth, Conversion).Error);
        Assert.Equal("RateConventionUnsupported", TreasuryRateConversion.Convert(Curve(), TreasuryTenor.OneMonth, Conversion with { EvidenceId = "" }).Error);
        Assert.Equal("RateConversionDomainInvalid", TreasuryRateConversion.Convert(Curve() with { Rates = [new(TreasuryTenor.OneMonth, -200)] }, TreasuryTenor.OneMonth, Conversion).Error);
    }

    [Fact]
    public void Curve_digest_is_order_invariant_but_detects_same_date_corrections()
    {
        Assert.Equal(TreasuryRateConversion.Digest(Curve()), TreasuryRateConversion.Digest(Curve() with { Rates = Curve().Rates.Reverse().ToArray(), RetrievedAtUtc = At.AddHours(1) }));
        Assert.NotEqual(TreasuryRateConversion.Digest(Curve()), TreasuryRateConversion.Digest(Curve() with { Rates = [new(TreasuryTenor.OneMonth, 4m)] }));
    }

    [Theory]
    [InlineData(OptionExerciseStyle.American, "PricingModelUnsupported")]
    [InlineData(OptionExerciseStyle.Unknown, "ContractMetadataUnavailable")]
    public void Unsupported_style_never_becomes_european(OptionExerciseStyle style, string error)
    {
        var result = Price(Context() with { Contract = Contract() with { ExerciseStyle = style } });
        Assert.Null(result.Value); Assert.Equal(error, result.Failure!.Code);
    }

    [Fact]
    public void Explicit_fractional_expiry_prices_same_day_without_rounding_to_zero()
    {
        var contract = Contract() with { ExpirationUtc = At.AddHours(6), LastTradingUtc = At.AddHours(6) };
        var result = Price(Context() with { Contract = contract }, Quote("ES-option-call", 10));
        Assert.Null(result.Failure);
        Assert.Equal(.25 / 365, result.Value!.TimeToExpiry, 14);
        Assert.Null(Price(Context() with { Contract = contract }, at: At.AddHours(6)).Value);
    }

    [Fact]
    public void Positive_fractional_constructor_preserves_date_only_path()
    {
        var a = new OptionCalculator(new DateOnly(2026, 9, 8), new DateOnly(2026, 10, 2)).GetOptionGreeks("CALL", 5000, 5000, 100, .05);
        var b = new OptionCalculator(24d / 365).GetOptionGreeks("CALL", 5000, 5000, 100, .05);
        Assert.Equal(a, b);
        Assert.Throws<ArgumentOutOfRangeException>(() => new OptionCalculator(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OptionCalculator(double.NaN));
    }

    [Fact]
    public void Pricing_is_deterministic_and_the_iv_reprices_its_quote()
    {
        var result = Price();
        Assert.Null(result.Failure);
        Assert.Equal(100d, result.Value!.TheoreticalPrice, 7);
        Assert.InRange(result.Value.Delta, 0, 1);
        Assert.Equal(result, Price());
        Assert.Equal(result.Value.ContextDigest, Price(option: Quote("ES-option-call", 100) with { Bid = 99.7500m, Ask = 100.2500m }).Value!.ContextDigest);
    }

    [Fact]
    public void Bad_quote_missing_reference_and_recovery_are_failures_not_zero_greeks()
    {
        Assert.Equal("InvalidQuote", Price(option: Quote("ES-option-call", 100) with { Ask = 1 }).Failure!.Code);
        Assert.Equal("StaleData", Price(option: Quote("ES-option-call", 100) with { EventAtUtc = At.AddSeconds(-2) }).Failure!.Code);
        Assert.Equal("IncoherentQuotes", Price(option: Quote("ES-option-call", 100) with { EventAtUtc = At.AddMilliseconds(-251) }).Failure!.Code);
        Assert.Equal("Recovering", Price(option: Quote("ES-option-call", 100) with { GenerationId = Guid.NewGuid() }).Failure!.Code);
        Assert.Equal("TreasuryStale", Price(Context() with { ValidUntilUtc = At }).Failure!.Code);
        Assert.Equal("GreeksCalculationFailed", Price(option: Quote("ES-option-call", 6000)).Failure!.Code);
    }

    [Fact]
    public void Locked_quotes_and_exact_freshness_boundaries_are_valid()
    {
        var option = Quote("ES-option-call", 100) with { Bid = 100, Ask = 100, EventAtUtc = At.AddMilliseconds(-1000) };
        var underlying = Quote("ES-future", 5000) with { EventAtUtc = At.AddMilliseconds(-750) };
        Assert.Null(Price(option: option, underlying: underlying).Failure);
    }

    [Fact]
    public void Calendar_requires_complete_coverage_and_does_not_reuse_year_fraction_for_tenor()
    {
        Assert.Equal(18, OptionPricingQualification.CountTradingDays(Calendar(), Contract(), At));
        Assert.Equal(24d / 365, OptionPricingQualification.YearFraction(Contract(), At));
        Assert.Throws<ArgumentException>(() => OptionPricingQualification.CountTradingDays(Calendar() with { CoverageUntil = new(2026, 9, 30) }, Contract(), At));
        Assert.Equal("DayCountUnsupported", Price(Context() with { Contract = Contract() with { DayCount = PricingDayCount.Unknown } }).Failure!.Code);
    }

    [Fact]
    public async Task Production_context_provider_reuses_curve_and_fails_when_daily_value_is_stale()
    {
        var source = new CurveSource(Curve());
        var provider = new TreasuryPricingProvider(source, new Clock(At));
        var contexts = new OptionPricingContextProvider(provider);
        var first = await contexts.PrepareAsync(Contract(), Calendar(), Publication(), Conversion, Generation, OptionCalculator.EngineVersion, At, default);
        var second = await contexts.PrepareAsync(Contract(), Calendar(), Publication(), Conversion, Generation, OptionCalculator.EngineVersion, At, default);
        Assert.NotNull(first.Context); Assert.NotNull(second.Context);
        Assert.Equal(first.Context.Contract, second.Context.Contract);
        Assert.Equal(first.Context.Rate, second.Context.Rate);
        Assert.Equal(first.Context.ValidUntilUtc, second.Context.ValidUntilUtc);
        Assert.Equal(1, source.Calls);
        var stale = new TreasuryPricingProvider(new CurveSource(Curve(new(2026, 9, 4))), new Clock(At));
        Assert.Equal("TreasuryStale", (await stale.GetAsync(At, 18, Publication(), Conversion, default)).Error);
    }

    [Fact]
    public async Task Publication_boundary_and_future_observation_are_enforced()
    {
        Assert.Equal(new DateOnly(2026, 9, 4), Publication().RequiredValueDate(At.AddHours(-1).AddTicks(-1)));
        Assert.Equal(new DateOnly(2026, 9, 8), Publication().RequiredValueDate(At.AddHours(-1)));
        var future = new TreasuryPricingProvider(new CurveSource(Curve() with { RetrievedAtUtc = At.AddSeconds(1) }), new Clock(At));
        Assert.False((await future.GetAsync(At, 0, Publication(), Conversion, default)).Succeeded);
        var completedRefresh = new TreasuryPricingProvider(new CurveSource(Curve() with { RetrievedAtUtc = At.AddSeconds(1) }), new Clock(At.AddSeconds(2)));
        Assert.False((await completedRefresh.GetAsync(At, 0, Publication(), Conversion, default)).Succeeded);
        Assert.True((await completedRefresh.GetAsync(At.AddSeconds(2), 0, Publication(), Conversion, default)).Succeeded);
    }

    [Fact]
    public void Shared_messagepack_roundtrip_preserves_explicit_contract_and_context()
    {
        var original = Context();
        var payload = MessagePackBinarySerializer.Shared.Serialize(original)!;
        var restored = MessagePackBinarySerializer.Shared.Deserialize<OptionPricingContext>(payload)!;
        Assert.Equal(original.Contract, restored.Contract);
        Assert.True(original.Calendar.TradingDates.SequenceEqual(restored.Calendar.TradingDates));
        Assert.Equal(Price(original), Price(restored));
    }

    internal sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    internal sealed class CurveSource(TreasuryCurveSnapshot curve) : ITreasuryCurve
    {
        public int Calls;
        public TreasuryContinuousRateResult GetContinuouslyCompoundedAnnualRate(TreasuryCurveSnapshot snapshot, TreasuryTenor tenor, TreasuryRateConversionPolicy policy) => TreasuryRateConversion.Convert(snapshot, tenor, policy);
        public Task<TreasuryCurveSnapshot?> GetLatestAsync(DateOnly date, CancellationToken cancellationToken = default) { Calls++; return Task.FromResult<TreasuryCurveSnapshot?>(curve); }
        public Task<IReadOnlyList<TreasuryCurveSnapshot>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TreasuryCurveSnapshot>>([curve]);
    }
}
