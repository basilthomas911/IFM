using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesBarData;

public sealed class FuturesBarMarketPriceTests
{
    static readonly DateOnly ValueDate = new(2026, 8, 18);
    static readonly DateTimeOffset Timestamp =
        new(2026, 8, 18, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Vx_uses_newer_valid_quote_midpoint_when_trade_is_stale()
    {
        var snapshot = CreateSnapshot(
            new FuturesMarketQuoteSnapshot(
                21.10m, 4, 21.30m, 5, 1, 1, 12,
                Timestamp, Timestamp),
            new FuturesMarketTradeSnapshot(
                20.75m, 2, 11, Timestamp.AddSeconds(-30), Timestamp.AddSeconds(-30)));

        FuturesBarMarketPrice.TryResolve("VX", snapshot, out var price)
            .Should().BeTrue();
        price.Should().Be(21.20m);
    }

    [Fact]
    public void Vx_prefers_newer_trade_and_rejects_invalid_quote()
    {
        var newerTrade = new FuturesMarketTradeSnapshot(
            21.25m, 2, 13, Timestamp.AddSeconds(1), Timestamp.AddSeconds(1));
        var crossedQuote = new FuturesMarketQuoteSnapshot(
            21.40m, 4, 21.30m, 5, 1, 1, 12, Timestamp, Timestamp);

        FuturesBarMarketPrice.TryResolve(
                "VX",
                CreateSnapshot(crossedQuote, newerTrade),
                out var price)
            .Should().BeTrue();
        price.Should().Be(21.25m);
    }

    [Fact]
    public void Es_remains_trade_only()
    {
        var quote = new FuturesMarketQuoteSnapshot(
            6500m, 4, 6500.5m, 5, 1, 1, 12, Timestamp, Timestamp);

        FuturesBarMarketPrice.TryResolve(
                "ES",
                CreateSnapshot(quote, null),
                out _)
            .Should().BeFalse();
    }

    static FuturesMarketPriceSnapshot CreateSnapshot(
        FuturesMarketQuoteSnapshot? quote,
        FuturesMarketTradeSnapshot? trade) => new(
        "VX20260819",
        181_038,
        105,
        AssetTypeId.Futures,
        ValueDate,
        quote,
        trade);
}
