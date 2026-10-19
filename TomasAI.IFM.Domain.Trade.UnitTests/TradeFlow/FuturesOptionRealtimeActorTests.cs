using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class FuturesOptionRealtimeActorTests
{
    static readonly DateTime Now = new(2026, 9, 12, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Quote_and_same_tick_pricing_completion_carry_full_data_to_position_with_distinct_command_ids()
    {
        var commands = new List<ChangeTradeLegDataCommand>();
        var actorService = Substitute.For<IActorService>();
        actorService.SendAsync<ChangeTradeLegDataCommand, StrategyPositionId>(Arg.Any<ChangeTradeLegDataCommand>(), Arg.Any<StrategyPositionId>())
            .Returns(call => { commands.Add(call.Arg<ChangeTradeLegDataCommand>()); return new ServiceResult<Guid>(Guid.NewGuid()); });
        var context = Substitute.For<IFuturesOptionRealtimeContext>();
        var routes = new ContractIdRouteIndex();
        context.ActorService.Returns(actorService); context.RouteIndex.Returns(routes);
        routes.Add(new PortfolioFundTradeLeg(1, 2, 3, 4, Guid.NewGuid(), Guid.NewGuid(), TradeStrategyKind.IronCondor, 1), "ES-option");
        var quote = new TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionTickDataV2ReadModel
        {
            ContractId = "ES-option", TickId = 100, ValueDate = DateOnly.FromDateTime(Now), OptionPrice = 10,
            BidPrice = 9, AskPrice = 11, BidSize = 2, AskSize = 3, ObservedAtUtc = new(Now),
            SourceGeneration = Guid.NewGuid(), ObservationRevision = 1, GreeksAvailable = false, PricingFailure = "CalculationPending"
        };
        var changed = new FuturesTickTradeDataChangedEvent
        {
            Id = Guid.NewGuid(), ReceivedOn = Now, TickDataId = new("ES-option", quote.ValueDate, 100, Now),
            OptionMarketPriceObservation = new("ES", OptionMarketPriceBasis.QuoteMidpoint, quote, 100, Now)
        };
        await changed.ExecuteAsync(context);
        var risk = quote with { IsPricingUpdate = true, ObservationRevision = 2, GreeksAvailable = true, PricingFailure = null,
            ImpliedVolatility = .2, Delta = .4, Gamma = .01, Vega = 12, Theta = -3, Rho = .1,
            UnderlyingPrice = 5000, TheoreticalPrice = 10, CalculatedAtUtc = new(Now.AddMilliseconds(1)) };
        await (changed with { OptionMarketPriceObservation = new("ES", OptionMarketPriceBasis.QuoteMidpoint, risk, 100, Now) }).ExecuteAsync(context);
        commands.Should().HaveCount(2);
        commands[0].CommandId.Should().NotBe(commands[1].CommandId);
        commands[1].OptionTick.Should().Be(risk);
        commands[1].SourceSequence.Should().Be(100);
        var restored = MessagePack.MessagePackSerializer.Deserialize<ChangeTradeLegDataCommand>(MessagePack.MessagePackSerializer.Serialize(commands[1]));
        restored.OptionTick.Should().BeEquivalentTo(risk);
    }

    [Fact]
    public async Task Routed_tick_sends_one_strategy_specific_position_command()
    {
        var actorService = Substitute.For<IActorService>();
        actorService.SendAsync<ChangeTradeLegDataCommand, StrategyPositionId>(
                Arg.Any<ChangeTradeLegDataCommand>(), Arg.Any<StrategyPositionId>())
            .Returns(new ServiceResult<Guid>(Guid.NewGuid()));
        var context = Substitute.For<IFuturesOptionRealtimeContext>();
        var routes = new ContractIdRouteIndex();
        context.ActorService.Returns(actorService);
        context.RouteIndex.Returns(routes);
        var positionId = Guid.NewGuid();
        var legId = Guid.NewGuid();
        routes.Add(new PortfolioFundTradeLeg(
            1, 2, 3, 4, positionId, legId, TradeStrategyKind.IronCondor,
            1), "ESZ6 C5000");
        var tick = new FuturesTickTradeDataChangedEvent
        {
            Id = Guid.NewGuid(),
            InstrumentId = 77,
            TickDataId = new("ESZ6 C5000", DateOnly.FromDateTime(Now), 1, Now),
            ReceivedOn = Now,
            TradeData = new FuturesTickTradeData(101, 0, 0, 0, 0, 12.25m, 1, 0, 0, 0)
        };

        await tick.ExecuteAsync(context);

        await actorService.Received(1)
            .SendAsync<ChangeTradeLegDataCommand, StrategyPositionId>(
                Arg.Is<ChangeTradeLegDataCommand>(command =>
                    command.EntityId.PositionId == positionId &&
                    command.TradeLegId == legId &&
                    command.Price == 12.25m &&
                    command.SourceSequence == 101 &&
                    command.RouteGeneration == 1),
                Arg.Is<StrategyPositionId>(id => id.PositionId == positionId));
    }

    [Fact]
    public async Task Tick_without_an_open_position_is_counted_and_ignored()
    {
        var context = Substitute.For<IFuturesOptionRealtimeContext>();
        var routes = new ContractIdRouteIndex();
        var actorService = Substitute.For<IActorService>();
        routes.RegisterKnownContract("ESZ6 C5000");
        context.ActorService.Returns(actorService);
        context.RouteIndex.Returns(routes);
        var tick = new FuturesTickTradeDataChangedEvent
        {
            Id = Guid.NewGuid(),
            InstrumentId = 77,
            TickDataId = new("ESZ6 C5000", DateOnly.FromDateTime(Now), 1, Now),
            ReceivedOn = Now,
            TradeData = new FuturesTickTradeData(1, 0, 0, 0, 0, 12.25m, 1, 0, 0, 0)
        };

        var act = async () => await tick.ExecuteAsync(context);

        await act.Should().NotThrowAsync();
        routes.UnroutedTicks.Should().Be(1);
        actorService.ReceivedCalls().Should().BeEmpty();
    }
}
