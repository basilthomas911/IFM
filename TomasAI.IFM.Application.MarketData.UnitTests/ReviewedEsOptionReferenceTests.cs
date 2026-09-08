using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class ReviewedEsOptionReferenceTests
{
    static ContractDetail Future() => new()
    {
        Dataset = "GLBX.MDP3", RawSymbol = "ESU6", Ticker = "ES", Underlying = "", Instrument = new(1, 42140870),
        ContractKind = ContractKind.Future, MaturityDate = new(2026, 9, 18), ExpirationTimestampNanoseconds = 1789738200000000000,
        Currency = "USD", SettlementCurrency = "", Exchange = "XCME", SecurityType = "FUT", Cfi = "FFIXSX", UnitOfMeasure = "IPNT"
    };
    static ContractDetail Option() => Future() with
    {
        RawSymbol = "E2DU6 C6500", Ticker = "E2D", Underlying = "ESU6", Instrument = new(1, 42), UnderlyingInstrumentId = 42140870,
        ContractKind = ContractKind.CallOption, MaturityDate = new(2026, 9, 10), MaturityWeek = 2,
        ExpirationTimestampNanoseconds = 1789070400000000000, StrikePrice = 6500000000000,
        SecurityType = "OOF", Cfi = "OCEFPS"
    };
    [Fact]
    public void Reviewed_mapping_binds_exact_underlying_expiry_units_calendar_and_tick_version()
    {
        var (candidate, mapping) = ReviewedEsOptionReference.Create(Option(), Future());
        Assert.Equal("ES20260910C6500", candidate.ContractId);
        Assert.Equal("ES20260918", mapping.UnderlyingContractId);
        Assert.Equal(mapping.UnderlyingContractId, candidate.Definition.Underlying);
        Assert.Equal(new DateTimeOffset(2026, 9, 10, 20, 0, 0, TimeSpan.Zero), mapping.LastTradingUtc);
        Assert.Equal(6500m, candidate.Definition.StrikePrice); Assert.Equal(50m, mapping.Multiplier);
        Assert.Equal(PricingDayCount.Actual365Fixed, mapping.DayCount);
        Assert.Null(OptionPricingQualification.Validate(mapping, new(2026, 9, 8, 16, 0, 0, TimeSpan.Zero)));
        Assert.Equal(2, OptionPricingQualification.CountTradingDays(ReviewedEsOptionReference.Calendar, mapping, new(2026, 9, 8, 16, 0, 0, TimeSpan.Zero)));
        Assert.Equal(.10m, OptionPremiumTicks.GetIncrement(mapping, 10m));
        Assert.Equal(PricingSemanticHash.Compute(Option()), candidate.DefinitionDigest);
        Assert.DoesNotContain(new DateOnly(2026, 9, 7), ReviewedEsOptionReference.Calendar.TradingDates);
        Assert.Contains(new DateOnly(2026, 9, 8), ReviewedEsOptionReference.Calendar.TradingDates);
    }
    [Theory]
    [InlineData(100)] [InlineData(6500)] [InlineData(10000)] [InlineData(20000)]
    public void Published_option_id_roundtrips_through_the_domain_business_key(int strike)
    {
        var (candidate, mapping) = ReviewedEsOptionReference.Create(Option() with
        { RawSymbol = $"E2DU6 C{strike}", StrikePrice = strike * 1_000_000_000L }, Future());
        var key = new TomasAI.IFM.Domain.MarketData.Shared.FuturesOptionContractId(candidate.ContractId);
        Assert.Equal(strike, key.StrikePrice); Assert.Equal("ES", key.Symbol);
        Assert.Equal(candidate.ContractId, mapping.ContractId);
    }

    [Theory]
    [InlineData("american")] [InlineData("unknown")] [InlineData("underlying-id")] [InlineData("underlying-symbol")]
    [InlineData("currency")] [InlineData("multiplier")] [InlineData("expiry")] [InlineData("weekday")]
    [InlineData("coverage")] [InlineData("week")] [InlineData("fractional-strike")]
    public void Contradictory_or_unreviewed_definitions_cannot_be_published(string field)
    {
        var value = field switch
        {
            "american" => Option() with { Cfi = "OCAFPS" }, "unknown" => Option() with { Ticker = "ES" },
            "underlying-id" => Option() with { UnderlyingInstrumentId = 999 }, "underlying-symbol" => Option() with { Underlying = "ESZ6" },
            "currency" => Option() with { Currency = "CAD" }, "multiplier" => Option() with { ContractMultiplier = 5 },
            "expiry" => Option() with { ExpirationTimestampNanoseconds = 1789070400000000100 },
            "weekday" => Option() with { MaturityDate = new(2026, 9, 11) }, "coverage" => Option() with { MaturityDate = new(2026, 10, 8) },
            "week" => Option() with { MaturityWeek = 3 }, _ => Option() with { StrikePrice = 6500500000000 }
        };
        Assert.Throws<InvalidDataException>(() => ReviewedEsOptionReference.Create(value, Future()));
    }
}
