using FluentAssertions;
using System;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Storage.MarketDataDb;
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
    public async Task OfflineBackfillReconcilesYieldProjection()
    {
        var rate = SampleData.YieldCurveRate with { ValueDate = new DateOnly(9997, 3, 1) };

        try
        {
            await fixture.DevDatabase.InsertYieldCurveRateAsync(rate);

            var result = await fixture.DevDatabase.BackfillFmpQueryProjectionsAsync(batchSize: 2);

            result.IsReconciled.Should().BeTrue();
            (await fixture.DevDatabase.GetYieldCurveRateAsync(rate.ValueDate))
                .Should().BeEquivalentTo(rate);
        }
        finally
        {
            await fixture.DevDatabase.DeleteYieldCurveRateAsync(rate.ValueDate);
        }
    }
}
