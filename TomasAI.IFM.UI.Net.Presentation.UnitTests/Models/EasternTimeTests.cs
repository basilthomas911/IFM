using FluentAssertions;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Models;

public class EasternTimeTests
{
    [Theory]
    [InlineData(2026, 1, 15, 17, 30, 12, 30)]
    [InlineData(2026, 7, 15, 17, 30, 13, 30)]
    public void FromUtc_AppliesEasternStandardAndDaylightOffsets(
        int year,
        int month,
        int day,
        int utcHour,
        int minute,
        int expectedHour,
        int expectedMinute)
    {
        var backendUtc = new DateTime(year, month, day, utcHour, minute, 0, DateTimeKind.Utc);

        var eastern = EasternTime.FromUtc(backendUtc);

        eastern.Should().Be(new DateTime(year, month, day, expectedHour, expectedMinute, 0));
        eastern.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    [Fact]
    public void FromUtc_TreatsUnspecifiedBackendValueAsUtc()
    {
        var wireValue = new DateTime(2026, 7, 15, 17, 30, 0, DateTimeKind.Unspecified);

        EasternTime.FromUtc(wireValue)
            .Should().Be(new DateTime(2026, 7, 15, 13, 30, 0));
    }

    [Fact]
    public void FromUtc_TreatsBackendTicksAsUtcEvenWhenKindWasMarkedLocal()
    {
        var incorrectlyMarkedWireValue = new DateTime(
            2026,
            7,
            15,
            17,
            30,
            0,
            DateTimeKind.Local);

        EasternTime.FromUtc(incorrectlyMarkedWireValue)
            .Should().Be(new DateTime(2026, 7, 15, 13, 30, 0));
    }

    [Theory]
    [InlineData(2026, 1, 15, 12, 30, 17, 30)]
    [InlineData(2026, 7, 15, 13, 30, 17, 30)]
    public void ToUtc_AppliesEasternStandardAndDaylightOffsets(
        int year,
        int month,
        int day,
        int easternHour,
        int minute,
        int expectedUtcHour,
        int expectedUtcMinute)
    {
        var uiTime = new DateTime(year, month, day, easternHour, minute, 0, DateTimeKind.Unspecified);

        var utc = EasternTime.ToUtc(uiTime);

        utc.Should().Be(new DateTime(year, month, day, expectedUtcHour, expectedUtcMinute, 0, DateTimeKind.Utc));
        utc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void ToUtc_DoesNotConvertAnAlreadyUtcValueTwice()
    {
        var utc = new DateTime(2026, 7, 15, 17, 30, 0, DateTimeKind.Utc);

        EasternTime.ToUtc(utc).Should().Be(utc);
    }

    [Fact]
    public void ToUtc_RejectsNonexistentSpringForwardTime()
    {
        var nonexistentEasternTime = new DateTime(2026, 3, 8, 2, 30, 0);

        var action = () => EasternTime.ToUtc(nonexistentEasternTime);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*does not exist because of the daylight-saving transition*");
    }

    [Fact]
    public void DateTimeOffsetConversion_PreservesTheInstant()
    {
        var backendUtc = new DateTimeOffset(2026, 7, 15, 17, 30, 0, TimeSpan.Zero);

        var eastern = EasternTime.FromUtc(backendUtc);

        eastern.Offset.Should().Be(TimeSpan.FromHours(-4));
        eastern.Hour.Should().Be(13);
        EasternTime.ToUtc(eastern).Should().Be(backendUtc);
    }
}
