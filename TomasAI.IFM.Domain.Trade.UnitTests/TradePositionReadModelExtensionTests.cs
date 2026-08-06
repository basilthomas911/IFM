using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.UnitTests;

public class TradePositionReadModelExtensionTests
{
    [Fact]
    public void Get_returns_latest_matching_value_date_from_unsorted_history()
    {
        var expected = Position(new DateOnly(2026, 8, 5), TradeStatus.IntraDay, 3m);
        TradePositionReadModel[] positions =
        [
            expected,
            Position(new DateOnly(2026, 8, 3), TradeStatus.IntraDay, 1m),
            Position(new DateOnly(2026, 8, 4), TradeStatus.IntraDay, 2m),
            Position(new DateOnly(2026, 8, 6), TradeStatus.EndOfDay, 4m)
        ];

        positions.Get(TradeType.PutCreditSpread, TradeStatus.IntraDay).Should().BeSameAs(expected);
    }

    [Fact]
    public void Get_by_value_date_returns_last_matching_position_without_string_conversion()
    {
        var valueDate = new DateOnly(2026, 8, 5);
        var expected = Position(valueDate, TradeStatus.IntraDay, 2m);
        TradePositionReadModel[] positions =
        [
            Position(valueDate, TradeStatus.IntraDay, 1m),
            Position(valueDate.AddDays(1), TradeStatus.IntraDay, 9m),
            expected
        ];

        positions.Get(TradeType.PutCreditSpread, TradeStatus.IntraDay, valueDate).Should().BeSameAs(expected);
    }

    [Fact]
    public void GetEodTradePnl_sums_opening_and_end_of_day_positions_only()
    {
        TradePositionReadModel[] positions =
        [
            Position(new DateOnly(2026, 8, 5), TradeStatus.Open, 10m),
            Position(new DateOnly(2026, 8, 5), TradeStatus.IntraDay, 100m),
            Position(new DateOnly(2026, 8, 5), TradeStatus.EndOfDay, 20m),
            Position(new DateOnly(2026, 8, 5), TradeStatus.Close, 200m)
        ];

        positions.GetEodTradePnl().Should().Be(30m);
    }

    static TradePositionReadModel Position(DateOnly valueDate, TradeStatus status, decimal tradePnl)
        => new()
        {
            OrderId = 1,
            TradeId = 2,
            TradeType = TradeType.PutCreditSpread,
            ValueDate = valueDate,
            DaysToExpiry = 30,
            TradeStatus = status,
            TradePnl = tradePnl
        };
}
