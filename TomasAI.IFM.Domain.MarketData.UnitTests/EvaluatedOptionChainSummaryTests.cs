using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Query;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public class EvaluatedOptionChainSummaryTests
{
    static EvaluatedOptionContractReadModel Leg(string id, decimal strike, bool call, double iv) =>
        new(id, strike, call, null, null, null, null, null, null, iv, null, null, null, null, null, null,
            null, null, false, false, null, null, null);

    [Fact]
    public void Uses_current_session_open_nearest_strike_pair_and_operational_date_for_summary()
    {
        var date = new DateOnly(2026, 10, 23);
        var chain = new EvaluatedOptionChainReadModel("ES", date.AddDays(7), 6000, null, null, "test", DateTimeOffset.UtcNow,
            [Leg("p", 6000, false, .2), Leg("c", 6000, true, .3), Leg("far", 6100, true, .8)])
            { LastUnderlyingPrice = 6050, PreviousSessionClose = 6000 };
        var result = EvaluatedOptionChainSummary.Apply(chain, date, sessionOpen: 6100);
        result.AtmImpliedVolatility.Should().Be(.25m);
        result.DailyPriceChange.Should().Be(-50);
        result.DailyPriceChangeFraction.Should().Be(-50m / 6100);
        result.DaysToExpiry.Should().Be(7);
    }

    [Fact]
    public void Does_not_fabricate_zero_for_missing_price_or_invalid_volatility()
    {
        var date = new DateOnly(2026, 10, 23);
        var result = EvaluatedOptionChainSummary.Apply(new("ES", date, 6000, null, null, "test", DateTimeOffset.UtcNow,
            [Leg("invalid", 6000, true, double.NaN)]), date, sessionOpen: 6100);
        result.AtmImpliedVolatility.Should().BeNull(); result.DailyPriceChange.Should().BeNull();
        result.DailyPriceChangeFraction.Should().BeNull(); result.DaysToExpiry.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Leaves_change_unavailable_without_valid_session_open(int? sessionOpen)
    {
        var date = new DateOnly(2026, 10, 23);
        var chain = new EvaluatedOptionChainReadModel("ES", date, 6000, null, null, "test", DateTimeOffset.UtcNow, [])
            { LastUnderlyingPrice = 6050, PreviousSessionClose = 6000 };

        var result = EvaluatedOptionChainSummary.Apply(chain, date, sessionOpen);

        result.DailyPriceChange.Should().BeNull();
        result.DailyPriceChangeFraction.Should().BeNull();
    }
}
