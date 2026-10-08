using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Models;

public class UiDateTimeBoundaryTests
{
    static readonly DateTime EasternStart = new(2026, 7, 15, 9, 30, 0);
    static readonly DateTime EasternEnd = new(2026, 7, 15, 16, 0, 0);
    static readonly DateTime UtcStart = new(2026, 7, 15, 13, 30, 0, DateTimeKind.Utc);
    static readonly DateTime UtcEnd = new(2026, 7, 15, 20, 0, 0, DateTimeKind.Utc);
    static readonly DateOnly ValueDate = new(2026, 7, 15);

    [Fact]
    public async Task MarketDataCommands_PreserveEasternImportCalendarDateInUtcEncoding()
    {
        var api = Substitute.For<IMarketDataCommandApi>();
        var easternEvening = new DateTime(2026, 7, 15, 21, 30, 0);
        var utcDate = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        api.ImportYieldCurveRatesAsync(utcDate).Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        api.ImportEconomicCalendarsAsync(utcDate, Arg.Any<string[]?>())
            .Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        var model = new MarketDataCommandService(api);

        await model.ImportYieldCurveRatesAsync(easternEvening);
        await model.ImportEconomicCalendarsAsync(easternEvening, ["US"]);

        await api.Received(1).ImportYieldCurveRatesAsync(utcDate);
        await api.Received(1).ImportEconomicCalendarsAsync(
            utcDate,
            Arg.Is<string[]>(values => values.SequenceEqual(new[] { "US" })));
    }

    [Fact]
    public async Task FuturesBarQuery_ConvertsEasternWindowToUtc()
    {
        var api = Substitute.For<IMarketDataFeedQueryApi>();
        api.GetFuturesBarDataAsync("ESU6", "ES", ValueDate, UtcStart, UtcEnd)
            .Returns(new ServiceOk<FuturesBarDataReadModel[]>([]));
        var model = new MarketDataFeedQueryService(api);

        await model.GetFuturesBarDataAsync(
            "ESU6",
            "ES",
            ValueDate,
            EasternStart,
            EasternEnd,
            _ => { });

        await api.Received(1).GetFuturesBarDataAsync(
            "ESU6",
            "ES",
            ValueDate,
            UtcStart,
            UtcEnd);
    }

    [Theory]
    [InlineData("ES")]
    [InlineData("VX")]
    public async Task FuturesBarWindow_IncludesEndedSessionAfterValueDateRollover(string symbol)
    {
        var api = Substitute.For<IMarketDataFeedQueryApi>();
        var end = new DateTime(2026, 10, 7, 22, 15, 0, DateTimeKind.Utc);
        var start = end.AddHours(-6);
        var endedDate = new DateOnly(2026, 10, 7);
        var activeDate = endedDate.AddDays(1);
        FuturesBarDataReadModel Bar(DateOnly date, DateTime timestamp, decimal price) => new(
            "contract", symbol, date, timestamp, BarRateType.FifteenSeconds, price, 0, 0);
        var previous = Bar(endedDate, end.AddHours(-5), 100m);
        var current = Bar(activeDate, end.AddMinutes(-1), 101m);
        api.GetFuturesBarDataAsync("contract", symbol, endedDate, start, end)
            .Returns(new ServiceOk<FuturesBarDataReadModel[]>([previous, Bar(endedDate, start.AddSeconds(-1), 99m)]));
        api.GetFuturesBarDataAsync("contract", symbol, activeDate, start, end)
            .Returns(new ServiceOk<FuturesBarDataReadModel[]>([current]));
        FuturesBarDataReadModel[] result = [];

        await new MarketDataFeedQueryService(api).GetFuturesBarWindowAsync(
            "contract", symbol, activeDate, start, end, values => result = values);

        Assert.Equal(new[] { previous, current }, result);
        await api.Received(1).GetFuturesBarDataAsync("contract", symbol, endedDate, start, end);
        await api.Received(1).GetFuturesBarDataAsync("contract", symbol, activeDate, start, end);
        Assert.Equal(2, api.ReceivedCalls().Count());
    }

    [Fact]
    public async Task FuturesBarWindow_AfterMidnightReadsOnlyRelevantDatesAndKeepsUtcWindow()
    {
        var api = Substitute.For<IMarketDataFeedQueryApi>();
        var end = new DateTime(2026, 10, 8, 5, 0, 0, DateTimeKind.Utc);
        var start = end.AddHours(-6);
        var date = new DateOnly(2026, 10, 8);
        api.GetFuturesBarDataAsync("contract", "ES", Arg.Any<DateOnly>(), start, end)
            .Returns(new ServiceOk<FuturesBarDataReadModel[]>([]));

        await new MarketDataFeedQueryService(api).GetFuturesBarWindowAsync(
            "contract", "ES", date, start, end, _ => { });

        await api.Received(1).GetFuturesBarDataAsync("contract", "ES", date.AddDays(-1), start, end);
        await api.Received(1).GetFuturesBarDataAsync("contract", "ES", date, start, end);
        Assert.Equal(2, api.ReceivedCalls().Count());
    }

    [Fact]
    public async Task EconomicCalendarQuery_ConvertsEasternDateToUtc()
    {
        var api = Substitute.For<IMarketDataQueryApi>();
        var feedApi = Substitute.For<IMarketDataFeedQueryApi>();
        api.GetEconomicCalendarsAsync(UtcStart, EconomicCalendarViewType.Today, "US")
            .Returns(new ServiceOk<EconomicCalendarReadModel[]>([]));
        var model = new MarketDataQueryService(api, feedApi);

        await model.LoadEconomicCalendarAsync(
            EasternStart,
            EconomicCalendarViewType.Today,
            "US",
            _ => { });

        await api.Received(1).GetEconomicCalendarsAsync(
            UtcStart,
            EconomicCalendarViewType.Today,
            "US");
    }

    [Fact]
    public async Task TradeBarQuery_ConvertsEasternWindowToUtc()
    {
        var api = Substitute.For<ITradeQueryApi>();
        api.GetOptionTradeSpreadBarDataAsync(
                1,
                2,
                TradeType.ShortIronCondor,
                ValueDate,
                UtcStart,
                UtcEnd)
            .Returns(new ServiceOk<OptionTradeSpreadBarsDataModel[]>([]));
        var model = new TradeQueryService(api);

        await model.GetOptionTradeSpreadBarDataAsync(
            1,
            2,
            TradeType.ShortIronCondor,
            ValueDate,
            EasternStart,
            EasternEnd,
            _ => { });

        await api.Received(1).GetOptionTradeSpreadBarDataAsync(
            1,
            2,
            TradeType.ShortIronCondor,
            ValueDate,
            UtcStart,
            UtcEnd);
    }
}
