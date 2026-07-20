using System;
using System.Data;
using Xunit;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace TomasAI.IFM.Framework.Storage.UnitTests;

public class AdoNetDataRecordTests
{
    static IDataReader CreateMockReader(bool isDbNull = false)
    {
        var mock = Substitute.For<IDataReader>();
        mock.IsDBNull(Arg.Any<int>()).Returns(isDbNull);
        return mock;
    }

    // --- SetReader ---

    [Fact]
    public void SetReaderReturnsSelf()
    {
        var record = new AdoNetDataRecord();
        var mockReader = CreateMockReader();
        var result = record.SetReader(mockReader);
        result.Should().BeSameAs(record);
    }

    // --- GetInt ---

    [Fact]
    public void GetIntReturnsValueWhenNotNull()
    {
        var mockReader = CreateMockReader();
        mockReader.GetInt32(0).Returns(42);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetInt(0).Should().Be(42);
    }

    [Fact]
    public void GetIntReturnsDefaultWhenDbNull()
    {
        var mockReader = CreateMockReader(isDbNull: true);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetInt(0).Should().Be(0);
    }

    [Fact]
    public void GetIntReturnsDefaultOnException()
    {
        var mockReader = CreateMockReader();
        mockReader.GetInt32(0).Throws(new InvalidCastException());
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetInt(0).Should().Be(0);
    }

    // --- GetFloat ---

    [Fact]
    public void GetFloatReturnsValueWhenNotNull()
    {
        var mockReader = CreateMockReader();
        mockReader.GetFloat(0).Returns(1.5f);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetFloat(0).Should().Be(1.5f);
    }

    [Fact]
    public void GetFloatReturnsDefaultWhenDbNull()
    {
        var mockReader = CreateMockReader(isDbNull: true);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetFloat(0).Should().Be(0f);
    }

    [Fact]
    public void GetFloatReturnsDefaultOnException()
    {
        var mockReader = CreateMockReader();
        mockReader.GetFloat(0).Throws(new InvalidCastException());
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetFloat(0).Should().Be(0f);
    }

    // --- GetDouble ---

    [Fact]
    public void GetDoubleReturnsValueWhenNotNull()
    {
        var mockReader = CreateMockReader();
        mockReader.GetDouble(0).Returns(3.14);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetDouble(0).Should().Be(3.14);
    }

    [Fact]
    public void GetDoubleReturnsDefaultWhenDbNull()
    {
        var mockReader = CreateMockReader(isDbNull: true);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetDouble(0).Should().Be(0d);
    }

    [Fact]
    public void GetDoubleReturnsDefaultOnException()
    {
        var mockReader = CreateMockReader();
        mockReader.GetDouble(0).Throws(new InvalidCastException());
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetDouble(0).Should().Be(0d);
    }

    // --- GetDecimal ---

    [Fact]
    public void GetDecimalReturnsValueWhenNotNull()
    {
        var mockReader = CreateMockReader();
        mockReader.GetDecimal(0).Returns(99.99m);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetDecimal(0).Should().Be(99.99m);
    }

    [Fact]
    public void GetDecimalReturnsDefaultWhenDbNull()
    {
        var mockReader = CreateMockReader(isDbNull: true);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetDecimal(0).Should().Be(0m);
    }

    [Fact]
    public void GetDecimalReturnsDefaultOnException()
    {
        var mockReader = CreateMockReader();
        mockReader.GetDecimal(0).Throws(new InvalidCastException());
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetDecimal(0).Should().Be(0m);
    }

    // --- GetBool ---

    [Fact]
    public void GetBoolReturnsValueWhenNotNull()
    {
        var mockReader = CreateMockReader();
        mockReader.GetBoolean(0).Returns(true);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetBool(0).Should().Be(true);
    }

    [Fact]
    public void GetBoolReturnsDefaultWhenDbNull()
    {
        var mockReader = CreateMockReader(isDbNull: true);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetBool(0).Should().Be(false);
    }

    [Fact]
    public void GetBoolReturnsDefaultOnException()
    {
        var mockReader = CreateMockReader();
        mockReader.GetBoolean(0).Throws(new InvalidCastException());
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetBool(0).Should().Be(false);
    }

    // --- GetLong ---

    [Fact]
    public void GetLongReturnsValueWhenNotNull()
    {
        var mockReader = CreateMockReader();
        mockReader.GetInt64(0).Returns(123456789L);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetLong(0).Should().Be(123456789L);
    }

