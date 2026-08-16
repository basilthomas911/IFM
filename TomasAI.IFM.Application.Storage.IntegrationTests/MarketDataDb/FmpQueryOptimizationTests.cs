using FluentAssertions;
using System;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Framework.Storage.Extensions;
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
    public void EconomicCalendarQueriesUseSingleBoundedCanonicalTable()
    {
        MarketDataSchemaCql.CreateEconomicCalendarV2Table
            .Should().Contain("economic_calendar_v2")
            .And.Contain("PRIMARY KEY ((countryCode, monthBucket), eventDate, eventName)");
        MarketDataSchemaCql.CreateEconomicCalendarCountryCodeV1Table
            .Should().Contain("PRIMARY KEY ((lookupId), countryCode)");

        MarketDataDbCql.GetEconomicCalendarCountryCodes
            .Should().Contain("economic_calendar_country_code_v1")
            .And.Contain("LIMIT 512")
            .And.NotContain("FROM economic_calendar;");
        MarketDataDbCql.GetEconomicCalendars
            .Should().Contain("FROM economic_calendar_v2")
            .And.Contain("countryCode = :countryCode")
            .And.Contain("monthBucket = :monthBucket")
            .And.Contain("LIMIT 2501")
            .And.NotContain("ALLOW FILTERING");
    }

    [Fact]
    public void FmpSchemaPersistsSupplementalFieldsAndCalendarRejectsAtCanonicalRow()
    {
        MarketDataSchemaCql.CreateEconomicCalendarV2Table
            .Should().Contain("impact text")
            .And.Contain("unit text")
            .And.Contain("change text")
            .And.Contain("changePercentage text");
        MarketDataDbCql.InsertEconomicCalendarV2
            .Should().Contain(":impact")
            .And.Contain(":changePercentage");
        MarketDataDbCql.InsertEconomicCalendarV2IfNotExists
            .Should().Contain("IF NOT EXISTS")
            .And.Contain("economic_calendar_v2");
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
    public async Task EconomicCalendarWritesPopulateBoundedPageAndCountryLookups()
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

            var page = await fixture.DevDatabase.GetEconomicCalendarPageAsync(new EconomicCalendarPageRequest
            {
                StartDateUtc = eventDate.Date,
                EndDateUtc = eventDate.Date.AddDays(1).AddTicks(-1),
                CountryCodes = [calendar.CountryCode],
                PageSize = 10
            });
            page.Items.Should().ContainSingle(row => row.Id == calendar.Id);
            (await fixture.DevDatabase.GetEconomicCalendarCountryCodesAsync())
                .Should().ContainSingle(row => row.CountryCode == calendar.CountryCode);
        }
        finally
        {
            await fixture.DevDatabase.DeleteEconomicCalendarAsync(calendar.Id);
        }

        var afterDelete = await fixture.DevDatabase.GetEconomicCalendarPageAsync(new EconomicCalendarPageRequest
        {
            StartDateUtc = eventDate.Date,
            EndDateUtc = eventDate.Date.AddDays(1).AddTicks(-1),
            CountryCodes = [calendar.CountryCode],
            PageSize = 10
        });
        afterDelete.Items.Should().NotContain(row => row.Id == calendar.Id);
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
        var createdOn = DateTime.UtcNow;
        createdOn = createdOn.AddTicks(-(createdOn.Ticks % TimeSpan.TicksPerMillisecond));
        var calendar = new EconomicCalendarReadModel(
            new DateTime(8995, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            "QX",
            $"reject-existing-{suffix}",
            "1",
            null,
            null,
            createdOn,
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
    public async Task CalendarCanonicalRejectAllowsSameCommandReplayAndOnlyOneConcurrentOwner()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var createdOn = DateTime.UtcNow;
        createdOn = createdOn.AddTicks(-(createdOn.Ticks % TimeSpan.TicksPerMillisecond));
        var calendar = new EconomicCalendarReadModel(
            new DateTime(8994, 8, 20, 10, 0, 0, DateTimeKind.Utc),
            "QW",
            $"reject-race-{suffix}",
            "1",
            null,
            null,
            createdOn,
            nameof(FmpQueryOptimizationTests));
        var commands = new[] { Guid.NewGuid(), Guid.NewGuid() };

        try
        {
            var outcomes = await Task.WhenAll(commands.Select(async commandId =>
            {
                try
                {
                    await fixture.DevDatabase.InsertEconomicCalendarsAsync(
                        [calendar], ImportDuplicatePolicy.Reject, commandId);
                    return (CommandId: commandId, Accepted: true);
                }
                catch (MarketDataImportDuplicateException)
                {
                    return (CommandId: commandId, Accepted: false);
                }
            }));

            outcomes.Should().ContainSingle(outcome => outcome.Accepted);
            var winner = outcomes.Single(outcome => outcome.Accepted);
            outcomes.Should().ContainSingle(outcome => !outcome.Accepted);
            await fixture.DevDatabase.InsertEconomicCalendarsAsync(
                [calendar], ImportDuplicatePolicy.Reject, winner.CommandId);
            (await fixture.DevDatabase.GetEconomicCalendarAsync(calendar.Id))
                .Should().BeEquivalentTo(calendar);
        }
        finally
        {
            await fixture.DevDatabase.DeleteEconomicCalendarAsync(calendar.Id);
        }
    }

    [Fact]
    public async Task OfflineBackfillReconcilesCalendarCutoverAndYieldProjection()
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
            await fixture.DevDatabase.Use("""
                CREATE TABLE IF NOT EXISTS economic_calendar (
                    eventDate timestamp, countryCode text, eventName text,
                    actual text, forecast text, prior text, impact text, unit text,
                    change text, changePercentage text, createdOn timestamp, createdBy text,
                    PRIMARY KEY (eventDate, countryCode, eventName)
                ) WITH CLUSTERING ORDER BY (countryCode ASC, eventName ASC);
                """).ExecuteCommandAsync();
            await fixture.DevDatabase.Use("""
                INSERT INTO economic_calendar (eventDate, countryCode, eventName, actual, forecast, prior,
                    impact, unit, change, changePercentage, createdOn, createdBy)
                VALUES (:eventDate, :countryCode, :eventName, :actual, :forecast, :prior,
                    :impact, :unit, :change, :changePercentage, :createdOn, :createdBy);
                """).SetParameters(new LegacyCalendarInsert(calendar)).ExecuteCommandAsync();
            await fixture.DevDatabase.InsertYieldCurveRateAsync(rate);

            var calendarResult = await fixture.DevDatabase.BackfillEconomicCalendarV2Async(batchSize: 2);
            var result = await fixture.DevDatabase.BackfillFmpQueryProjectionsAsync(batchSize: 2);

            calendarResult.IsReconciled.Should().BeTrue();
            result.IsReconciled.Should().BeTrue();
            (await fixture.DevDatabase.GetEconomicCalendarAsync(calendar.Id))
                .Should().BeEquivalentTo(calendar);
            (await fixture.DevDatabase.GetYieldCurveRateAsync(rate.ValueDate))
                .Should().BeEquivalentTo(rate);
        }
        finally
        {
            await fixture.DevDatabase.DeleteEconomicCalendarAsync(calendar.Id);
            await fixture.DevDatabase.Use("""
                DELETE FROM economic_calendar
                WHERE eventDate = :eventDate AND countryCode = :countryCode AND eventName = :eventName;
                """).SetParameters(new LegacyCalendarKey(calendar)).ExecuteCommandAsync();
            await fixture.DevDatabase.DeleteYieldCurveRateAsync(rate.ValueDate);
        }
    }

    readonly record struct LegacyCalendarInsert(EconomicCalendarReadModel Row)
        : TomasAI.IFM.Framework.Storage.IBindValue
    {
        public object Bind() => new object?[]
        {
            Row.EventDate, Row.CountryCode, Row.EventName, Row.Actual, Row.Forecast, Row.Prior,
            Row.Impact, Row.Unit, Row.Change, Row.ChangePercentage, Row.CreatedOn, Row.CreatedBy
        };
    }

    readonly record struct LegacyCalendarKey(EconomicCalendarReadModel Row)
        : TomasAI.IFM.Framework.Storage.IBindValue
    {
        public object Bind() => new object?[] { Row.EventDate, Row.CountryCode, Row.EventName };
    }
}
