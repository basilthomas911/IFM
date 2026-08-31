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
    [InlineData(2026, 8, 10, 17, null)]
    [InlineData(2026, 8, 10, 18, "2026-08-11")]
    [InlineData(2026, 8, 31, 18, "2026-09-01")]
    [InlineData(2026, 8, 14, 16, "2026-08-14")]
    [InlineData(2026, 8, 14, 17, null)]
    [InlineData(2026, 8, 14, 18, null)]
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
    [InlineData("2026-03-09T20:59:59+00:00", "2026-03-09")]
    [InlineData("2026-03-09T22:00:00+00:00", "2026-03-10")]
    [InlineData("2026-11-02T21:59:59+00:00", "2026-11-02")]
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
    [InlineData("2026-08-10T17:00:00-04:00", "2026-08-10")]
    [InlineData("2026-08-14T18:00:00-04:00", "2026-08-14")]
    public void OperationalValueDate_UsesMostRecentFridayWhileWeekendIsClosed(
        string instant,
        string expected)
        => FuturesTradingValueDate.GetOperational(DateTimeOffset.Parse(instant))
            .Should().Be(DateOnly.Parse(expected));

    [Theory]
    [InlineData("2026-08-08T16:00:00-04:00", "2026-08-07", null, false)]
    [InlineData("2026-08-09T17:59:59-04:00", "2026-08-07", null, false)]
    [InlineData("2026-08-09T18:00:00-04:00", "2026-08-10", "2026-08-10", true)]
    [InlineData("2026-08-10T16:59:59-04:00", "2026-08-10", "2026-08-10", true)]
    [InlineData("2026-08-10T17:00:00-04:00", "2026-08-10", null, false)]
    [InlineData("2026-08-10T18:00:00-04:00", "2026-08-11", "2026-08-11", true)]
    [InlineData("2026-08-14T17:00:00-04:00", "2026-08-14", null, false)]
    public void MarketSession_SeparatesOperationalAndLiveValueDates(
        string instant,
        string operational,
        string? active,
        bool isOpen)
    {
        var result = GetMarketSession.Calculate(DateTimeOffset.Parse(instant));

        result.IsValid.Should().BeTrue();
        result.OperationalValueDate.Should().Be(DateOnly.Parse(operational));
        result.ActiveValueDate.Should().Be(active is null ? null : DateOnly.Parse(active));
        result.IsLiveSessionOpen.Should().Be(isOpen);
        result.SessionEndUtc.Should().BeAfter(result.SessionStartUtc);
        result.NextTransitionUtc.Should().BeAfter(DateTimeOffset.Parse(instant).UtcDateTime);
    }

    [Theory]
    [InlineData("2026-08-09T17:59:59-04:00", "2026-08-09T22:00:00+00:00")]
    [InlineData("2026-08-09T18:00:00-04:00", "2026-08-10T21:00:00+00:00")]
    [InlineData("2026-08-10T16:59:59-04:00", "2026-08-10T21:00:00+00:00")]
    [InlineData("2026-08-10T17:00:00-04:00", "2026-08-10T22:00:00+00:00")]
    [InlineData("2026-08-10T18:00:00-04:00", "2026-08-11T21:00:00+00:00")]
    [InlineData("2026-08-14T17:00:00-04:00", "2026-08-16T22:00:00+00:00")]
    public void NextTransitionUtc_UsesMarketOpenAndCloseBoundaries(
        string instant,
        string expected)
        => FuturesTradingValueDate.GetNextTransitionUtc(DateTimeOffset.Parse(instant))
            .Should().Be(DateTimeOffset.Parse(expected));

    [Theory]
    [InlineData("2026-08-18", "2026-08-17T22:00:00+00:00")]
    [InlineData("2026-11-03", "2026-11-02T23:00:00+00:00")]
    public void SessionStartUtc_UsesPreviousDayAtSixPmEastern(
        string valueDate,
        string expectedUtc)
        => FuturesTradingValueDate.GetSessionStartUtc(DateOnly.Parse(valueDate))
            .Should().Be(DateTimeOffset.Parse(expectedUtc));

    [Theory]
    [InlineData("2026-08-18", "2026-08-18T21:00:00+00:00")]
    [InlineData("2026-11-03", "2026-11-03T22:00:00+00:00")]
    public void SessionEndUtc_UsesValueDateAtFivePmEastern(
        string valueDate,
        string expectedUtc)
        => FuturesTradingValueDate.GetSessionEndUtc(DateOnly.Parse(valueDate))
            .Should().Be(DateTimeOffset.Parse(expectedUtc));

    [Fact]
    public void MarketSessionAuthority_InitializesFromApiClockAndAdvancesOnceAtRollover()
    {
        var timeProvider = new SettableTimeProvider(
            DateTimeOffset.Parse("2026-08-31T17:59:00-04:00"));
        var authority = new FuturesMarketSessionAuthority(timeProvider);

        authority.Current.OperationalValueDate.Should().Be(new DateOnly(2026, 8, 31));
        authority.Current.ActiveValueDate.Should().BeNull();
        authority.Current.Revision.Should().Be(1);

        timeProvider.UtcNow = DateTimeOffset.Parse("2026-08-31T18:00:00-04:00");
        var rolled = authority.Refresh();
        var reconciled = authority.Refresh();

        rolled.OperationalValueDate.Should().Be(new DateOnly(2026, 9, 1));
        rolled.ActiveValueDate.Should().Be(new DateOnly(2026, 9, 1));
        rolled.Revision.Should().Be(2);
        reconciled.Revision.Should().Be(2);
        reconciled.AsOfUtc.Should().Be(timeProvider.UtcNow.UtcDateTime);
    }

    sealed class SettableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
