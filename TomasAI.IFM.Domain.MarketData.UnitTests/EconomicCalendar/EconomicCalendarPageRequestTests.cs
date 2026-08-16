using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;

namespace TomasAI.IFM.Domain.MarketData.UnitTests.EconomicCalendar;

public sealed class EconomicCalendarPageRequestTests
{
    [Fact]
    public void ValidateAcceptsBoundedUtcRequest()
    {
        var request = CreateRequest();

        var act = request.Validate;

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateRejectsNonUtcDates()
    {
        var request = CreateRequest() with
        {
            StartDateUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local)
        };

        var act = request.Validate;

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateRejectsExcessivePartitionFanOut()
    {
        var start = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var request = CreateRequest() with
        {
            StartDateUtc = start,
            EndDateUtc = start.AddMonths(119),
            CountryCodes = Enumerable.Range(0, 5).Select(index => $"C{index}").ToArray()
        };

        var act = request.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*partition limit*");
    }

    [Fact]
    public void ValidateRejectsOversizedPage()
    {
        var request = CreateRequest() with
        {
            PageSize = EconomicCalendarQueryLimits.MaximumPageSize + 1
        };

        var act = request.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    static EconomicCalendarPageRequest CreateRequest()
    {
        var start = DateTime.UtcNow.Date;
        return new EconomicCalendarPageRequest
        {
            StartDateUtc = start,
            EndDateUtc = start.AddDays(1).AddTicks(-1),
            CountryCodes = ["US"],
            PageSize = 100
        };
    }
}
