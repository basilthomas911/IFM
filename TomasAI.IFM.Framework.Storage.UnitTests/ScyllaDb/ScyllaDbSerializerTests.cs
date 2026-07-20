using System;
using Cassandra;
using Cassandra.Serialization;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.ScyllaDb;

namespace TomasAI.IFM.Framework.Storage.UnitTests.ScyllaDb;

public class ScyllaDbSerializerTests
{
    // --- DateOnlyToLocalDateTypeSerializer ---

    [Fact]
    public void DateOnlySerializer_CqlType_IsDate()
    {
        // Arrange
        var serializer = new DateOnlyToLocalDateTypeSerializer();

        // Act & Assert
        serializer.CqlType.Should().Be(ColumnTypeCode.Date);
    }

    [Fact]
    public void DateOnlySerializer_Serialize_ThenDeserialize_RoundTrips()
    {
        // Arrange
        var serializer = new DateOnlyToLocalDateTypeSerializer();
        var original = new DateOnly(2024, 6, 15);
        ushort protocolVersion = 4;

        // Act
        var bytes = serializer.Serialize(protocolVersion, original);
        var result = serializer.Deserialize(protocolVersion, bytes, 0, bytes.Length, null);

        // Assert
        result.Should().Be(original);
    }

    [Fact]
    public void DateOnlySerializer_Serialize_MinValue_RoundTrips()
    {
        // Arrange
        var serializer = new DateOnlyToLocalDateTypeSerializer();
        var original = new DateOnly(1, 1, 1);
        ushort protocolVersion = 4;

        // Act
        var bytes = serializer.Serialize(protocolVersion, original);
        var result = serializer.Deserialize(protocolVersion, bytes, 0, bytes.Length, null);

        // Assert
        result.Should().Be(original);
    }

    [Fact]
    public void DateOnlySerializer_Serialize_LeapYear_RoundTrips()
    {
        // Arrange
        var serializer = new DateOnlyToLocalDateTypeSerializer();
        var original = new DateOnly(2024, 2, 29);
        ushort protocolVersion = 4;

        // Act
        var bytes = serializer.Serialize(protocolVersion, original);
        var result = serializer.Deserialize(protocolVersion, bytes, 0, bytes.Length, null);

        // Assert
        result.Should().Be(original);
    }

    [Fact]
    public void DateOnlySerializer_Serialize_EndOfYear_RoundTrips()
    {
        // Arrange
        var serializer = new DateOnlyToLocalDateTypeSerializer();
        var original = new DateOnly(2024, 12, 31);
        ushort protocolVersion = 4;

        // Act
        var bytes = serializer.Serialize(protocolVersion, original);
        var result = serializer.Deserialize(protocolVersion, bytes, 0, bytes.Length, null);

        // Assert
        result.Should().Be(original);
    }

    // --- TimeOnlyToLocalTimeTypeSerializer ---

    [Fact]
    public void TimeOnlySerializer_CqlType_IsTime()
    {
        // Arrange
        var serializer = new TimeOnlyToLocalTimeTypeSerializer();

        // Act & Assert
        serializer.CqlType.Should().Be(ColumnTypeCode.Time);
    }

    [Fact]
    public void TimeOnlySerializer_Serialize_ThenDeserialize_RoundTrips()
    {
        // Arrange
        var serializer = new TimeOnlyToLocalTimeTypeSerializer();
        var original = new TimeOnly(14, 30, 45);
        ushort protocolVersion = 4;

        // Act
        var bytes = serializer.Serialize(protocolVersion, original);
        var result = serializer.Deserialize(protocolVersion, bytes, 0, bytes.Length, null);

        // Assert
        result.Hour.Should().Be(14);
        result.Minute.Should().Be(30);
        result.Second.Should().Be(45);
    }

