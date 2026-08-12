using FluentAssertions;
using System;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

public sealed class EconomicCalendarDbTests(MarketDataFixture fixture)
    : IClassFixture<MarketDataFixture>
{
    [Fact]
    public void EconomicCalendarSchemaAndRangeQueryUseMarketDataCountryMonthProjection()
    {
        MarketDataSchemaCql.CreateEconomicCalendarByCountryMonthV2Table
            .Should().Contain("economic_calendar_by_country_month_v2")
            .And.Contain("PRIMARY KEY ((countryCode, monthBucket), eventDate, eventName)");

        MarketDataDbCql.GetEconomicCalendars
            .Should().Contain("countryCode = :countryCode")
            .And.Contain("monthBucket = :monthBucket")
            .And.Contain("eventDate >= :startDate")
            .And.Contain("eventDate <= :endDate");
    }

    [Fact]
    public async Task EconomicCalendarCrudAndCountryMonthRangeUseMarketDataKeyspace()
    {
        var db = fixture.DevDatabase;
        var eventName = $"storage-integration-{Guid.NewGuid():N}";
        var eventDate = new DateTime(2048, 7, 15, 14, 30, 0, DateTimeKind.Utc);
        var original = CreateCalendar(eventDate, "US", eventName, "1");

        try
        {
            await db.InsertEconomicCalendarAsync(original);

            (await db.GetEconomicCalendarAsync(original.Id)).Should().BeEquivalentTo(original);
            (await db.GetEconomicCalendarsAsync(eventDate.Date, eventDate.Date.AddDays(1).AddTicks(-1), "US"))
                .Should().ContainSingle(row => row.Id == original.Id);

            var updated = original with { Actual = "2" };
            await db.UpdateEconomicCalendarAsync(original.Id, updated);
            (await db.GetEconomicCalendarAsync(original.Id))!.Actual.Should().Be("2");
        }
        finally
        {
            await db.DeleteEconomicCalendarAsync(original.Id);
        }

        (await db.GetEconomicCalendarAsync(original.Id)).Should().BeNull();
    }

    [Fact]
    public async Task EconomicCalendarRangeSpansMonthBucketsInclusively()
    {
        var db = fixture.DevDatabase;
        var runId = Guid.NewGuid().ToString("N");
        EconomicCalendarReadModel[] calendars =
        [
            CreateCalendar(new DateTime(2049, 1, 31, 23, 59, 0, DateTimeKind.Utc), "CA", $"month-end-{runId}", "1"),
            CreateCalendar(new DateTime(2049, 2, 1, 0, 1, 0, DateTimeKind.Utc), "CA", $"month-start-{runId}", "2")
        ];

        try
        {
            await db.InsertEconomicCalendarsAsync(calendars);
            var rows = await db.GetEconomicCalendarsAsync(calendars[0].EventDate, calendars[1].EventDate, "CA");
            rows.Where(row => row.EventName.EndsWith(runId, StringComparison.Ordinal))
                .Should().HaveCount(2);
        }
        finally
        {
            foreach (var calendar in calendars)
                await db.DeleteEconomicCalendarAsync(calendar.Id);
        }
    }

    static EconomicCalendarReadModel CreateCalendar(
        DateTime eventDate,
        string countryCode,
        string eventName,
        string actual)
    {
        var createdOn = DateTime.UtcNow;
        createdOn = createdOn.AddTicks(-(createdOn.Ticks % TimeSpan.TicksPerMillisecond));
        return new(eventDate, countryCode, eventName, actual, "1", "1", createdOn, "StorageIntegrationTest");
    }
}
