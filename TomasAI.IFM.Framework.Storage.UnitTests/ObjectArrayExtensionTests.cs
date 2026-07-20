using System;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.Extensions;

namespace TomasAI.IFM.Framework.Storage.UnitTests;

public class ObjectArrayExtensionTests
{
    [Fact]
    public void GetInt_ValidAndInvalid_ReturnsExpected()
    {
        object[] arr = { 42, "100", null, "abc", -1 };
        arr.GetInt(0).Should().Be(42);
        arr.GetInt(1).Should().Be(100);
        arr.GetInt(2).Should().Be(0);
        arr.GetInt(3).Should().Be(0);
        arr.GetInt(4).Should().Be(-1);
        arr.GetInt(-1).Should().Be(0);
        arr.GetInt(5).Should().Be(0);
        ((object[])null).GetInt(0).Should().Be(0);
    }

    [Fact]
    public void GetFloat_ValidAndInvalid_ReturnsExpected()
    {
        object[] arr = { 1.5f, "2.5", null, "abc", -3.5f };
        arr.GetFloat(0).Should().Be(1.5f);
        arr.GetFloat(1).Should().Be(2.5f);
        arr.GetFloat(2).Should().Be(0f);
        arr.GetFloat(3).Should().Be(0f);
        arr.GetFloat(4).Should().Be(-3.5f);
        arr.GetFloat(-1).Should().Be(0f);
        arr.GetFloat(5).Should().Be(0f);
        ((object[])null).GetFloat(0).Should().Be(0f);
    }

    [Fact]
    public void GetDouble_ValidAndInvalid_ReturnsExpected()
    {
        object[] arr = { 1.5d, "2.5", null, "abc", -3.5d };
        arr.GetDouble(0).Should().Be(1.5d);
        arr.GetDouble(1).Should().Be(2.5d);
        arr.GetDouble(2).Should().Be(0d);
        arr.GetDouble(3).Should().Be(0d);
        arr.GetDouble(4).Should().Be(-3.5d);
        arr.GetDouble(-1).Should().Be(0d);
        arr.GetDouble(5).Should().Be(0d);
        ((object[])null).GetDouble(0).Should().Be(0d);
    }

    [Fact]
    public void GetDecimal_ValidAndInvalid_ReturnsExpected()
    {
        object[] arr = { 1.5m, "2.5", null, "abc", -3.5m };
        arr.GetDecimal(0).Should().Be(1.5m);
        arr.GetDecimal(1).Should().Be(2.5m);
        arr.GetDecimal(2).Should().Be(0m);
        arr.GetDecimal(3).Should().Be(0m);
        arr.GetDecimal(4).Should().Be(-3.5m);
        arr.GetDecimal(-1).Should().Be(0m);
        arr.GetDecimal(5).Should().Be(0m);
        ((object[])null).GetDecimal(0).Should().Be(0m);
    }

    [Fact]
    public void GetBool_ValidAndInvalid_ReturnsExpected()
    {
        object[] arr = { true, "true", null, "abc", false, "False" };
        arr.GetBool(0).Should().BeTrue();
        arr.GetBool(1).Should().BeTrue();
        arr.GetBool(2).Should().BeFalse();
        arr.GetBool(3).Should().BeFalse();
        arr.GetBool(4).Should().BeFalse();
        arr.GetBool(5).Should().BeFalse();
        arr.GetBool(-1).Should().BeFalse();
        arr.GetBool(6).Should().BeFalse();
        ((object[])null).GetBool(0).Should().BeFalse();
    }

    [Fact]
    public void GetLong_ValidAndInvalid_ReturnsExpected()
    {
        object[] arr = { 1234567890123L, "9876543210", null, "abc", -123L };
        arr.GetLong(0).Should().Be(1234567890123L);
        arr.GetLong(1).Should().Be(9876543210L);
        arr.GetLong(2).Should().Be(0L);
        arr.GetLong(3).Should().Be(0L);
        arr.GetLong(4).Should().Be(-123L);
        arr.GetLong(-1).Should().Be(0L);
        arr.GetLong(5).Should().Be(0L);
        ((object[])null).GetLong(0).Should().Be(0L);
    }

