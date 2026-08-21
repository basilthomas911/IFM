using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Query;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.UnitTests.EconomicCalendar;

public sealed class EconomicCalendarRangeTests
{
    [Theory]
    [InlineData(EconomicCalendarViewType.ThisWeek, 3, 10)]
    [InlineData(EconomicCalendarViewType.NextWeek, 10, 17)]
    public async Task WeeklyViews_PreserveTheEasternMidnightUtcBoundary(
        EconomicCalendarViewType viewType,
        int expectedStartDay,
        int expectedEndDay)
    {
        var database = Substitute.For<IMarketDataDbContext>();
        database
            .GetEconomicCalendarsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.FromResult<ICollection<EconomicCalendarReadModel>>([]));
        var factory = Substitute.For<IDbContextFactory>();
        factory.MarketDataDb.Returns(database);
        var query = new GetEconomicCalendarQuery(
            new DateTime(2026, 8, 5, 4, 0, 0, DateTimeKind.Utc),
            viewType,
            "US");

        _ = await query.GetEconomicCalendarAsync(factory);

        var expectedStart = new DateTime(2026, 8, expectedStartDay, 4, 0, 0, DateTimeKind.Utc);
        var expectedEnd = new DateTime(2026, 8, expectedEndDay, 4, 0, 0, DateTimeKind.Utc).AddTicks(-1);
        await database.Received(1).GetEconomicCalendarsAsync(expectedStart, expectedEnd, "US");
    }

    [Fact]
    public async Task Today_PreservesTheEasternDayUtcRangeInsteadOfTruncatingToUtcMidnight()
    {
        var database = Substitute.For<IMarketDataDbContext>();
        database
            .GetEconomicCalendarsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.FromResult<ICollection<EconomicCalendarReadModel>>([]));
        var factory = Substitute.For<IDbContextFactory>();
        factory.MarketDataDb.Returns(database);
        var easternMidnightUtc = new DateTime(2026, 8, 20, 4, 0, 0, DateTimeKind.Utc);
        var query = new GetEconomicCalendarQuery(
            easternMidnightUtc,
            EconomicCalendarViewType.Today,
            "US");

        _ = await query.GetEconomicCalendarAsync(factory);

        await database.Received(1).GetEconomicCalendarsAsync(
            easternMidnightUtc,
            easternMidnightUtc.AddDays(1).AddTicks(-1),
            "US");
    }
}
