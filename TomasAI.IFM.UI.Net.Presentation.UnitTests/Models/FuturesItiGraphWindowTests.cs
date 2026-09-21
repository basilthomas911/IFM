using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.UI.Net.Models.Operations;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Models;

public sealed class FuturesItiGraphWindowTests
{
    static readonly DateTimeOffset WednesdayNow =
        new(2026, 9, 16, 17, 45, 0, TimeSpan.Zero);
    static readonly DateOnly ValueDate = new(2026, 9, 16);

    [Fact]
    public void Daily_IsTheRollingEightHoursEndingNow()
    {
        var window = FuturesItiGraphWindow.Resolve(WednesdayNow, ValueDate, TimeFrameType.Daily);

        window.StartUtc.Should().Be(new DateTime(2026, 9, 16, 9, 45, 0, DateTimeKind.Utc));
        window.EndUtc.Should().Be(WednesdayNow.UtcDateTime);
        window.Contains(window.StartUtc).Should().BeTrue();
        window.Contains(window.StartUtc.AddTicks(-1)).Should().BeFalse();
    }

    [Fact]
    public void Weekly_CoversSevenCalendarDatesEndingOnValueDate()
    {
        var window = FuturesItiGraphWindow.Resolve(WednesdayNow, ValueDate, TimeFrameType.Weekly);

        window.StartUtc.Should().Be(new DateTime(2026, 9, 10, 4, 0, 0, DateTimeKind.Utc));
        window.EndUtc.Should().Be(WednesdayNow.UtcDateTime);
    }

    [Fact]
    public void Monthly_StartsAtEasternMonthBeginningAndEndsNow()
    {
        var window = FuturesItiGraphWindow.Resolve(WednesdayNow, ValueDate, TimeFrameType.Monthly);

        window.StartUtc.Should().Be(new DateTime(2026, 9, 1, 4, 0, 0, DateTimeKind.Utc));
        window.EndUtc.Should().Be(WednesdayNow.UtcDateTime);
    }

    [Fact]
    public void UnsupportedTimeFrame_IsRejected()
        => FluentActions.Invoking(() =>
                FuturesItiGraphWindow.Resolve(WednesdayNow, ValueDate, TimeFrameType.OneMinute))
            .Should().Throw<ArgumentOutOfRangeException>();
}
