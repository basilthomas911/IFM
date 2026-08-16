using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.FinancialModelingPrep;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class FmpMarketDataImportCoordinatorTests
{
    [Fact]
    public async Task ImportAsync_MapsSupplementalFieldsAndSubmitsDeterministicSingleDateCommands()
    {
        var first = new DateOnly(2026, 8, 13);
        var second = first.AddDays(1);
        var treasury = Substitute.For<ITreasuryCurve>();
        treasury.GetRangeAsync(first, second, Arg.Any<CancellationToken>())
            .Returns([Curve(second), Curve(first)]);
        var calendar = Substitute.For<IEconomicCalendar>();
        calendar.GetAsync(first, second, Arg.Any<IReadOnlySet<string>?>(), Arg.Any<CancellationToken>())
            .Returns([new EconomicCalendarEntry(
                first.ToDateTime(new TimeOnly(12, 30), DateTimeKind.Utc),
                "US",
                "CPI",
                "2.9",
                "3.0",
                null,
                "High",
                "%",
                "-0.1",
                "-3.33",
                DateTimeOffset.UtcNow,
                "test")]);
        var commands = SuccessfulCommands();
        var coordinator = Create(treasury, calendar, commands);

        var result = await coordinator.ImportAsync(new(first, second));

        Assert.Equal(3, result.AcceptedCommands);
        Assert.Equal(3, result.AcceptedRows);
        Assert.Equal(1, result.NoDataDates);
        Received.InOrder(() =>
        {
            commands.ImportYieldCurveRatesAsync(
                first.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                Arg.Any<YieldCurveRateReadModel[]>());
            commands.ImportEconomicCalendarsAsync(
                first.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                Arg.Is<EconomicCalendarReadModel[]>(rows =>
                    rows.Length == 1
                    && rows[0].Impact == "High"
                    && rows[0].Unit == "%"
                    && rows[0].Prior == null));
            commands.ImportYieldCurveRatesAsync(
                second.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                Arg.Any<YieldCurveRateReadModel[]>());
        });
    }

    [Fact]
    public async Task ImportAsync_StopsAfterCommandFailureAndReportsUnsubmittedDates()
    {
        var first = new DateOnly(2026, 8, 13);
        var second = first.AddDays(1);
        var treasury = Substitute.For<ITreasuryCurve>();
        treasury.GetRangeAsync(first, second, Arg.Any<CancellationToken>())
            .Returns([Curve(first), Curve(second)]);
        var calendar = Substitute.For<IEconomicCalendar>();
        var commands = Substitute.For<IMarketDataCommandApi>();
        commands.ImportYieldCurveRatesAsync(Arg.Any<DateTime>(), Arg.Any<YieldCurveRateReadModel[]>())
            .Returns(new ServiceResult<Guid>(409, "duplicate"));
        var coordinator = Create(treasury, calendar, commands);

        var result = await coordinator.ImportAsync(new(
            first,
            second,
            IncludeEconomicCalendar: false));

        Assert.Equal(1, result.FailedDates);
        Assert.Equal(1, result.RemainingUnsubmittedDates);
        Assert.Equal(0, result.AcceptedCommands);
        await commands.Received(1).ImportYieldCurveRatesAsync(
            Arg.Any<DateTime>(), Arg.Any<YieldCurveRateReadModel[]>());
    }

    private static FmpMarketDataImportCoordinator Create(
        ITreasuryCurve treasury,
        IEconomicCalendar calendar,
        IMarketDataCommandApi commands) =>
        new(
            treasury,
            calendar,
            commands,
            new FmpMarketDataImportOptions(),
            NullLogger<FmpMarketDataImportCoordinator>.Instance);

    private static IMarketDataCommandApi SuccessfulCommands()
    {
        var commands = Substitute.For<IMarketDataCommandApi>();
        commands.ImportYieldCurveRatesAsync(Arg.Any<DateTime>(), Arg.Any<YieldCurveRateReadModel[]>())
            .Returns(_ => new ServiceResult<Guid>(Guid.NewGuid()));
        commands.ImportEconomicCalendarsAsync(Arg.Any<DateTime>(), Arg.Any<EconomicCalendarReadModel[]>())
            .Returns(_ => new ServiceResult<Guid>(Guid.NewGuid()));
        return commands;
    }

    private static TreasuryCurveSnapshot Curve(DateOnly date) =>
        new(
            date,
            Enum.GetValues<TreasuryTenor>()
                .Select((tenor, index) => new TreasuryRatePoint(tenor, 4m + index / 100m))
                .ToArray(),
            DateTimeOffset.UtcNow,
            "test");
}
