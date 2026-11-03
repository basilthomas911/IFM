using MessagePack;
using NSubstitute;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using Xunit;
namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;
public sealed class IronCondorChartRangeTests
{
    [Theory]
    [InlineData("2026-10-22T22:30:00Z", "2026-10-23", "2026-10-22T22:30:00Z")]
    [InlineData("2026-10-22T22:30:00Z", "2026-10-22", "2026-10-22T21:00:00Z")]
    [InlineData("2026-10-20T20:00:00Z", "2026-10-23", "2026-10-20T20:00:00Z")]
    public void Lifetime_axis_is_independent_of_observation_end(string now, string valueDate, string expectedEnd)
    {
        var window = GetIronCondorTradePlanChart.ResolveWindow(new(2026,10,19), new(2026,11,6), DateOnly.Parse(valueDate),
            DateTimeOffset.Parse(now), Utc("2026-10-19T14:00:00Z"), null);
        Assert.Equal(Utc("2026-10-18T22:00:00Z"), window.StartUtc);
        Assert.Equal(Utc("2026-11-06T22:00:00Z"), window.EndUtc);
        Assert.Equal(Utc(expectedEnd), window.ObservationEndUtc);
        Assert.Equal(TomasAI.IFM.Domain.MarketData.Shared.FuturesTradingValueDate.GetSessionStartUtc(DateOnly.Parse(valueDate)).UtcDateTime, window.SessionStartUtc);
    }

    [Fact]
    public void Observations_stop_at_close_and_start_at_actual_establishment()
    {
        var window = GetIronCondorTradePlanChart.ResolveWindow(new(2026,10,19), new(2026,11,6), new(2026,10,19),
            DateTimeOffset.Parse("2026-11-10T00:00:00Z"), Utc("2026-10-19T14:00:00Z"), Utc("2026-10-19T16:00:00Z"));
        Assert.Equal(Utc("2026-10-19T14:00:00Z"), window.SessionStartUtc);
        Assert.Equal(Utc("2026-10-19T16:00:00Z"), window.ObservationEndUtc);
    }

    [Fact]
    public void Eod_history_uses_latest_sealed_day_and_signed_price_per_unit_without_filling_gaps()
    {
        var trade = new TomasAI.IFM.Domain.Trade.Shared.EstablishedTradeDefinition {
            StrategyKind = TomasAI.IFM.Domain.Trade.Shared.TradeStrategyKind.IronCondor,
            EstablishedAtUtc = Utc("2026-10-19T14:00:00Z"),
            Legs = [new() { TradeLegId = Guid.NewGuid(), Expiry = new(2026,11,6) }] };
        var day = new TomasAI.IFM.Domain.Trade.Shared.StrategyPositionSnapshot {
            Phase = TomasAI.IFM.Domain.Trade.Shared.StrategyPositionPhase.EndOfDay,
            ValueDate = new(2026,10,19), AsOfUtc = Utc("2026-10-19T21:00:00Z"), PositionSequence = 1,
            Legs = [new() { SignedQuantity = -2, CurrentPrice = 10 }, new() { SignedQuantity = 2, CurrentPrice = 3 }] };
        var points = GetIronCondorTradePlanChart.DailyPoints([
            day, day with { PositionSequence = 2, Legs = [new() { SignedQuantity = -2, CurrentPrice = 12 }, new() { SignedQuantity = 2, CurrentPrice = 3 }] },
            day with { Phase = TomasAI.IFM.Domain.Trade.Shared.StrategyPositionPhase.MarkToMarket, ValueDate = new(2026,10,20) },
            day with { ValueDate = new(2026,10,21), AsOfUtc = Utc("2026-10-21T21:00:00Z") }], trade, new(2026,10,21), Utc("2026-10-21T22:00:00Z"));
        Assert.Single(points);
        Assert.Equal(-9m, points[0].NetSpread);
        Assert.Equal(Utc("2026-10-19T21:00:00Z"), points[0].AtUtc);
        Assert.Null(points[0].ForwardSpread);
    }

