using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Query;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using Xunit;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public sealed class GetValueDateTests
{
    [Fact]
    public async Task Query_returns_operational_value_date_while_market_is_closed()
    {
        var timeProvider = new SettableTimeProvider(
            DateTimeOffset.Parse("2026-08-14T17:00:00-04:00"));
        var authority = new FuturesMarketSessionAuthority(
            timeProvider,
            new FuturesValueDateProvider(timeProvider));

        var result = await new GetValueDateQuery().ExecuteAsync(authority);

        result.Should().NotBeNull();
        result.Value.Should().Be(new DateOnly(2026, 8, 14));
        authority.Current.ActiveValueDate.Should().BeNull();
    }

    [Theory]
    [InlineData("2026-08-10T16:59:59-04:00", "2026-08-10")]
    [InlineData("2026-08-10T17:00:00-04:00", "2026-08-10")]
    [InlineData("2026-08-14T16:59:59-04:00", "2026-08-14")]
    [InlineData("2026-08-14T17:00:00-04:00", "2026-08-14")]
    [InlineData("2026-08-16T17:59:59-04:00", "2026-08-14")]
    [InlineData("2026-08-16T18:00:00-04:00", "2026-08-17")]
    public void Provider_never_returns_default_at_session_boundaries(
        string instant,
        string expected)
    {
        var timeProvider = new SettableTimeProvider(DateTimeOffset.Parse(instant));
        var provider = new FuturesValueDateProvider(timeProvider);

        provider.ValueDate.Should().Be(DateOnly.Parse(expected));
        provider.ValueDate.Should().NotBe(default);
    }

    [Theory]
    [InlineData(2026, 8, 8, 12, "2026-08-07")]
    [InlineData(2026, 8, 9, 17, "2026-08-07")]
    [InlineData(2026, 8, 9, 18, "2026-08-10")]
    [InlineData(2026, 8, 10, 16, "2026-08-10")]
    [InlineData(2026, 8, 10, 17, "2026-08-10")]
    [InlineData(2026, 8, 10, 18, "2026-08-11")]
    [InlineData(2026, 8, 31, 18, "2026-09-01")]
    [InlineData(2026, 8, 14, 16, "2026-08-14")]
    [InlineData(2026, 8, 14, 17, "2026-08-14")]
    [InlineData(2026, 8, 14, 18, "2026-08-14")]
    public void CalculateValueDate_UsesFuturesMarketSessionBoundary(
        int year,
        int month,
        int day,
        int hour,
        string expectedDate)
    {
        var result = GetValueDate.CalculateValueDate(new DateTime(year, month, day, hour, 0, 0));

        result.Should().NotBeNull();
        result.Value.Should().Be(DateOnly.Parse(expectedDate));
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
    public void BootstrapValueDate_RetainsTheEndedSessionDuringClosedHours(
        string instant,
        string expected)
        => FuturesTradingValueDate.GetOperational(DateTimeOffset.Parse(instant))
            .Should().Be(DateOnly.Parse(expected));

    [Theory]
    [InlineData("2026-08-08T16:00:00-04:00", "2026-08-07", null, false, FuturesMarketState.Closed)]
    [InlineData("2026-08-09T17:59:59-04:00", "2026-08-07", null, false, FuturesMarketState.Closed)]
    [InlineData("2026-08-09T18:00:00-04:00", "2026-08-10", "2026-08-10", true, FuturesMarketState.OffTrading)]
    [InlineData("2026-08-10T02:59:59-04:00", "2026-08-10", "2026-08-10", true, FuturesMarketState.OffTrading)]
    [InlineData("2026-08-10T03:00:00-04:00", "2026-08-10", "2026-08-10", true, FuturesMarketState.LiveTrading)]
    [InlineData("2026-08-10T15:59:59-04:00", "2026-08-10", "2026-08-10", true, FuturesMarketState.LiveTrading)]
    [InlineData("2026-08-10T16:00:00-04:00", "2026-08-10", "2026-08-10", true, FuturesMarketState.LiveTrading)]
    [InlineData("2026-08-10T16:59:59-04:00", "2026-08-10", "2026-08-10", true, FuturesMarketState.LiveTrading)]
    [InlineData("2026-08-10T17:00:00-04:00", "2026-08-10", null, false, FuturesMarketState.Closed)]
    [InlineData("2026-08-10T18:00:00-04:00", "2026-08-11", "2026-08-11", true, FuturesMarketState.OffTrading)]
    [InlineData("2026-08-14T17:00:00-04:00", "2026-08-14", null, false, FuturesMarketState.Closed)]
    public void MarketSession_SeparatesOperationalAndLiveValueDates(
        string instant,
        string operational,
        string? active,
        bool isOpen,
        FuturesMarketState expectedState)
    {
        var result = GetMarketSession.Calculate(DateTimeOffset.Parse(instant));

        result.IsValid.Should().BeTrue();
        result.OperationalValueDate.Should().Be(DateOnly.Parse(operational));
        result.ActiveValueDate.Should().Be(active is null ? null : DateOnly.Parse(active));
        result.IsMarketOpen.Should().Be(isOpen);
        result.State.Should().Be(expectedState);
        result.IsLiveTrading.Should().Be(expectedState == FuturesMarketState.LiveTrading);
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
    public void Operational_date_does_not_advance_on_close_open_midnight_or_failed_eod()
    {
        var clock = new SettableTimeProvider(DateTimeOffset.Parse("2026-08-31T16:59:00-04:00"));
        var provider = new FuturesValueDateProvider(clock);
        var authority = new FuturesMarketSessionAuthority(clock, provider);
        foreach (var instant in new[] { "2026-08-31T17:00:00-04:00", "2026-08-31T18:00:00-04:00", "2026-09-01T00:00:00-04:00" })
        {
            clock.UtcNow = DateTimeOffset.Parse(instant);
            authority.Refresh().OperationalValueDate.Should().Be(new DateOnly(2026,8,31));
        }
        authority.Current.IsEndOfDayPending.Should().BeTrue();
        authority.Current.ActiveValueDate.Should().BeNull();
        // No successful completion event is applied for a rejected or failed EOD command.
        authority.ApplyCompletedEndOfDay(new DateOnly(2026,8,31)).Should().BeTrue();
        authority.Current.OperationalValueDate.Should().Be(new DateOnly(2026,9,1));
        authority.Current.IsEndOfDayPending.Should().BeFalse();
        authority.Current.ActiveValueDate.Should().Be(new DateOnly(2026,9,1));
        var revision = authority.Current.Revision;
        authority.ApplyCompletedEndOfDay(new DateOnly(2026,8,31)).Should().BeFalse();
        authority.Refresh().Revision.Should().Be(revision);
    }

    [Fact]
    public void Friday_completion_advances_to_monday_and_restart_restores_persisted_date()
    {
        var clock = new SettableTimeProvider(DateTimeOffset.Parse("2026-08-14T17:01:00-04:00"));
        var authority = new FuturesMarketSessionAuthority(clock);
        authority.Current.OperationalValueDate.Should().Be(new DateOnly(2026,8,14));
        authority.ApplyCompletedEndOfDay(new DateOnly(2026,8,14));
        authority.Current.OperationalValueDate.Should().Be(new DateOnly(2026,8,17));
        clock.UtcNow = DateTimeOffset.Parse("2026-08-18T12:00:00-04:00");
        var restarted = new FuturesMarketSessionAuthority(clock);
        restarted.ApplyCompletedEndOfDay(new DateOnly(2026,8,14));
        restarted.Current.OperationalValueDate.Should().Be(new DateOnly(2026,8,17), "unconfirmed later sessions cannot advance the persisted operational date");
        restarted.ApplyCompletedEndOfDay(new DateOnly(2026,8,13)).Should().BeFalse();
    }

    [Fact]
    public void MarketSessionDecision_RoundTripsItsExplicitStateOverMessagePack()
    {
        var source = GetMarketSession.Calculate(
            DateTimeOffset.Parse("2026-08-10T03:00:00-04:00")) with
        {
            Revision = 17,
            AsOfUtc = new DateTime(2026, 8, 10, 7, 0, 0, DateTimeKind.Utc)
        };

        var restored = MessagePackSerializer.Deserialize<MarketSessionReadModel>(
            MessagePackSerializer.Serialize(source));

        restored.Should().BeEquivalentTo(source);
        restored.State.Should().Be(FuturesMarketState.LiveTrading);
        restored.IsValid.Should().BeTrue();
        new MarketSessionReadModel().IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("2026-08-10T02:59:59-04:00", "2026-08-10T07:00:00Z")]
    [InlineData("2026-08-10T03:00:00-04:00", "2026-08-10T21:00:00Z")]
    [InlineData("2026-08-10T15:59:59-04:00", "2026-08-10T21:00:00Z")]
    [InlineData("2026-08-10T16:00:00-04:00", "2026-08-10T21:00:00Z")]
    [InlineData("2026-08-10T17:00:00-04:00", "2026-08-10T22:00:00Z")]
    [InlineData("2026-08-10T18:00:00-04:00", "2026-08-11T07:00:00Z")]
    public void MarketSessionDecision_ReportsEveryPermissionAndLifecycleBoundary(
        string instant,
        string expectedTransition)
        => GetMarketSession.Calculate(DateTimeOffset.Parse(instant)).NextTransitionUtc
            .Should().Be(DateTime.Parse(expectedTransition).ToUniversalTime());

    sealed class SettableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
