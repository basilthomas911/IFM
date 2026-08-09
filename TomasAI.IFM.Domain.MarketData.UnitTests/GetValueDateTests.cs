using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Query;
using Xunit;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public sealed class GetValueDateTests
{
    [Theory]
    [InlineData(2026, 8, 8, 12, null)]
    [InlineData(2026, 8, 9, 17, null)]
    [InlineData(2026, 8, 9, 18, "2026-08-10")]
    [InlineData(2026, 8, 10, 16, "2026-08-10")]
    [InlineData(2026, 8, 10, 18, "2026-08-11")]
    [InlineData(2026, 8, 14, 18, "2026-08-14")]
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
}
