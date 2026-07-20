using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Shared.Reference;

namespace TomasAI.IFM.Shared.UnitTests.Reference
{
    public class EconomicCalendarTests
    {
        [Fact]
        public void ConstructorOk()
        {
            // given valid constructor parameters...
            var eventDate = (new DateTime(2020, 10, 10)).Date;
            var countryCode = "USA";
            var eventName = "US Unemployment Report";

            // when constructing and economic calendar...
            var ec = new EconomicCalendarId(eventDate, countryCode, eventName);

            // then property values are all valid.
            ec.EventDate.Should().Be(eventDate);
            ec.CountryCode.Should().Be(countryCode);
            ec.EventName.Should().Be(eventName);
        }

        [Fact]
        public void ConstructorWithMinValueEventDate()
        {
            // given an event date parameter with min value...
            var eventDate = DateTime.MinValue;
            var countryCode = "USA";
            var eventName = "US Unemployment Report";

            // when constructing an economic calendar...
            var e = Assert.Throws<ArgumentNullException>( () => new EconomicCalendarId(eventDate, countryCode, eventName));

            // then throw argumen null exception.
            e.Should().BeOfType<ArgumentNullException>();
        }

        [Fact]
        public void ConstructorWithMaxValueEventDate()
        {
            // given an event date parameter with max value...
            var eventDate = DateTime.MaxValue;
            var countryCode = "USA";
            var eventName = "US Unemployment Report";

            // when constructing an economic calendar...
            var e = Assert.Throws<ArgumentNullException>(() => new EconomicCalendarId(eventDate, countryCode, eventName));

            // then throw argumen null exception.
            e.Should().BeOfType<ArgumentNullException>();
        }

        [Fact]
        public void ConstructorWithEmptyCountryCode()
        {
            // given an event date parameter with empty country...
            var eventDate = (new DateTime(2020, 10, 10)).Date;
            var countryCode = default(string);
            var eventName = "US Unemployment Report";

            // when constructing an economic calendar...
            var e = Assert.Throws<ArgumentNullException>(() => new EconomicCalendarId(eventDate, countryCode, eventName));

            // then throw argumen null exception.
            e.Should().BeOfType<ArgumentNullException>();
        }

        [Fact]
        public void ConstructorWithEmptyEventName()
        {
            // given an event date parameter with empty country...
            var eventDate = (new DateTime(2020, 10, 10)).Date;
            var countryCode = "USA";
            var eventName = default(string);

            // when constructing an economic calendar...
            var e = Assert.Throws<ArgumentNullException>(() => new EconomicCalendarId(eventDate, countryCode, eventName));

            // then throw argumen null exception.
            e.Should().BeOfType<ArgumentNullException>();
        }

        [Fact]
        public void EqualsOk()
        {
            // given valid constructor parameters...
            var eventDate = (new DateTime(2020, 10, 10)).Date;
            var countryCode = "USA";
            var eventName = "US Unemployment Report";

            // when constructing a pair of economic calendars with same parameters...
            var ec = new EconomicCalendarId(eventDate, countryCode, eventName);
            var ec2 = new EconomicCalendarId(eventDate, countryCode, eventName);

            // then pair of economic calendars are equal.
            ec.Should().Be(ec2);
        }

        [Fact]
        public void EqualsWithNullEconomicCalendarId()
        {
            // given valid constructor parameters...
            var eventDate = (new DateTime(2020, 10, 10)).Date;
            var countryCode = "USA";
            var eventName = "US Unemployment Report";

            // when a valid economic calendar id with a null economic calendar...
            var ec = new EconomicCalendarId(eventDate, countryCode, eventName);
            var ec2 = default(EconomicCalendarId);

            // then pair of economic calendars are not equal.
            ec.Should().NotBe(ec2);
        }

        [Fact]
        public void EqualsWithDifferentParameterValues()
        {
            // given valid constructor parameters...
            var eventDate = (new DateTime(2020, 10, 10)).Date;
            var countryCode = "USA";
            var eventName = "US Unemployment Report";

            // when constructing a pair of economic calendars with different parameters...
            var ec = new EconomicCalendarId(eventDate, countryCode, eventName);
            var ec2 = new EconomicCalendarId(eventDate, "CAN", eventName);

            // then pair of economic calendars are not equal.
            ec.Should().NotBe(ec2);
        }
    }
}
