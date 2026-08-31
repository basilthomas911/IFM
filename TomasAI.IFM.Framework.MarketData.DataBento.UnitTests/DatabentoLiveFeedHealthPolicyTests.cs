using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class DatabentoLiveFeedHealthPolicyTests
{
    static readonly DateTimeOffset Now = new(2026, 8, 31, 14, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, DatabentoLiveFeedHealthState.Green)]
    [InlineData(5, DatabentoLiveFeedHealthState.Green)]
    [InlineData(6, DatabentoLiveFeedHealthState.Yellow)]
    [InlineData(15, DatabentoLiveFeedHealthState.Yellow)]
    [InlineData(16, DatabentoLiveFeedHealthState.Red)]
    public void Active_route_uses_exact_five_and_fifteen_minute_boundaries(
        int ageMinutes,
        DatabentoLiveFeedHealthState expected)
    {
        var timestamp = Now.AddMinutes(-ageMinutes);
        var actual = DatabentoLiveFeedHealthPolicy.Evaluate(
            true, Now.AddHours(-1), timestamp, timestamp, Now);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Inactive_route_is_never_red()
    {
        Assert.Equal(
            DatabentoLiveFeedHealthState.Inactive,
            DatabentoLiveFeedHealthPolicy.Evaluate(false, null, null, null, Now));
    }

    [Fact]
    public void Old_backlog_record_does_not_make_route_green()
    {
        Assert.Equal(
            DatabentoLiveFeedHealthState.Red,
            DatabentoLiveFeedHealthPolicy.Evaluate(
                true,
                Now.AddHours(-1),
                Now,
                Now.AddMinutes(-20),
                Now));
    }

    [Fact]
    public void Route_without_data_ages_from_activation()
    {
        Assert.Equal(
            DatabentoLiveFeedHealthState.Yellow,
            DatabentoLiveFeedHealthPolicy.Evaluate(
                true,
                Now.AddMinutes(-10),
                null,
                null,
                Now));
    }
}
