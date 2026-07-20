using System;
using FluentAssertions;
using Xunit;
using TomasAI.IFM.Framework.Storage.Extensions;

namespace TomasAI.IFM.Framework.Storage.UnitTests;

/// <summary>
/// Provides unit tests for the <see cref="ReadOnlySpanExtension"/> class, verifying the behavior of its parsing and
/// data extraction methods for various data types and scenarios.
/// </summary>
/// <remarks>This test class includes a variety of test cases to ensure the correctness of methods in the <see
/// cref="ReadOnlySpanExtension"/> class. The tests cover scenarios such as parsing delimited strings, handling empty
/// spans, extracting primitive data types, and working with enums. The tests also verify that the methods correctly
/// update the <c>start</c> parameter to reflect the position after parsing.</remarks>
public class ReadOnlySpanExtensionTests
{
    enum TestEnum { None, First, Second, Third }

    [Fact]
    public void Parse_ReturnsCorrectSlice_AndUpdatesStart()
    {
        // Arrange
        var input = "abc|def|ghi|".AsSpan();
        int start = 0;

        // Act & Assert
        ReadOnlySpanExtension.Parse(input, ref start).Should().Be("abc");
        ReadOnlySpanExtension.Parse(input, ref start).Should().Be("def");
        ReadOnlySpanExtension.Parse(input, ref start).Should().Be("ghi");
        ReadOnlySpanExtension.Parse(input, ref start).Should().Be("");
        start.Should().Be(-1);
    }

    [Fact]
    public void Parse_NoPipe_ReturnsRestAndSetsStartToMinusOne()
    {
        var input = "abcdef".AsSpan();
        int start = 0;
        ReadOnlySpanExtension.Parse(input, ref start).Should().Be("abcdef");
        start.Should().Be(-1);
    }

    [Fact]
    public void Parse_EmptySpan_ReturnsEmptyStringAndSetsStartToMinusOne()
    {
        var input = "".AsSpan();
        int start = 0;
        ReadOnlySpanExtension.Parse(input, ref start).Should().Be("");
        start.Should().Be(-1);
    }

    [Fact]
    public void Parse_StartPastEnd_ReturnsEmptyString()
    {
        var input = "abc|def".AsSpan();
        int start = 100;
        ReadOnlySpanExtension.Parse(input, ref start).Should().Be("");
    }

    [Fact]
    public void GetIntFloatDoubleDecimalBoolLongDateTime()
    {
        var input = "42|3.14|2.718|123456789012345|true|2023-12-31T23:59:59|".AsSpan();
        int start = 0;
        input.GetInt(ref start).Should().Be(42);
        input.GetFloat(ref start).Should().BeApproximately(3.14f, 0.0001f);
        input.GetDouble(ref start).Should().BeApproximately(2.718, 0.0001);
        input.GetLong(ref start).Should().Be(123456789012345L);
        input.GetBool(ref start).Should().BeTrue();
        input.GetDateTime(ref start).Should().Be(new DateTime(2023, 12, 31, 23, 59, 59));
    }

#if NET6_0_OR_GREATER
    [Fact]
    public void GetDateOnlyTimeOnly()
    {
        var input = "2023-12-31|23:59:59|".AsSpan();
        int start = 0;
        input.GetDateOnly(ref start).Should().Be(new DateOnly(2023, 12, 31));
        input.GetTimeOnly(ref start).Should().Be(new TimeOnly(23, 59, 59));
    }
#endif

    [Fact]
    public void GetEnum_ReturnsCorrectEnumValue_AndUpdatesStart()
    {
        var input = "First|Second|Third|None|".AsSpan();
        int start = 0;
        input.GetEnum<TestEnum>(ref start).Should().Be(TestEnum.First);
        input.GetEnum<TestEnum>(ref start).Should().Be(TestEnum.Second);
        input.GetEnum<TestEnum>(ref start).Should().Be(TestEnum.Third);
        input.GetEnum<TestEnum>(ref start).Should().Be(TestEnum.None);
        input.GetEnum<TestEnum>(ref start).Should().Be(TestEnum.None); // default for empty
        start.Should().Be(-1);
    }
}