    [Fact]
    public void GetDateTime_ValidAndInvalid_ReturnsExpected()
    {
        var dt = new DateTime(2023, 1, 1, 12, 0, 0);
        object[] arr = { dt, dt.ToString("o"), null, "abc" };
        arr.GetDateTime(0).Should().Be(dt);
        arr.GetDateTime(1).Should().Be(dt);
        arr.GetDateTime(2).Should().Be(default(DateTime));
        arr.GetDateTime(3).Should().Be(default(DateTime));
        arr.GetDateTime(-1).Should().Be(default(DateTime));
        arr.GetDateTime(4).Should().Be(default(DateTime));
        ((object[])null).GetDateTime(0).Should().Be(default(DateTime));
    }

#if NET6_0_OR_GREATER
    [Fact]
    public void GetDateOnly_ValidAndInvalid_ReturnsExpected()
    {
        var d = new DateOnly(2023, 1, 1);
        object[] arr = { d, d.ToString("yyyy-MM-dd"), null, "abc" };
        arr.GetDateOnly(0).Should().Be(d);
        arr.GetDateOnly(1).Should().Be(d);
        arr.GetDateOnly(2).Should().Be(default(DateOnly));
        arr.GetDateOnly(3).Should().Be(default(DateOnly));
        arr.GetDateOnly(-1).Should().Be(default(DateOnly));
        arr.GetDateOnly(4).Should().Be(default(DateOnly));
        ((object[])null).GetDateOnly(0).Should().Be(default(DateOnly));
    }

    [Fact]
    public void GetTimeOnly_ValidAndInvalid_ReturnsExpected()
    {
        var t = new TimeOnly(12, 0, 0);
        object[] arr = { t, t.ToString("HH:mm:ss"), null, "abc" };
        arr.GetTimeOnly(0).Should().Be(t);
        arr.GetTimeOnly(1).Should().Be(t);
        arr.GetTimeOnly(2).Should().Be(default(TimeOnly));
        arr.GetTimeOnly(3).Should().Be(default(TimeOnly));
        arr.GetTimeOnly(-1).Should().Be(default(TimeOnly));
        arr.GetTimeOnly(4).Should().Be(default(TimeOnly));
        ((object[])null).GetTimeOnly(0).Should().Be(default(TimeOnly));
    }
#endif

    [Fact]
    public void GetEnum_ValidAndInvalid_ReturnsExpected()
    {
        object[] arr = { DayOfWeek.Monday, "Tuesday", null, "abc", 1 };
        arr.GetEnum<DayOfWeek>(0).Should().Be(DayOfWeek.Monday);
        arr.GetEnum<DayOfWeek>(1).Should().Be(DayOfWeek.Tuesday);
        arr.GetEnum<DayOfWeek>(2).Should().Be(default(DayOfWeek));
        arr.GetEnum<DayOfWeek>(3).Should().Be(default(DayOfWeek));
        arr.GetEnum<DayOfWeek>(4).Should().Be(DayOfWeek.Monday); // 1 maps to Monday
        arr.GetEnum<DayOfWeek>(-1).Should().Be(default(DayOfWeek));
        arr.GetEnum<DayOfWeek>(5).Should().Be(default(DayOfWeek));
        ((object[])null).GetEnum<DayOfWeek>(0).Should().Be(default(DayOfWeek));
    }

    [Fact]
    public void GetString_ValidAndInvalid_ReturnsExpected()
    {
        object[] arr = { "hello", 123, null };
        arr.GetString(0).Should().Be("hello");
        arr.GetString(1).Should().Be("123");
        arr.GetString(2).Should().Be(string.Empty);
        arr.GetString(-1).Should().Be(string.Empty);
        arr.GetString(3).Should().Be(string.Empty);
        ((object[])null).GetString(0).Should().Be(string.Empty);
    }

    [Fact]
    public void GetBytes_ValidAndInvalid_ReturnsExpected()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var str = "abc";
        var base64 = Convert.ToBase64String(bytes);
        object[] arr = { bytes, str, base64, null };
        arr.GetBytes(0).Should().BeEquivalentTo(bytes);
        arr.GetBytes(1).Should().BeEquivalentTo(System.Text.Encoding.UTF8.GetBytes(str));
        arr.GetBytes(2).Should().BeEquivalentTo(bytes);
        arr.GetBytes(3).Should().BeEquivalentTo(Array.Empty<byte>());
        arr.GetBytes(-1).Should().BeEquivalentTo(Array.Empty<byte>());
        arr.GetBytes(4).Should().BeEquivalentTo(Array.Empty<byte>());
        ((object[])null).GetBytes(0).Should().BeEquivalentTo(Array.Empty<byte>());
    }

    [Fact]
    public void GetGuid_ValidAndInvalid_ReturnsExpected()
    {
        var guid = Guid.NewGuid();
        var guidStr = guid.ToString();
        var guidBytes = guid.ToByteArray();
        object[] arr = { guid, guidStr, guidBytes, null, "abc" };
        arr.GetGuid(0).Should().Be(guid);
        arr.GetGuid(1).Should().Be(guid);
        arr.GetGuid(2).Should().Be(guid);
        arr.GetGuid(3).Should().Be(Guid.Empty);
        arr.GetGuid(4).Should().Be(Guid.Empty);
        arr.GetGuid(-1).Should().Be(Guid.Empty);
        arr.GetGuid(5).Should().Be(Guid.Empty);
        ((object[])null).GetGuid(0).Should().Be(Guid.Empty);
    }
}