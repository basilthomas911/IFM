using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Domain.Reference.EconomicCalendar.Query;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.UnitTests.EconomicCalendar;

public sealed class EconomicCalendarRangeTests
{
    [Theory]
    [InlineData(EconomicCalendarViewType.ThisWeek, 3, 9)]
    [InlineData(EconomicCalendarViewType.NextWeek, 10, 16)]
    public async Task WeeklyViews_ExcludeMidnightAtStartOfFollowingWeek(
        EconomicCalendarViewType viewType,
        int expectedStartDay,
        int expectedEndDay)
    {
        var database = Substitute.For<IReferenceDbContext>();
        database
            .GetEconomicCalendarsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.FromResult<ICollection<EconomicCalendarReadModel>>([]));
        var factory = Substitute.For<IDbContextFactory>();
        factory.ReferenceDb.Returns(database);
        var query = new GetEconomicCalendarQuery(
            new DateTime(2026, 8, 5),
            viewType,
            "US");

        _ = await query.GetEconomicCalendarAsync(factory);

        var expectedStart = new DateTime(2026, 8, expectedStartDay);
        var expectedEnd = new DateTime(2026, 8, expectedEndDay, 23, 59, 59, 999);
        await database.Received(1).GetEconomicCalendarsAsync(expectedStart, expectedEnd, "US");
    }
}
