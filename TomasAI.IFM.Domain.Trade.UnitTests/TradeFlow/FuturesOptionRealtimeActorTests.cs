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