    [Fact]
    public void GetLongReturnsDefaultWhenDbNull()
    {
        var mockReader = CreateMockReader(isDbNull: true);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetLong(0).Should().Be(0L);
    }

    [Fact]
    public void GetLongReturnsDefaultOnException()
    {
        var mockReader = CreateMockReader();
        mockReader.GetInt64(0).Throws(new InvalidCastException());
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetLong(0).Should().Be(0L);
    }

    // --- GetDateTime ---

    [Fact]
    public void GetDateTimeReturnsValueWhenNotNull()
    {
        var expected = new DateTime(2024, 6, 15, 10, 30, 0);
        var mockReader = CreateMockReader();
        mockReader.GetDateTime(0).Returns(expected);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetDateTime(0).Should().Be(expected);
    }

    [Fact]
    public void GetDateTimeReturnsDefaultWhenDbNull()
    {
        var mockReader = CreateMockReader(isDbNull: true);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetDateTime(0).Should().Be(default(DateTime));
    }

    [Fact]
    public void GetDateTimeFallsBackToDateTimeOffsetOnException()
    {
        var dto = new DateTimeOffset(2024, 6, 15, 10, 30, 0, TimeSpan.Zero);
        var mockReader = CreateMockReader();
        mockReader.GetDateTime(0).Throws(new InvalidCastException());
        mockReader.GetValue(0).Returns(dto);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetDateTime(0).Should().Be(dto.DateTime);
    }

    [Fact]
    public void GetDateTimeFallsBackToLongTicksOnException()
    {
        var ticks = new DateTime(2024, 1, 1).Ticks;
        var mockReader = CreateMockReader();
        mockReader.GetDateTime(0).Throws(new InvalidCastException());
        mockReader.GetValue(0).Returns(ticks);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetDateTime(0).Should().Be(new DateTime(ticks));
    }

    [Fact]
    public void GetDateTimeFallsBackToStringOnException()
    {
        var mockReader = CreateMockReader();
        mockReader.GetDateTime(0).Throws(new InvalidCastException());
        mockReader.GetValue(0).Returns("2024-06-15");
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetDateTime(0).Should().Be(new DateTime(2024, 6, 15));
    }

    [Fact]
    public void GetDateTimeReturnsDefaultWhenAllFallbacksFail()
    {
        var mockReader = CreateMockReader();
        mockReader.GetDateTime(0).Throws(new InvalidCastException());
        mockReader.GetValue(0).Returns("not-a-date");
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetDateTime(0).Should().Be(default(DateTime));
    }

    // --- GetDateOnly ---

    [Fact]
    public void GetDateOnlyReturnsValueWhenNotNull()
    {
        var dt = new DateTime(2024, 6, 15);
        var mockReader = CreateMockReader();
        mockReader.GetDateTime(0).Returns(dt);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetDateOnly(0).Should().Be(new DateOnly(2024, 6, 15));
    }

    [Fact]
    public void GetDateOnlyReturnsDefaultWhenDbNull()
    {
        var mockReader = CreateMockReader(isDbNull: true);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetDateOnly(0).Should().Be(default(DateOnly));
    }

    [Fact]
    public void GetDateOnlyFallsBackToStringOnException()
    {
        var mockReader = CreateMockReader();
        mockReader.GetDateTime(0).Throws(new InvalidCastException());
        mockReader.GetValue(0).Returns("2024-06-15");
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetDateOnly(0).Should().Be(new DateOnly(2024, 6, 15));
    }

    // --- GetTimeOnly ---

    [Fact]
    public void GetTimeOnlyReturnsValueFromTimeSpan()
    {
        var ts = new TimeSpan(14, 30, 0);
        var mockReader = CreateMockReader();
        mockReader.GetValue(0).Returns(ts);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetTimeOnly(0).Should().Be(new TimeOnly(14, 30, 0));
    }

    [Fact]
    public void GetTimeOnlyReturnsDefaultWhenDbNull()
    {
        var mockReader = CreateMockReader(isDbNull: true);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetTimeOnly(0).Should().Be(default(TimeOnly));
    }

    [Fact]
    public void GetTimeOnlyReturnsValueFromDateTime()
    {
        var dt = new DateTime(2024, 1, 1, 14, 30, 0);
        var mockReader = CreateMockReader();
        mockReader.GetValue(0).Returns(dt);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetTimeOnly(0).Should().Be(new TimeOnly(14, 30, 0));
    }

