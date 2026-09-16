using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.UI.Net.Models.Operations;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Models;

public sealed class FuturesItiGraphWindowTests
{
    static readonly DateTimeOffset WednesdayNow =
        new(2026, 9, 16, 17, 45, 0, TimeSpan.Zero);

    [Fact]
    public void Daily_IsTheRollingEightHoursEndingNow()
    {
        var window = FuturesItiGraphWindow.Resolve(WednesdayNow, TimeFrameType.Daily);

        window.StartUtc.Should().Be(new DateTime(2026, 9, 16, 9, 45, 0, DateTimeKind.Utc));
        window.EndUtc.Should().Be(WednesdayNow.UtcDateTime);
        window.Contains(window.StartUtc).Should().BeTrue();
        window.Contains(window.StartUtc.AddTicks(-1)).Should().BeFalse();
    }

    [Fact]
    public void Weekly_StartsAtEasternMondayAndEndsNow()
    {
        var window = FuturesItiGraphWindow.Resolve(WednesdayNow, TimeFrameType.Weekly);

        window.StartUtc.Should().Be(new DateTime(2026, 9, 14, 4, 0, 0, DateTimeKind.Utc));
        window.EndUtc.Should().Be(WednesdayNow.UtcDateTime);
    }

    [Fact]
    public void Monthly_StartsAtEasternMonthBeginningAndEndsNow()
    {
        var window = FuturesItiGraphWindow.Resolve(WednesdayNow, TimeFrameType.Monthly);

        window.StartUtc.Should().Be(new DateTime(2026, 9, 1, 4, 0, 0, DateTimeKind.Utc));
        window.EndUtc.Should().Be(WednesdayNow.UtcDateTime);
    }

    [Fact]
    public void UnsupportedTimeFrame_IsRejected()
        => FluentActions.Invoking(() =>
                FuturesItiGraphWindow.Resolve(WednesdayNow, TimeFrameType.OneMinute))
            .Should().Throw<ArgumentOutOfRangeException>();
}
