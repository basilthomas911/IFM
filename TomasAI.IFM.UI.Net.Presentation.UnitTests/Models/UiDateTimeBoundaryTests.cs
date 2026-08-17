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
    public async Task MarketDataCommands_ConvertEasternImportDatesToUtc()
    {
        var api = Substitute.For<IMarketDataCommandApi>();
        api.ImportYieldCurveRatesAsync(UtcStart).Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        api.ImportEconomicCalendarsAsync(UtcStart, Arg.Any<string[]?>())
            .Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        var model = new MarketDataCommandModel(api);

        await model.ImportYieldCurveRatesAsync(EasternStart);
        await model.ImportEconomicCalendarsAsync(EasternStart, ["US"]);

        await api.Received(1).ImportYieldCurveRatesAsync(UtcStart);
        await api.Received(1).ImportEconomicCalendarsAsync(
            UtcStart,
            Arg.Is<string[]>(values => values.SequenceEqual(new[] { "US" })));
    }

    [Fact]
    public async Task FuturesBarQuery_ConvertsEasternWindowToUtc()
    {
        var api = Substitute.For<IMarketDataFeedQueryApi>();
        api.GetFuturesBarDataAsync("ESU6", "ES", ValueDate, UtcStart, UtcEnd)
            .Returns(new ServiceOk<FuturesBarDataReadModel[]>([]));
        var model = new MarketDataFeedQueryModel(api);

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

    [Fact]
    public async Task EconomicCalendarQuery_ConvertsEasternDateToUtc()
    {
        var api = Substitute.For<IMarketDataQueryApi>();
        var feedApi = Substitute.For<IMarketDataFeedQueryApi>();
        api.GetEconomicCalendarsAsync(UtcStart, EconomicCalendarViewType.Today, "US")
            .Returns(new ServiceOk<EconomicCalendarReadModel[]>([]));
        var model = new MarketDataQueryModel(api, feedApi);

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
        var model = new TradeQueryModel(api);

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