    [Fact]
    public void GetTimeOnlyReturnsValueFromLongTicks()
    {
        var ticks = new TimeOnly(14, 30, 0).Ticks;
        var mockReader = CreateMockReader();
        mockReader.GetValue(0).Returns(ticks);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetTimeOnly(0).Should().Be(new TimeOnly(14, 30, 0));
    }

    [Fact]
    public void GetTimeOnlyReturnsValueFromString()
    {
        var mockReader = CreateMockReader();
        mockReader.GetValue(0).Returns("14:30:00");
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetTimeOnly(0).Should().Be(new TimeOnly(14, 30, 0));
    }

    // --- GetEnum ---

    [Fact]
    public void GetEnumReturnsValueFromString()
    {
        var mockReader = CreateMockReader();
        mockReader.GetString(0).Returns("End");
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetEnum<TestData.TestEnumValue>(0).Should().Be(TestData.TestEnumValue.End);
    }

    [Fact]
    public void GetEnumReturnsDefaultWhenDbNull()
    {
        var mockReader = CreateMockReader(isDbNull: true);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetEnum<TestData.TestEnumValue>(0).Should().Be(default(TestData.TestEnumValue));
    }

    [Fact]
    public void GetEnumFallsBackToIntValueOnStringException()
    {
        var mockReader = CreateMockReader();
        mockReader.GetString(0).Throws(new InvalidCastException());
        mockReader.GetValue(0).Returns(1);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetEnum<TestData.TestEnumValue>(0).Should().Be(TestData.TestEnumValue.End);
    }

    [Fact]
    public void GetEnumReturnsDefaultWhenInvalidString()
    {
        var mockReader = CreateMockReader();
        mockReader.GetString(0).Returns("InvalidValue");
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetEnum<TestData.TestEnumValue>(0).Should().Be(default(TestData.TestEnumValue));
    }

    // --- GetString ---

    [Fact]
    public void GetStringReturnsValueWhenNotNull()
    {
        var mockReader = CreateMockReader();
        mockReader.GetString(0).Returns("hello");
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetString(0).Should().Be("hello");
    }

    [Fact]
    public void GetStringReturnsEmptyWhenDbNull()
    {
        var mockReader = CreateMockReader(isDbNull: true);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetString(0).Should().Be(string.Empty);
    }

    [Fact]
    public void GetStringReturnsEmptyOnException()
    {
        var mockReader = CreateMockReader();
        mockReader.GetString(0).Throws(new InvalidCastException());
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetString(0).Should().Be(string.Empty);
    }

    // --- GetBytes ---

    [Fact]
    public void GetBytesReturnsValueWhenNotNull()
    {
        var expected = new byte[] { 1, 2, 3 };
        var mockReader = CreateMockReader();
        mockReader.GetBytes(0, 0, null, 0, 0).Returns(3);
        mockReader.GetBytes(0, 0, Arg.Any<byte[]>(), 0, 3)
            .Returns(ci =>
            {
                var buf = ci.ArgAt<byte[]>(2);
                Array.Copy(expected, 0, buf, 0, 3);
                return 3L;
            });
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetBytes(0).Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GetBytesReturnsEmptyWhenDbNull()
    {
        var mockReader = CreateMockReader(isDbNull: true);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetBytes(0).Should().BeEquivalentTo(Array.Empty<byte>());
    }

    [Fact]
    public void GetBytesReturnsEmptyWhenZeroLength()
    {
        var mockReader = CreateMockReader();
        mockReader.GetBytes(0, 0, null, 0, 0).Returns(0);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetBytes(0).Should().BeEquivalentTo(Array.Empty<byte>());
    }

    [Fact]
    public void GetBytesReturnsEmptyOnException()
    {
        var mockReader = CreateMockReader();
        mockReader.GetBytes(0, 0, null, 0, 0).Throws(new InvalidCastException());
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetBytes(0).Should().BeEquivalentTo(Array.Empty<byte>());
    }

    // --- Multiple column indices ---

    [Fact]
    public void GetIntAtDifferentIndices()
    {
        var mockReader = CreateMockReader();
        mockReader.GetInt32(0).Returns(10);
        mockReader.GetInt32(1).Returns(20);
        mockReader.GetInt32(2).Returns(30);
        var record = new AdoNetDataRecord().SetReader(mockReader);
        record.GetInt(0).Should().Be(10);
        record.GetInt(1).Should().Be(20);
        record.GetInt(2).Should().Be(30);
    }
}
