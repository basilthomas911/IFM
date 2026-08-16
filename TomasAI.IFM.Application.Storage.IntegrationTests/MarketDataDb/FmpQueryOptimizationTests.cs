using FluentAssertions;
using System;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class FmpQueryOptimizationCollection
{
    public const string Name = nameof(FmpQueryOptimizationCollection);
}

[Collection(FmpQueryOptimizationCollection.Name)]
public sealed class FmpQueryOptimizationTests(MarketDataFixture fixture)
    : IClassFixture<MarketDataFixture>
{
    [Fact]
    public void EconomicCalendarQueriesUseBoundedDedicatedProjections()
    {
        MarketDataSchemaCql.CreateEconomicCalendarByMonthV1Table
            .Should().Contain("PRIMARY KEY ((monthBucket), eventDate, countryCode, eventName)");
        MarketDataSchemaCql.CreateEconomicCalendarCountryCodeV1Table
            .Should().Contain("PRIMARY KEY ((lookupId), countryCode)");
        MarketDataSchemaCql.CreateEconomicCalendarMonthV1Table
            .Should().Contain("PRIMARY KEY ((lookupId), monthBucket)");

        MarketDataDbCql.GetEconomicCalendarCountryCodes
            .Should().Contain("economic_calendar_country_code_v1")
            .And.Contain("LIMIT 512")
            .And.NotContain("FROM economic_calendar;");
        MarketDataDbCql.GetEconomicCalendarsByMonth
            .Should().Contain("economic_calendar_by_month_v1")
            .And.Contain("monthBucket = :monthBucket")
            .And.Contain("LIMIT 2500");
        MarketDataDbCql.GetEconomicCalendars
            .Should().Contain("LIMIT 2500");
    }

    [Fact]
    public void FmpSchemaPersistsSupplementalFieldsAndRejectUsesConditionalOwnership()
    {
        MarketDataSchemaCql.CreateEconomicCalendarTable
            .Should().Contain("impact text")
            .And.Contain("unit text")
            .And.Contain("change text")
            .And.Contain("changePercentage text");
        MarketDataDbCql.InsertEconomicCalendar
            .Should().Contain(":impact")
            .And.Contain(":changePercentage");
        MarketDataSchemaCql.CreateMarketDataImportOwnershipV1Table
            .Should().Contain("PRIMARY KEY ((dataset, logicalKey))");
        MarketDataDbCql.ClaimMarketDataImportOwnershipV1
            .Should().Contain("IF NOT EXISTS")
            .And.NotContain("ALLOW FILTERING");
        MarketDataDbCql.GetMarketDataImportOwnershipV1
            .Should().Contain("dataset = :dataset AND logicalKey = :logicalKey")
            .And.NotContain("ALLOW FILTERING");
    }

    [Fact]
    public async Task EconomicCalendarRangeRejectsUnboundedMonthFanOut()
    {
        var start = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(MarketDataDbContext.EconomicCalendarMaximumRangeMonths);

        var act = () => fixture.DevDatabase.GetEconomicCalendarsAsync(start, end, "US");

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task EconomicCalendarWritesPopulateBoundedAllAndCountryLookups()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var eventDate = new DateTime(8998, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var createdOn = DateTime.UtcNow;
        createdOn = createdOn.AddTicks(-(createdOn.Ticks % TimeSpan.TicksPerMillisecond));
        var calendar = new EconomicCalendarReadModel(
            eventDate,
            "QZ",
            $"projection-{suffix}",
            "1",
            "1",
            "1",
            createdOn,
            nameof(FmpQueryOptimizationTests));

        try
        {
            await fixture.DevDatabase.InsertEconomicCalendarAsync(calendar);

            (await fixture.DevDatabase.GetEconomicCalendarAllAsync())
                .Should().ContainSingle(row => row.Id == calendar.Id);
            (await fixture.DevDatabase.GetEconomicCalendarCountryCodesAsync())
                .Should().ContainSingle(row => row.CountryCode == calendar.CountryCode);
        }
        finally
        {
            await fixture.DevDatabase.DeleteEconomicCalendarAsync(calendar.Id);
        }

        (await fixture.DevDatabase.GetEconomicCalendarAllAsync())
            .Should().NotContain(row => row.Id == calendar.Id);
    }

    [Fact]
    public void YieldCurveQueriesUseOrderedAndDistinctBoundedShapes()
    {
        MarketDataSchemaCql.CreateYieldCurveRateTable
            .Should().Contain("PRIMARY KEY ((id), valueDate)")
            .And.Contain("CLUSTERING ORDER BY (valueDate DESC)");
        MarketDataSchemaCql.CreateYieldCurveRateYearV1Table
            .Should().Contain("PRIMARY KEY ((lookupId), rateYear)");
        MarketDataSchemaCql.CreateYieldCurveRateByDateV1Table
            .Should().Contain("PRIMARY KEY ((lookupId), valueDate)")
            .And.Contain("CLUSTERING ORDER BY (valueDate DESC)");

        MarketDataDbCql.GetLastYieldCurveRate
            .Should().Contain("yield_curve_rate_by_date_v1")
            .And.Contain("WHERE lookupId = 1 LIMIT 1");
        MarketDataDbCql.GetYieldCurveRates
            .Should().Contain("LIMIT 5000");
        MarketDataDbCql.GetYieldCurveRateYears
            .Should().Contain("yield_curve_rate_year_v1")
            .And.Contain("LIMIT 200")
            .And.NotContain("yield_curve_rates");
    }

    [Fact]
    public async Task YieldCurveRangeRejectsUnboundedMaterialization()
    {
        var start = new DateOnly(2000, 1, 1);
        var end = start.AddDays(MarketDataDbContext.YieldCurveMaximumRangeDays);

        var act = () => fixture.DevDatabase.GetYieldCurveRatesAsync(start, end);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task YieldCurveWritesDeduplicateYearProjectionAndLatestIsServerOrdered()
    {
        var earlier = SampleData.YieldCurveRate with { ValueDate = new DateOnly(9998, 1, 2) };
        var latest = SampleData.YieldCurveRate with { ValueDate = new DateOnly(9998, 12, 30) };

        try
        {
            await fixture.DevDatabase.InsertYieldCurveRatesAsync([latest, earlier]);

            (await fixture.DevDatabase.GetYieldCurveRateYearsAsync())
                .Count(year => year == 9998).Should().Be(1);
            (await fixture.DevDatabase.GetLastYieldCurveRateAsync())
                .Should().NotBeNull().And.Match<YieldCurveRateReadModel>(row => row.ValueDate == latest.ValueDate);
        }
        finally
        {
            await fixture.DevDatabase.DeleteYieldCurveRateAsync(earlier.ValueDate);
            await fixture.DevDatabase.DeleteYieldCurveRateAsync(latest.ValueDate);
        }
    }

    [Fact]
    public async Task RejectOwnershipAllowsSameCommandReplayAndRejectsConcurrentCommand()
    {
        var dateOffset = (int)(BitConverter.ToUInt32(Guid.NewGuid().ToByteArray()) % 30_000);
        var rate = SampleData.YieldCurveRate with
        {
            ValueDate = new DateOnly(8000, 1, 1).AddDays(dateOffset)
        };
        var commandId = Guid.NewGuid();

        try
        {
            await fixture.DevDatabase.InsertYieldCurveRatesAsync(
                [rate], ImportDuplicatePolicy.Reject, commandId);
            await fixture.DevDatabase.InsertYieldCurveRatesAsync(
                [rate], ImportDuplicatePolicy.Reject, commandId);

            var act = () => fixture.DevDatabase.InsertYieldCurveRatesAsync(
                [rate], ImportDuplicatePolicy.Reject, Guid.NewGuid());

            await act.Should().ThrowAsync<MarketDataImportDuplicateException>();
            (await fixture.DevDatabase.GetYieldCurveRateAsync(rate.ValueDate))
                .Should().BeEquivalentTo(rate);
        }
        finally
        {
            await fixture.DevDatabase.DeleteYieldCurveRateAsync(rate.ValueDate);
        }
    }

    [Fact]
    public async Task RejectOwnershipFailsClosedForPreExistingCalendarRow()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var calendar = new EconomicCalendarReadModel(
            new DateTime(8995, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            "QX",
            $"reject-existing-{suffix}",
            "1",
            null,
            null,
            DateTime.UtcNow,
            nameof(FmpQueryOptimizationTests));

        try
        {
            await fixture.DevDatabase.InsertEconomicCalendarAsync(calendar);

            var act = () => fixture.DevDatabase.InsertEconomicCalendarsAsync(
                [calendar with { Actual = "2" }],
                ImportDuplicatePolicy.Reject,
                Guid.NewGuid());

            await act.Should().ThrowAsync<MarketDataImportDuplicateException>();
            (await fixture.DevDatabase.GetEconomicCalendarAsync(calendar.Id))!
                .Actual.Should().Be("1");
        }
        finally
        {
            await fixture.DevDatabase.DeleteEconomicCalendarAsync(calendar.Id);
        }
    }

    [Fact]
    public async Task OfflineBackfillRebuildsAndReconcilesAllFmpQueryProjections()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var createdOn = DateTime.UtcNow;
        createdOn = createdOn.AddTicks(-(createdOn.Ticks % TimeSpan.TicksPerMillisecond));
        var calendar = new EconomicCalendarReadModel(
            new DateTime(8997, 3, 1, 12, 0, 0, DateTimeKind.Utc),
            "QY",
            $"backfill-{suffix}",
            "1",
            "1",
            "1",
            createdOn,
            nameof(FmpQueryOptimizationTests));
        var rate = SampleData.YieldCurveRate with { ValueDate = new DateOnly(9997, 3, 1) };

        try
        {
            await fixture.DevDatabase.InsertEconomicCalendarAsync(calendar);
            await fixture.DevDatabase.InsertYieldCurveRateAsync(rate);

            var result = await fixture.DevDatabase.BackfillFmpQueryProjectionsAsync(batchSize: 2);

            result.IsReconciled.Should().BeTrue();
            (await fixture.DevDatabase.GetEconomicCalendarAllAsync())
                .Should().Contain(row => row.Id == calendar.Id);
            (await fixture.DevDatabase.GetYieldCurveRateAsync(rate.ValueDate))
                .Should().BeEquivalentTo(rate);
        }
        finally
        {
            await fixture.DevDatabase.DeleteEconomicCalendarAsync(calendar.Id);
            await fixture.DevDatabase.DeleteYieldCurveRateAsync(rate.ValueDate);
        }
    }
}
