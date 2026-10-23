using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Query;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests;

public class FuturesBarWindowPartitionsTests
{
    [Fact]
    public void Includes_ended_and_new_sessions_in_one_window_across_18_Eastern()
    {
        var end = new DateTime(2026,10,7,22,15,0,DateTimeKind.Utc);
        FuturesBarWindowPartitions.Get(new(2026,10,8), end.AddHours(-6), end)
            .Should().Equal(new DateOnly(2026,10,7), new DateOnly(2026,10,8));
    }

    [Fact]
    public void Includes_session_partition_after_midnight_and_rejects_unbounded_or_local_windows()
    {
        var end = new DateTime(2026,10,8,5,0,0,DateTimeKind.Utc);
        FuturesBarWindowPartitions.Get(new(2026,10,8), end.AddHours(-6), end)
            .Should().Equal(new DateOnly(2026,10,7), new DateOnly(2026,10,8));
        var invalid = () => FuturesBarWindowPartitions.Get(new(2026,10,8), end.AddDays(-400), end);
        invalid.Should().Throw<ArgumentException>();
        var local = () => FuturesBarWindowPartitions.Get(new(2026,10,8), DateTime.SpecifyKind(end, DateTimeKind.Local), end);
        local.Should().Throw<ArgumentException>();
    }
}