    [Fact]
    public void TimeOnlySerializer_Serialize_Midnight_RoundTrips()
    {
        // Arrange
        var serializer = new TimeOnlyToLocalTimeTypeSerializer();
        var original = new TimeOnly(0, 0, 0);
        ushort protocolVersion = 4;

        // Act
        var bytes = serializer.Serialize(protocolVersion, original);
        var result = serializer.Deserialize(protocolVersion, bytes, 0, bytes.Length, null);

        // Assert
        result.Hour.Should().Be(0);
        result.Minute.Should().Be(0);
        result.Second.Should().Be(0);
    }

    [Fact]
    public void TimeOnlySerializer_Serialize_EndOfDay_RoundTrips()
    {
        // Arrange
        var serializer = new TimeOnlyToLocalTimeTypeSerializer();
        var original = new TimeOnly(23, 59, 59);
        ushort protocolVersion = 4;

        // Act
        var bytes = serializer.Serialize(protocolVersion, original);
        var result = serializer.Deserialize(protocolVersion, bytes, 0, bytes.Length, null);

        // Assert
        result.Hour.Should().Be(23);
        result.Minute.Should().Be(59);
        result.Second.Should().Be(59);
    }

    // --- DateTimeToLocalDateTypeSerializer ---

    [Fact]
    public void DateTimeSerializer_CqlType_IsDate()
    {
        // Arrange
        var serializer = new DateTimeToLocalDateTypeSerializer();

        // Act & Assert
        serializer.CqlType.Should().Be(ColumnTypeCode.Date);
    }

    [Fact]
    public void DateTimeSerializer_Serialize_ThenDeserialize_RoundTrips()
    {
        // Arrange
        var serializer = new DateTimeToLocalDateTypeSerializer();
        var original = new DateTime(2024, 6, 15);
        ushort protocolVersion = 4;

        // Act
        var bytes = serializer.Serialize(protocolVersion, original);
        var result = serializer.Deserialize(protocolVersion, bytes, 0, bytes.Length, null);

        // Assert
        result.Year.Should().Be(2024);
        result.Month.Should().Be(6);
        result.Day.Should().Be(15);
    }

    [Fact]
    public void DateTimeSerializer_Serialize_StripsTimeComponent()
    {
        // Arrange
        var serializer = new DateTimeToLocalDateTypeSerializer();
        var original = new DateTime(2024, 6, 15, 14, 30, 45);
        ushort protocolVersion = 4;

        // Act
        var bytes = serializer.Serialize(protocolVersion, original);
        var result = serializer.Deserialize(protocolVersion, bytes, 0, bytes.Length, null);

        // Assert — time component is lost (date-only serialization)
        result.Year.Should().Be(2024);
        result.Month.Should().Be(6);
        result.Day.Should().Be(15);
        result.Hour.Should().Be(0);
        result.Minute.Should().Be(0);
        result.Second.Should().Be(0);
    }

    [Fact]
    public void DateTimeSerializer_Serialize_LeapYear_RoundTrips()
    {
        // Arrange
        var serializer = new DateTimeToLocalDateTypeSerializer();
        var original = new DateTime(2024, 2, 29);
        ushort protocolVersion = 4;

        // Act
        var bytes = serializer.Serialize(protocolVersion, original);
        var result = serializer.Deserialize(protocolVersion, bytes, 0, bytes.Length, null);

        // Assert
        result.Year.Should().Be(2024);
        result.Month.Should().Be(2);
        result.Day.Should().Be(29);
    }

    [Fact]
    public void DateTimeSerializer_Serialize_EndOfYear_RoundTrips()
    {
        // Arrange
        var serializer = new DateTimeToLocalDateTypeSerializer();
        var original = new DateTime(2024, 12, 31);
        ushort protocolVersion = 4;

        // Act
        var bytes = serializer.Serialize(protocolVersion, original);
        var result = serializer.Deserialize(protocolVersion, bytes, 0, bytes.Length, null);

        // Assert
        result.Year.Should().Be(2024);
        result.Month.Should().Be(12);
        result.Day.Should().Be(31);
    }
}
