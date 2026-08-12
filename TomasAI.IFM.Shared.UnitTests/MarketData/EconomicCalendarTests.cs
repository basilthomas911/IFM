using FluentAssertions;
using System;
using TomasAI.IFM.Domain.MarketData.Shared;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.MarketData;

public class EconomicCalendarTests
{
    [Fact]
    public void ConstructorOk()
    {
        var eventDate = new DateTime(2020, 10, 10).Date;
        const string countryCode = "USA";
        const string eventName = "US Unemployment Report";

        var economicCalendar = new EconomicCalendarId(eventDate, countryCode, eventName);

        economicCalendar.EventDate.Should().Be(eventDate);
        economicCalendar.CountryCode.Should().Be(countryCode);
        economicCalendar.EventName.Should().Be(eventName);
    }

    [Fact]
    public void EqualsOk()
    {
        var eventDate = new DateTime(2020, 10, 10).Date;
        const string countryCode = "USA";
        const string eventName = "US Unemployment Report";

        var economicCalendar = new EconomicCalendarId(eventDate, countryCode, eventName);
        var equivalentCalendar = new EconomicCalendarId(eventDate, countryCode, eventName);

        economicCalendar.Should().Be(equivalentCalendar);
    }

    [Fact]
    public void EqualsWithNullEconomicCalendarId()
    {
        var economicCalendar = new EconomicCalendarId(
            new DateTime(2020, 10, 10).Date,
            "USA",
            "US Unemployment Report");

        economicCalendar.Should().NotBe(default(EconomicCalendarId));
    }

    [Fact]
    public void EqualsWithDifferentParameterValues()
    {
        var eventDate = new DateTime(2020, 10, 10).Date;
        const string eventName = "US Unemployment Report";
        var economicCalendar = new EconomicCalendarId(eventDate, "USA", eventName);
        var differentCalendar = new EconomicCalendarId(eventDate, "CAN", eventName);

        economicCalendar.Should().NotBe(differentCalendar);
    }
}
