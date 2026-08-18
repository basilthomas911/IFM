using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Query;
using TomasAI.IFM.Domain.MarketData.Shared;
using Xunit;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public sealed class GetValueDateTests
{
    [Theory]
    [InlineData(2026, 8, 8, 12, null)]
    [InlineData(2026, 8, 9, 17, null)]
    [InlineData(2026, 8, 9, 18, "2026-08-10")]
    [InlineData(2026, 8, 10, 16, "2026-08-10")]
    [InlineData(2026, 8, 10, 18, "2026-08-11")]
    [InlineData(2026, 8, 14, 18, "2026-08-14")]
    public void CalculateValueDate_UsesFuturesMarketSessionBoundary(
        int year,
        int month,
        int day,
        int hour,
        string? expectedDate)
    {
        var result = GetValueDate.CalculateValueDate(new DateTime(year, month, day, hour, 0, 0));

        if (expectedDate is null)
        {
            result.Should().BeNull();
            return;
        }

        result.Should().NotBeNull();
        result!.Value.Should().Be(DateOnly.Parse(expectedDate));
    }

    [Theory]
    [InlineData("2026-03-09T21:59:59+00:00", "2026-03-09")]
    [InlineData("2026-03-09T22:00:00+00:00", "2026-03-10")]
    [InlineData("2026-11-02T22:59:59+00:00", "2026-11-02")]
    [InlineData("2026-11-02T23:00:00+00:00", "2026-11-03")]
    public void FuturesTradingValueDate_UsesEasternTimeAcrossDaylightSavingTime(
        string instant,
        string expected)
    {
        FuturesTradingValueDate.TryGet(DateTimeOffset.Parse(instant), out var valueDate)
            .Should().BeTrue();
        valueDate.Should().Be(DateOnly.Parse(expected));
    }

    [Theory]
    [InlineData("2026-08-08T16:00:00-04:00", "2026-08-07")]
    [InlineData("2026-08-09T17:59:59-04:00", "2026-08-07")]
    public void OperationalValueDate_UsesMostRecentFridayWhileWeekendIsClosed(
        string instant,
        string expected)
        => FuturesTradingValueDate.GetOperational(DateTimeOffset.Parse(instant))
            .Should().Be(DateOnly.Parse(expected));

    [Theory]
    [InlineData("2026-08-18", "2026-08-17T22:00:00+00:00")]
    [InlineData("2026-11-03", "2026-11-02T23:00:00+00:00")]
    public void SessionStartUtc_UsesPreviousDayAtSixPmEastern(
        string valueDate,
        string expectedUtc)
        => FuturesTradingValueDate.GetSessionStartUtc(DateOnly.Parse(valueDate))
            .Should().Be(DateTimeOffset.Parse(expectedUtc));
}
