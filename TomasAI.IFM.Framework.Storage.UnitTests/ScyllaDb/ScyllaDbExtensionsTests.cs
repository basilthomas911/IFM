using System;
using Xunit;
using FluentAssertions;
using Cassandra;
using TomasAI.IFM.Framework.Storage.ScyllaDb;

namespace TomasAI.IFM.Framework.Storage.UnitTests.ScyllaDb;

public class ScyllaDbExtensionsTests
{
    // --- AsDateTime ---

    [Fact]
    public void AsDateTime_ConvertsDateOnlyToDateTime()
    {
        // Arrange
        var dateOnly = new DateOnly(2024, 6, 15);

        // Act
        var result = dateOnly.AsDateTime();

        // Assert
        result.Should().Be(new DateTime(2024, 6, 15, 0, 0, 0));
    }

    [Fact]
    public void AsDateTime_MinValue_ConvertsCorrectly()
    {
        // Arrange
        var dateOnly = DateOnly.MinValue;

        // Act
        var result = dateOnly.AsDateTime();

        // Assert
        result.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void AsDateTime_MaxValue_ConvertsCorrectly()
    {
        // Arrange
        var dateOnly = DateOnly.MaxValue;

        // Act
        var result = dateOnly.AsDateTime();

        // Assert
        result.Year.Should().Be(9999);
        result.Month.Should().Be(12);
        result.Day.Should().Be(31);
    }

    // --- AsDateOnly ---

    [Fact]
    public void AsDateOnly_ConvertsDateTimeToDateOnly()
    {
        // Arrange
        var dateTime = new DateTime(2024, 6, 15, 14, 30, 45);

        // Act
        var result = dateTime.AsDateOnly();

        // Assert
        result.Should().Be(new DateOnly(2024, 6, 15));
    }

    [Fact]
    public void AsDateOnly_StripsTimeComponent()
    {
        // Arrange
        var dateTime = new DateTime(2024, 1, 1, 23, 59, 59);

        // Act
        var result = dateTime.AsDateOnly();

        // Assert
        result.Should().Be(new DateOnly(2024, 1, 1));
    }

    // --- AsLocalDate ---

    [Fact]
    public void AsLocalDate_ConvertsDateTimeToLocalDate()
    {
        // Arrange
        var dateTime = new DateTime(2024, 6, 15);

        // Act
        var result = dateTime.AsLocalDate();

        // Assert
        result.Year.Should().Be(2024);
        result.Month.Should().Be(6);
        result.Day.Should().Be(15);
    }

    [Fact]
    public void AsLocalDate_IgnoresTimeComponent()
    {
        // Arrange
        var dateTime = new DateTime(2024, 12, 25, 10, 30, 0);

        // Act
        var result = dateTime.AsLocalDate();

        // Assert
        result.Year.Should().Be(2024);
        result.Month.Should().Be(12);
        result.Day.Should().Be(25);
    }

    // --- AsLocalTime ---

    [Fact]
    public void AsLocalTime_ConvertsDateTimeToLocalTime()
    {
        // Arrange
        var dateTime = new DateTime(2024, 6, 15, 14, 30, 45);

        // Act
        var result = dateTime.AsLocalTime();

        // Assert
        result.Hour.Should().Be(14);
        result.Minute.Should().Be(30);
        result.Second.Should().Be(45);
    }

    [Fact]
    public void AsLocalTime_MidnightConvertsCorrectly()
    {
        // Arrange
        var dateTime = new DateTime(2024, 1, 1, 0, 0, 0);

        // Act
        var result = dateTime.AsLocalTime();

        // Assert
        result.Hour.Should().Be(0);
        result.Minute.Should().Be(0);
        result.Second.Should().Be(0);
    }

    // --- AsDateTime roundtrip with AsDateOnly ---

    [Fact]
    public void AsDateOnly_ThenAsDateTime_RoundTripsDatePortion()
    {
        // Arrange
        var original = new DateTime(2024, 3, 15, 10, 30, 0);

        // Act
        var dateOnly = original.AsDateOnly();
        var roundTripped = dateOnly.AsDateTime();

        // Assert
        roundTripped.Year.Should().Be(2024);
        roundTripped.Month.Should().Be(3);
        roundTripped.Day.Should().Be(15);
        roundTripped.Hour.Should().Be(0);
        roundTripped.Minute.Should().Be(0);
    }

    // --- AsLocalDate roundtrip ---

    [Fact]
    public void AsLocalDate_RoundTripPreservesDateComponents()
    {
        // Arrange
        var dateTime = new DateTime(2024, 7, 4);

        // Act
        var localDate = dateTime.AsLocalDate();
        var roundTripped = new DateTime(localDate.Year, localDate.Month, localDate.Day);

        // Assert
        roundTripped.Should().Be(dateTime);
    }

    // --- AsMilliseconds ---

    [Fact]
    public void AsMilliseconds_ReturnsMillisecondComponent()
    {
        // Arrange
        var localTime = new LocalTime(14, 30, 45, 123000000);

        // Act
        var result = localTime.AsMilliseconds();

        // Assert
        result.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void AsMilliseconds_ZeroNanoseconds_ReturnsZero()
    {
        // Arrange
        var localTime = new LocalTime(0, 0, 0, 0);

        // Act
        var result = localTime.AsMilliseconds();

        // Assert
        result.Should().Be(0);
    }

    // --- AsMicroseconds ---

    [Fact]
    public void AsMicroseconds_ReturnsMicrosecondComponent()
    {
        // Arrange
        var localTime = new LocalTime(14, 30, 45, 123456000);

        // Act
        var result = localTime.AsMicroseconds();

        // Assert
        result.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void AsMicroseconds_ZeroNanoseconds_ReturnsZero()
    {
        // Arrange
        var localTime = new LocalTime(0, 0, 0, 0);

        // Act
        var result = localTime.AsMicroseconds();

        // Assert
        result.Should().Be(0);
    }

    // --- Edge cases ---

    [Fact]
    public void AsDateOnly_LeapYearDate_ConvertsCorrectly()
    {
        // Arrange
        var dateTime = new DateTime(2024, 2, 29);

        // Act
        var result = dateTime.AsDateOnly();

        // Assert
        result.Should().Be(new DateOnly(2024, 2, 29));
    }

    [Fact]
    public void AsLocalDate_LeapYearDate_ConvertsCorrectly()
    {
        // Arrange
        var dateTime = new DateTime(2024, 2, 29);

        // Act
        var result = dateTime.AsLocalDate();

        // Assert
        result.Year.Should().Be(2024);
        result.Month.Should().Be(2);
        result.Day.Should().Be(29);
    }
}
