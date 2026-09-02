using FluentAssertions;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class FuturesRolloverPreparationPolicyTests
{
    [Fact]
    public void MondayEffectiveDatePreparesOnPrecedingFriday()
    {
        var calendar = new CmeFuturesMarketSessionCalendar();

        calendar.GetPreparationDate(new DateOnly(2026, 9, 14))
            .Should().Be(new DateOnly(2026, 9, 11));
        FuturesRolloverPreparationPolicy.TryResolveTargetValueDate(
                Eastern(new DateOnly(2026, 9, 11), new TimeOnly(17, 30)),
                calendar,
                out var target)
            .Should().BeTrue();
        target.Should().Be(new DateOnly(2026, 9, 14));
    }

    [Fact]
    public void ExplicitClosuresAndConsecutiveClosuresAreSkipped()
    {
        var calendar = new CmeFuturesMarketSessionCalendar([
            new DateOnly(2026, 12, 24),
            new DateOnly(2026, 12, 25)]);

        calendar.GetPreparationDate(new DateOnly(2026, 12, 28))
            .Should().Be(new DateOnly(2026, 12, 23));
        calendar.NextBusinessDay(new DateOnly(2026, 12, 23))
            .Should().Be(new DateOnly(2026, 12, 28));
    }

    [Theory]
    [InlineData(16, 59, false)]
    [InlineData(17, 0, true)]
    [InlineData(17, 59, true)]
    [InlineData(18, 0, false)]
    public void PreparationWindowUsesDstSafeEasternBoundaries(
        int hour,
        int minute,
        bool expected)
    {
        var calendar = new CmeFuturesMarketSessionCalendar();

        FuturesRolloverPreparationPolicy.TryResolveTargetValueDate(
                Eastern(new DateOnly(2026, 3, 9), new TimeOnly(hour, minute)),
                calendar,
                out _)
            .Should().Be(expected);
    }

    [Fact]
    public void MissedPreparationIsDueOnOrAfterEffectiveValueDate()
    {
        FuturesRolloverPreparationPolicy.IsDue(
                new DateOnly(2026, 9, 18), new DateOnly(2026, 9, 18))
            .Should().BeTrue();
        FuturesRolloverPreparationPolicy.IsDue(
                new DateOnly(2026, 9, 17), new DateOnly(2026, 9, 18))
            .Should().BeFalse();
    }

    static DateTimeOffset Eastern(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(local, FuturesTradingValueDate.MarketTimeZone);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }
}
