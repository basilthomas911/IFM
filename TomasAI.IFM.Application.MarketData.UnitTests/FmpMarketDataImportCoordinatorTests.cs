using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.FinancialModelingPrep;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class FmpMarketDataImportCoordinatorTests
{
    [Fact]
    public async Task ImportAsync_SubmitsDeterministicParameterOnlySingleDateCommands()
    {
        var first = new DateOnly(2026, 8, 13);
        var second = first.AddDays(1);
        var commands = SuccessfulCommands();
        var coordinator = Create(commands);

        var result = await coordinator.ImportAsync(new(first, second, CountryCodes: ["US", "CA"]));

        Assert.Equal(4, result.SubmittedCommands);
        Assert.Equal(0, result.RejectedSubmissions);
        Assert.All(result.Dates, value => Assert.Equal(FmpImportDateStatus.Submitted, value.Status));
        Assert.All(result.Dates, value => Assert.NotEqual(Guid.Empty, value.CommandId));
        Received.InOrder(() =>
        {
            commands.ImportYieldCurveRatesAsync(
                first.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            commands.ImportEconomicCalendarsAsync(
                first.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                Arg.Is<string[]>(countries => countries.SequenceEqual(new[] { "US", "CA" })));
            commands.ImportYieldCurveRatesAsync(
                second.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            commands.ImportEconomicCalendarsAsync(
                second.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                Arg.Is<string[]>(countries => countries.SequenceEqual(new[] { "US", "CA" })));
        });
    }

    [Fact]
    public async Task ImportAsync_StopsAfterCommandFailureAndReportsUnsubmittedDates()
    {
        var first = new DateOnly(2026, 8, 13);
        var second = first.AddDays(1);
        var commands = Substitute.For<IMarketDataCommandApi>();
        commands.ImportYieldCurveRatesAsync(Arg.Any<DateTime>())
            .Returns(new ServiceResult<Guid>(409, "duplicate"));
        var coordinator = Create(commands);

        var result = await coordinator.ImportAsync(new(
            first,
            second,
            IncludeEconomicCalendar: false));

        Assert.Equal(1, result.RejectedSubmissions);
        Assert.Equal(1, result.RemainingUnsubmittedDates);
        Assert.Equal(0, result.SubmittedCommands);
        var rejected = Assert.Single(result.Dates);
        Assert.Equal(FmpImportDateStatus.Rejected, rejected.Status);
        Assert.Null(rejected.CommandId);
        await commands.Received(1).ImportYieldCurveRatesAsync(Arg.Any<DateTime>());
    }

    private static FmpMarketDataImportCoordinator Create(IMarketDataCommandApi commands) =>
        new(
            commands,
            new FmpMarketDataImportOptions(),
            NullLogger<FmpMarketDataImportCoordinator>.Instance);

    private static IMarketDataCommandApi SuccessfulCommands()
    {
        var commands = Substitute.For<IMarketDataCommandApi>();
        commands.ImportYieldCurveRatesAsync(Arg.Any<DateTime>())
            .Returns(_ => new ServiceResult<Guid>(Guid.NewGuid()));
        commands.ImportEconomicCalendarsAsync(Arg.Any<DateTime>(), Arg.Any<string[]?>())
            .Returns(_ => new ServiceResult<Guid>(Guid.NewGuid()));
        return commands;
    }
}