    [Fact]
    public async Task Lifetime_series_combines_all_eod_pages_with_only_the_selected_session()
    {
        var tradeDb = Substitute.For<ITradeDbReadContext>();
        var planDb = Substitute.For<ITradePlanDbReadContext>();
        var trade = Trade();
        var position = StrategyPositionId.Create(trade.Id, TradeStrategyKind.IronCondor);
        var date = new DateOnly(2026, 10, 22);
        var end = Utc("2026-10-22T16:00:00Z");
        var sessionStart = Utc("2026-10-21T22:00:00Z");
        byte[] cursor = [1];
        tradeDb.GetEstablishedTradeAsync(trade.Id, default).Returns(trade);
        tradeDb.GetStrategyPositionHistoryAsync(position.PositionId, Utc("2026-10-18T22:00:00Z"), end, 1000,
            Arg.Any<byte[]?>(), default).Returns(call => call.ArgAt<byte[]?>(4) is null
                ? new QueryPage<StrategyPositionSnapshot>([Eod(trade, new(2026,10,19)), Eod(trade, new(2026,10,20)) with {
                    Id = new(position.Trade, Guid.NewGuid()) }], cursor)
                : new QueryPage<StrategyPositionSnapshot>([Eod(trade, new(2026,10,21)), Eod(trade, date) with {
                    Phase = StrategyPositionPhase.MarkToMarket }], null));
        planDb.GetChartRangeAsync(position, date, date, sessionStart, end, default)
            .Returns(new StrategyTradePlanChartSeries(sessionStart, end, [new(end, -12m)]));

        var series = await GetIronCondorTradePlanChart.LoadSeriesAsync(tradeDb, planDb, position, date,
            new DateTimeOffset(end), default);

        Assert.Equal(Utc("2026-10-18T22:00:00Z"), series.StartUtc);
        Assert.Equal(Utc("2026-11-06T22:00:00Z"), series.EndUtc);
        Assert.Equal(new[] { Utc("2026-10-19T21:00:00Z"), Utc("2026-10-21T21:00:00Z"), end }, series.Points.Select(p => p.AtUtc));
        Assert.Equal(new[] { -14m, -14m, -12m }, series.Points.Select(p => p.NetSpread));
        await tradeDb.Received(2).GetStrategyPositionHistoryAsync(position.PositionId, series.StartUtc, end, 1000,
            Arg.Any<byte[]?>(), default);
        await planDb.Received(1).GetChartRangeAsync(position, date, date, sessionStart, end, default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Later_selected_date_keeps_lifetime_axis_without_reading_post_closure_intraday(bool matured)
    {
        var tradeDb = Substitute.For<ITradeDbReadContext>();
        var planDb = Substitute.For<ITradePlanDbReadContext>();
        var trade = Trade() with { ClosedAtUtc = matured ? null : Utc("2026-10-20T16:00:00Z") };
        var position = StrategyPositionId.Create(trade.Id, TradeStrategyKind.IronCondor);
        tradeDb.GetEstablishedTradeAsync(trade.Id, default).Returns(trade);
        tradeDb.GetStrategyPositionHistoryAsync(position.PositionId, Arg.Any<DateTime>(), Arg.Any<DateTime>(), 1000,
            Arg.Any<byte[]?>(), default).Returns(new QueryPage<StrategyPositionSnapshot>([Eod(trade, new(2026,10,19))], null));
        var series = await GetIronCondorTradePlanChart.LoadSeriesAsync(tradeDb, planDb, position,
            matured ? new(2026,11,9) : new(2026,10,22), DateTimeOffset.Parse("2026-11-10T16:00:00Z"), default);
        Assert.Single(series.Points);
        Assert.Equal(Utc("2026-11-06T22:00:00Z"), series.EndUtc);
        Assert.Empty(planDb.ReceivedCalls());
    }

    [Fact]
    public void Legacy_eod_uses_market_local_date_and_confirmed_close_boundary()
    {
        var trade = Trade();
        var day = Eod(trade, new(2026,10,19)) with {
            ValueDate = default, CloseBoundaryUtc = Utc("2026-10-19T20:00:00Z") };
        var points = GetIronCondorTradePlanChart.DailyPoints([day], trade, new(2026,10,20), Utc("2026-10-20T16:00:00Z"));
        Assert.Single(points);
        Assert.Equal(day.CloseBoundaryUtc, points[0].AtUtc);
        Assert.Equal(-14m, points[0].NetSpread);
    }

    static EstablishedTradeDefinition Trade() => new() {
        Id = new(1,2,3,4), StrategyKind = TradeStrategyKind.IronCondor,
        EstablishedAtUtc = Utc("2026-10-19T14:00:00Z"),
        Legs = Enumerable.Range(0,4).Select(index => new TradeLegDefinition {
            TradeLegId = Guid.NewGuid(), SignedQuantity = index % 2 == 0 ? -2 : 2,
            Expiry = new(2026,11,6) }).ToArray() };

    static StrategyPositionSnapshot Eod(EstablishedTradeDefinition trade, DateOnly date) => new() {
        Id = StrategyPositionId.Create(trade.Id, TradeStrategyKind.IronCondor), StrategyKind = TradeStrategyKind.IronCondor,
        ValueDate = date, Phase = StrategyPositionPhase.EndOfDay, PositionSequence = date.DayNumber,
        AsOfUtc = TomasAI.IFM.Domain.MarketData.Shared.FuturesTradingValueDate.GetSessionEndUtc(date).UtcDateTime,
        Legs = trade.Legs.Select(leg => new StrategyPositionLeg {
            TradeLegId = leg.TradeLegId, SignedQuantity = leg.SignedQuantity,
            CurrentPrice = leg.SignedQuantity < 0 ? 10m : 3m }).ToArray() };

    static DateTime Utc(string text) => DateTimeOffset.Parse(text).UtcDateTime;

    [Fact]
    public void Price_only_series_roundtrips_without_plan_payloads()
    {
        var start=new DateTime(2026,10,22,16,30,0,DateTimeKind.Utc);
        var series=new StrategyTradePlanChartSeries(start,start.AddHours(6),[new(start,-10m){ForwardSpread=-11m}]);
        var copy=MessagePackSerializer.Deserialize<StrategyTradePlanChartSeries>(MessagePackSerializer.Serialize(series));
        Assert.Equal(series.StartUtc,copy.StartUtc);Assert.Equal(series.EndUtc,copy.EndUtc);
        Assert.Equal(series.Points,copy.Points);
        var query=new GetIronCondorTradePlanChartQuery();
        Assert.Equal(query.ErrorCode,MessagePackSerializer.Deserialize<GetIronCondorTradePlanChartQuery>(MessagePackSerializer.Serialize(query)).ErrorCode);
    }
}
