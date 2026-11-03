using TomasAI.IFM.Framework.Storage.Extensions;
using Xunit;

namespace TomasAI.IFM.Framework.Storage.UnitTests;

public sealed class ExtensionBlockCompatibilityTests
{
    [Fact]
    public void Static_and_extension_calls_preserve_ref_cursor_and_generic_arguments()
    {
        ReadOnlySpan<char> input = "42|Friday|tail";
        var cursor = 0;
        Assert.Equal(42, ReadOnlySpanExtension.GetInt(charBuffer: input, start: ref cursor));
        Assert.Equal(DayOfWeek.Friday, input.GetEnum<DayOfWeek>(ref cursor));
        Assert.Equal("tail", ReadOnlySpanExtension.GetString(input, ref cursor));
        Assert.Equal(-1, cursor);

        cursor = 3;
        Assert.Equal(DayOfWeek.Friday, ReadOnlySpanExtension.GetEnum<DayOfWeek>(input, ref cursor));
        Assert.Equal(10, cursor);
    }

    [Fact]
    public void Framework_conditional_date_and_time_extensions_remain_available()
    {
        ReadOnlySpan<char> input = "2026-11-03|13:45:00";
        var cursor = 0;
        Assert.Equal(new DateOnly(2026, 11, 3), input.GetDateOnly(ref cursor));
        Assert.Equal(new TimeOnly(13, 45), ReadOnlySpanExtension.GetTimeOnly(input, ref cursor));
        Assert.Equal(-1, cursor);
    }
}
