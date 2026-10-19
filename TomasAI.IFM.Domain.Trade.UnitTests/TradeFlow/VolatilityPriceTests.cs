using FluentAssertions;
using MessagePack;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class VolatilityPriceTests
{
    static readonly DateTimeOffset At = new(2026, 10, 16, 15, 0, 0, TimeSpan.Zero);
    static FuturesMarketPriceSnapshot Price => new("VX-front", 1, 1,
        TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.AssetTypeId.Futures,
        new(2026, 10, 16), new(18, 1, 20, 1, 1, 1, 1, At, At), new(18.25m, 1, 1, At, At));

    [Fact]
    public void Latest_valid_trade_or_newer_midpoint_supplies_numeric_price()
    {
        VolatilityMarketPriceReader.Resolve(Price, At).Should().Be(18.25m);
        VolatilityMarketPriceReader.Resolve(Price with { Quote = Price.Quote!.Value with { EventTimestamp = At.AddSeconds(1) } }, At.AddSeconds(1)).Should().Be(19);
        VolatilityMarketPriceReader.Resolve(Price with { Trade = null }, At).Should().Be(19);
        VolatilityMarketPriceReader.Resolve(Price with { Quote = Price.Quote!.Value with { BidPrice = 21 } }, At).Should().Be(18.25m);
        VolatilityMarketPriceReader.Resolve(Price, At.AddSeconds(-1)).Should().BeNull();
        VolatilityMarketPriceReader.Resolve(Price with { Quote = null, Trade = null }, At).Should().BeNull();
    }

    [Fact]
    public void Reader_resolves_selected_front_contract_and_requires_matching_session()
    {
        var market = Substitute.For<IMarketDataApi>();
        market.TryGetOnTheRunFuturesContract("VX", out Arg.Any<FuturesContractV3ReadModel>())
            .Returns(call => { call[1] = new FuturesContractV3ReadModel { ContractId = "VX-front", Symbol = "VX" }; return true; });
        market.TryGetLastTickPrice("VX-front", out Arg.Any<FuturesMarketPriceSnapshot>())
            .Returns(call => { call[1] = Price; return true; });
        VolatilityMarketPriceReader.Read(market, "VX", new(2026, 10, 16), At.UtcDateTime).Should().Be(18.25m);
        VolatilityMarketPriceReader.Read(market, "VX", new(2026, 10, 15), At.UtcDateTime).Should().BeNull();
        VolatilityMarketPriceReader.Read(market, "OTHER", new(2026, 10, 16), At.UtcDateTime).Should().BeNull();
    }

    [Fact]
    public void Price_populates_snapshot_independently_of_missing_option_risk_and_round_trips()
    {
        var position = new StrategyPositionSnapshot { ValueDate = new(2026, 10, 16) };
        var inputs = IronCondorTradePlanSnapshotCalculator.CaptureUnavailableInputs(position, new() { VolatilityPrice = 18.25m });
        var snapshot = new IronCondorTradePlanSnapshotCalculator().Create(new() { Position = position,
            ValueDate = position.ValueDate, CalculatedAtUtc = At.UtcDateTime }, Guid.NewGuid(), null, null, inputs);
        snapshot.VolatilityPrice.Should().Be(18.25m);
        var restored = MessagePackSerializer.Deserialize<IronCondorTradePlanSnapshot>(MessagePackSerializer.Serialize(snapshot));
        restored.VolatilityPrice.Should().Be(18.25m);
        typeof(IronCondorTradePlanSnapshot).GetProperty("VixVolatility").Should().BeNull();
        var old = MessagePackSerializer.Deserialize<IronCondorTradePlanSnapshot>(MessagePackSerializer.Serialize(new OldClassification()));
        old.VolatilityPrice.Should().BeNull();
    }

    [Fact]
    public async Task Monitoring_capture_reads_current_vx_price_without_waiting_for_option_risk()
    {
        var market = Substitute.For<IMarketDataApi>();
        market.TryGetOnTheRunFuturesContract("VX", out Arg.Any<FuturesContractV3ReadModel>())
            .Returns(call => { call[1] = new FuturesContractV3ReadModel { ContractId = "VX-front" }; return true; });
        var cached = Price;
        market.TryGetLastTickPrice("VX-front", out Arg.Any<FuturesMarketPriceSnapshot>())
            .Returns(call => { call[1] = cached; return true; });
        var databases = Substitute.For<TomasAI.IFM.Application.Storage.IDbContextFactory>();
        var trades = Substitute.For<TomasAI.IFM.Application.Storage.TradeDb.ITradeDbContext>();
        databases.TradeDb.Returns(trades);
        var id = new TradeEntityId(1, 2, 3, 4);
        TradeLegDefinition[] legs = [
            new() { TradeLegId = Guid.NewGuid(), ContractId = "SP", PutCall = 2, Strike = 7375, SignedQuantity = -1 },
            new() { TradeLegId = Guid.NewGuid(), ContractId = "LP", PutCall = 2, Strike = 7325, SignedQuantity = 1 },
            new() { TradeLegId = Guid.NewGuid(), ContractId = "SC", PutCall = 1, Strike = 8050, SignedQuantity = -1 },
            new() { TradeLegId = Guid.NewGuid(), ContractId = "LC", PutCall = 1, Strike = 8100, SignedQuantity = 1 } ];
        trades.GetEstablishedTradeAsync(id, Arg.Any<CancellationToken>()).Returns(new EstablishedTradeDefinition
        { Id = id, StrategyKind = TradeStrategyKind.IronCondor, Legs = legs.Select(leg => leg with { Expiry = new DateOnly(2026, 11, 20) }).ToArray() });
        using var stop = new CancellationTokenSource();
        var reader = new IronCondorMonitoringInputReader(databases, NSubstitute.Substitute.For<Microsoft.Extensions.Logging.ILogger>(),
            stoppingToken: stop.Token, marketData: market);
        var position = new StrategyPositionSnapshot { Id = StrategyPositionId.Create(id, TradeStrategyKind.IronCondor) };
        IronCondorTradePlanInputs? captured = null;
        for (var i = 0; i < 200 && captured is null; i++)
        { captured = reader.Capture(position, Price.ValueDate, At.UtcDateTime); if (captured is null) await Task.Delay(10); }
        captured.Should().NotBeNull(); captured!.VolatilityPrice.Should().Be(18.25m);
        cached = cached with { Trade = cached.Trade!.Value with { LastPrice = 20.5m, EventTimestamp = At.AddSeconds(1) } };
        reader.Capture(position, Price.ValueDate, At.AddSeconds(1).UtcDateTime)!.VolatilityPrice.Should().Be(20.5m);
        await stop.CancelAsync();
    }

    [MessagePackObject]
    public sealed record OldClassification
    {
        [Key(27)] public string VixVolatility { get; init; } = "Low";
        [Key(48)] public StrategyPositionSnapshot Position { get; init; } = new();
        [Key(49)] public Guid SourceEventId { get; init; }
        [Key(51)] public string[] UnavailableReasons { get; init; } = [];
        [Key(52)] public string CalculationVersion { get; init; } = "OldClassification";
    }
}
