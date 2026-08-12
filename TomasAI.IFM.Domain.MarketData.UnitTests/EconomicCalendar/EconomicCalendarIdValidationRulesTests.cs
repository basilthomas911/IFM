using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Validation;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.UnitTests.EconomicCalendar;

public class EconomicCalendarIdValidationRulesTests
{
    public static TheoryData<DateTime, string?, string?, string> InvalidIds => new()
    {
        {
            DateTime.MinValue,
            "USA",
            "US Unemployment Report",
            EconomicCalendarIdValidationRules.EventDateErrorMessage
        },
        {
            DateTime.MaxValue,
            "USA",
            "US Unemployment Report",
            EconomicCalendarIdValidationRules.EventDateErrorMessage
        },
        {
            new DateTime(2020, 10, 10),
            null,
            "US Unemployment Report",
            EconomicCalendarIdValidationRules.CountryCodeErrorMessage
        },
        {
            new DateTime(2020, 10, 10),
            "USA",
            null,
            EconomicCalendarIdValidationRules.EventNameErrorMessage
        }
    };

    [Theory]
    [MemberData(nameof(InvalidIds))]
    public void Execute_InvalidId_ReturnsExpectedError(
        DateTime eventDate,
        string? countryCode,
        string? eventName,
        string expectedError)
    {
        var id = new EconomicCalendarId(eventDate, countryCode!, eventName!);

        var errors = new EconomicCalendarIdValidationRules().Execute(id);

        errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(expectedError);
    }

    [Fact]
    public void Execute_ValidId_ReturnsNoErrors()
    {
        var id = new EconomicCalendarId(
            new DateTime(2020, 10, 10),
            "USA",
            "US Unemployment Report");

        var errors = new EconomicCalendarIdValidationRules().Execute(id);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Execute_SerializerDefaultId_ReturnsAllRequiredErrors()
    {
        var errors = new EconomicCalendarIdValidationRules()
            .Execute(new EconomicCalendarId());

        errors.Select(error => error.ErrorMessage).Should().BeEquivalentTo(
            EconomicCalendarIdValidationRules.EventDateErrorMessage,
            EconomicCalendarIdValidationRules.CountryCodeErrorMessage,
            EconomicCalendarIdValidationRules.EventNameErrorMessage);
    }
}
