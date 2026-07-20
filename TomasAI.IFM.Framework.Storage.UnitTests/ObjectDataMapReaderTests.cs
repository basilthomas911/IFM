using System;
using System.Data;
using System.Linq.Expressions;
using Xunit;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Framework.Storage.UnitTests.TestData;

namespace TomasAI.IFM.Framework.Storage.UnitTests;

public class ObjectDataMapReaderTests
{
    static IDataReader CreateMockReader(bool isDbNull = false)
    {
        var mock = Substitute.For<IDataReader>();
        mock.IsDBNull(Arg.Any<int>()).Returns(isDbNull);
        return mock;
    }

    static ObjectDataMapReader<DataReaderEntity> CreateMapReader(IDataReader mockReader)
        => new ObjectDataMapReader<DataReaderEntity>(mockReader);

    // --- String ---

    [Fact]
    public void GetStringReturnsValue()
    {
        // Arrange
        var mockReader = CreateMockReader();
        mockReader.GetOrdinal("StringValue").Returns(0);
        mockReader.GetString(0).Returns("hello");
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.StringValue);

        // Assert
        result.Should().Be("hello");
    }

    // --- Bool ---

    [Fact]
    public void GetBoolReturnsValue()
    {
        // Arrange
        var mockReader = CreateMockReader();
        mockReader.GetOrdinal("BooleanValue").Returns(0);
        mockReader.GetBoolean(0).Returns(true);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.BooleanValue);

        // Assert
        result.Should().Be(true);
    }

    [Fact]
    public void GetNullBoolReturnsNullWhenDbNull()
    {
        // Arrange
        var mockReader = CreateMockReader(isDbNull: true);
        mockReader.GetOrdinal("NullBooleanValue").Returns(0);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.NullBooleanValue);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetNullBoolReturnsValueWhenNotNull()
    {
        // Arrange
        var mockReader = CreateMockReader();
        mockReader.GetOrdinal("NullBooleanValue").Returns(0);
        mockReader.GetBoolean(0).Returns(true);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.NullBooleanValue);

        // Assert
        result.Should().Be(true);
    }

    // --- Int ---

    [Fact]
    public void GetIntReturnsValue()
    {
        // Arrange
        var mockReader = CreateMockReader();
        mockReader.GetOrdinal("IntValue").Returns(0);
        mockReader.GetInt32(0).Returns(42);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.IntValue);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void GetNullIntReturnsNullWhenDbNull()
    {
        // Arrange
        var mockReader = CreateMockReader(isDbNull: true);
        mockReader.GetOrdinal("NullIntValue").Returns(0);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.NullIntValue);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetNullIntReturnsValueWhenNotNull()
    {
        // Arrange
        var mockReader = CreateMockReader();
        mockReader.GetOrdinal("NullIntValue").Returns(0);
        mockReader.GetInt32(0).Returns(42);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.NullIntValue);

        // Assert
        result.Should().Be(42);
    }

    // --- Short ---

    [Fact]
    public void GetShortReturnsValue()
    {
        // Arrange
        var mockReader = CreateMockReader();
        mockReader.GetOrdinal("ShortValue").Returns(0);
        mockReader.GetInt16(0).Returns((short)7);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.ShortValue);

        // Assert
        result.Should().Be((short)7);
    }

    [Fact]
    public void GetNullShortReturnsNullWhenDbNull()
    {
        // Arrange
        var mockReader = CreateMockReader(isDbNull: true);
        mockReader.GetOrdinal("NullShortValue").Returns(0);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.NullShortValue);

        // Assert
        result.Should().BeNull();
    }

    // --- Long ---

    [Fact]
    public void GetLongReturnsValue()
    {
        // Arrange
        var mockReader = CreateMockReader();
        mockReader.GetOrdinal("LongValue").Returns(0);
        mockReader.GetInt64(0).Returns(123456789L);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.LongValue);

        // Assert
        result.Should().Be(123456789L);
    }

    [Fact]
    public void GetNullLongReturnsNullWhenDbNull()
    {
        // Arrange
        var mockReader = CreateMockReader(isDbNull: true);
        mockReader.GetOrdinal("NullLongValue").Returns(0);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.NullLongValue);

        // Assert
        result.Should().BeNull();
    }

    // --- Double ---

    [Fact]
    public void GetDoubleReturnsValue()
    {
        // Arrange
        var mockReader = CreateMockReader();
        mockReader.GetOrdinal("DoubleValue").Returns(0);
        mockReader.GetDouble(0).Returns(3.14);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.DoubleValue);

        // Assert
        result.Should().Be(3.14);
    }

    [Fact]
    public void GetNullDoubleReturnsNullWhenDbNull()
    {
        // Arrange
        var mockReader = CreateMockReader(isDbNull: true);
        mockReader.GetOrdinal("NullDoubleValue").Returns(0);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.NullDoubleValue);

        // Assert
        result.Should().BeNull();
    }

    // --- Float ---

    [Fact]
    public void GetFloatReturnsValue()
    {
        // Arrange
        var mockReader = CreateMockReader();
        mockReader.GetOrdinal("FloatValue").Returns(0);
        mockReader.GetFloat(0).Returns(1.5f);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.FloatValue);

        // Assert
        result.Should().Be(1.5f);
    }

    [Fact]
    public void GetNullFloatReturnsNullWhenDbNull()
    {
        // Arrange
        var mockReader = CreateMockReader(isDbNull: true);
        mockReader.GetOrdinal("NullFloatValue").Returns(0);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.NullFloatValue);

        // Assert
        result.Should().BeNull();
    }

    // --- Decimal ---

    [Fact]
    public void GetDecimalReturnsValue()
    {
        // Arrange
        var mockReader = CreateMockReader();
        mockReader.GetOrdinal("DecimalValue").Returns(0);
        mockReader.GetDecimal(0).Returns(99.99m);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.DecimalValue);

        // Assert
        result.Should().Be(99.99m);
    }

    [Fact]
    public void GetNullDecimalReturnsNullWhenDbNull()
    {
        // Arrange
        var mockReader = CreateMockReader(isDbNull: true);
        mockReader.GetOrdinal("NullDecimalValue").Returns(0);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.NullDecimalValue);

        // Assert
        result.Should().BeNull();
    }

    // --- DateTime ---

    [Fact]
    public void GetDateTimeReturnsValue()
    {
        // Arrange
        var expected = new DateTime(2024, 6, 15, 10, 30, 0);
        var mockReader = CreateMockReader();
        mockReader.GetOrdinal("DateTimeValue").Returns(0);
        mockReader.GetDateTime(0).Returns(expected);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.DateTimeValue);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetNullDateTimeReturnsNullWhenDbNull()
    {
        // Arrange
        var mockReader = CreateMockReader(isDbNull: true);
        mockReader.GetOrdinal("NullDateTimeValue").Returns(0);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.NullDateTimeValue);

        // Assert
        result.Should().BeNull();
    }

    // --- TimeSpan ---

    [Fact]
    public void GetTimeSpanReturnsValue()
    {
        // Arrange
        var ticks = new TimeSpan(1, 2, 3).Ticks;
        var mockReader = CreateMockReader();
        mockReader.GetOrdinal("TimeSpanValue").Returns(0);
        mockReader.GetInt64(0).Returns(ticks);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.TimeSpanValue);

        // Assert
        result.Should().Be(new TimeSpan(1, 2, 3));
    }

    [Fact]
    public void GetNullTimeSpanReturnsNullWhenDbNull()
    {
        // Arrange
        var mockReader = CreateMockReader(isDbNull: true);
        mockReader.GetOrdinal("NullTimeSpanValue").Returns(0);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.NullTimeSpanValue);

        // Assert
        result.Should().BeNull();
    }

    // --- Guid ---

    [Fact]
    public void GetGuidReturnsValue()
    {
        // Arrange
        var expected = Guid.NewGuid();
        var mockReader = CreateMockReader();
        mockReader.GetOrdinal("GuidValue").Returns(0);
        mockReader.GetGuid(0).Returns(expected);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.GuidValue);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetNullGuidReturnsNullWhenDbNull()
    {
        // Arrange
        var mockReader = CreateMockReader(isDbNull: true);
        mockReader.GetOrdinal("NullGuidValue").Returns(0);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get(e => e.NullGuidValue);

        // Assert
        result.Should().BeNull();
    }

    // --- Enum ---

    [Fact]
    public void GetEnumReturnsValue()
    {
        // Arrange
        var mockReader = CreateMockReader();
        mockReader.GetOrdinal("EnumValue").Returns(0);
        mockReader.GetString(0).Returns("End");
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get<TestEnumValue>(e => e.EnumValue);

        // Assert
        result.Should().Be(TestEnumValue.End);
    }

    [Fact]
    public void GetEnumReturnsDefaultWhenDbNull()
    {
        // Arrange
        var mockReader = CreateMockReader(isDbNull: true);
        mockReader.GetOrdinal("EnumValue").Returns(0);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get<TestEnumValue>(e => e.EnumValue);

        // Assert
        result.Should().Be(default(TestEnumValue));
    }

    [Fact]
    public void GetEnumReturnsDefaultWhenEmptyString()
    {
        // Arrange
        var mockReader = CreateMockReader();
        mockReader.GetOrdinal("EnumValue").Returns(0);
        mockReader.GetString(0).Returns(string.Empty);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get<TestEnumValue>(e => e.EnumValue);

        // Assert
        result.Should().Be(default(TestEnumValue));
    }

    [Fact]
    public void GetEnumReturnsDefaultWhenInvalidString()
    {
        // Arrange
        var mockReader = CreateMockReader();
        mockReader.GetOrdinal("EnumValue").Returns(0);
        mockReader.GetString(0).Returns("InvalidValue");
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.Get<TestEnumValue>(e => e.EnumValue);

        // Assert
        result.Should().Be(default(TestEnumValue));
    }

    // --- GetISODateTime ---

    [Fact]
    public void GetISODateTimeReturnsValue()
    {
        // Arrange
        var expected = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var mockReader = CreateMockReader();
        mockReader.GetOrdinal("DateTimeValue").Returns(0);
        mockReader.GetString(0).Returns(expected.ToString("O"));
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.GetISODateTime(e => e.DateTimeValue);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetISODateTimeReturnsDefaultWhenDbNull()
    {
        // Arrange
        var mockReader = CreateMockReader(isDbNull: true);
        mockReader.GetOrdinal("DateTimeValue").Returns(0);
        var reader = CreateMapReader(mockReader);

        // Act
        var result = reader.GetISODateTime(e => e.DateTimeValue);

        // Assert
        result.Should().Be(default(DateTime));
    }

    // --- Field index caching ---

    [Fact]
    public void GetFieldIndexIsCached()
    {
        // Arrange
        var mockReader = CreateMockReader();
        mockReader.GetOrdinal("IntValue").Returns(0);
        mockReader.GetInt32(0).Returns(42);
        var reader = CreateMapReader(mockReader);

        // Act
        reader.Get(e => e.IntValue);
        reader.Get(e => e.IntValue);

        // Assert - GetOrdinal should only be called once due to caching
        mockReader.Received(1).GetOrdinal("IntValue");
    }

    // --- As<TMapper> ---

    [Fact]
    public void AsCastsToRequestedType()
    {
        // Arrange
        var mockReader = CreateMockReader();
        var reader = CreateMapReader(mockReader);

        // Act
        var asReader = reader.As<ObjectDataMapReader<DataReaderEntity>>();

        // Assert
        asReader.Should().BeSameAs(reader);
    }

    [Fact]
    public void AsReturnsNullForIncompatibleType()
    {
        // Arrange
        var mockReader = CreateMockReader();
        var reader = CreateMapReader(mockReader);

        // Act
        var asResult = reader.As<string>();

        // Assert
        asResult.Should().BeNull();
    }
}
