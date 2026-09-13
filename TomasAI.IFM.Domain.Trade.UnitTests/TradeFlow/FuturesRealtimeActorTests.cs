using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Extensions;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class FuturesRealtimeActorTests
{
    static readonly DateTime Now = new(2026, 9, 12, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Routed_tick_sends_one_futures_position_update()
    {
        var actorService = Substitute.For<IActorService>();
        actorService.SendAsync<UpdateFuturesPositionMarketPriceCommand, StrategyPositionId>(
                Arg.Any<UpdateFuturesPositionMarketPriceCommand>(), Arg.Any<StrategyPositionId>())
            .Returns(new ServiceResult<Guid>(Guid.NewGuid()));
        var context = Substitute.For<IFuturesRealtimeContext>();
        var routes = new MarketInstrumentRouteIndex();
        context.ActorService.Returns(actorService);
        context.RouteIndex.Returns(routes);
        var positionId = Guid.NewGuid();
        var legId = Guid.NewGuid();
        routes.Add(new MarketPositionRoute(
            1, 2, 3, 4, positionId, legId, TradeStrategyKind.FuturesOutright,
            FuturesPositionActorNames.Command, $"1.2.3.4.{positionId:N}", 1), 77);
        var tick = new FuturesTickTradeDataChangedEvent
        {
            Id = Guid.NewGuid(),
            InstrumentId = 77,
            ReceivedOn = Now,
            TradeData = new FuturesTickTradeData(101, 0, 0, 0, 0, 5150.25m, 1, 0, 0, 0)
        };

        await tick.ExecuteAsync(context);

        await actorService.Received(1)
            .SendAsync<UpdateFuturesPositionMarketPriceCommand, StrategyPositionId>(
                Arg.Is<UpdateFuturesPositionMarketPriceCommand>(command =>
                    command.EntityId.PositionId == positionId &&
                    command.TradeLegId == legId &&
                    command.Price == 5150.25m &&
                    command.SourceSequence == 101 &&
                    command.RouteGeneration == 1),
                Arg.Is<StrategyPositionId>(id => id.PositionId == positionId));
    }

    [Fact]
    public async Task Tick_for_known_contract_without_open_position_is_ignored()
    {
        var context = Substitute.For<IFuturesRealtimeContext>();
        var routes = new MarketInstrumentRouteIndex();
        var actorService = Substitute.For<IActorService>();
        routes.RegisterKnownInstrument(77);
        context.ActorService.Returns(actorService);
        context.RouteIndex.Returns(routes);
        var tick = new FuturesTickTradeDataChangedEvent
        {
            Id = Guid.NewGuid(),
            InstrumentId = 77,
            ReceivedOn = Now,
            TradeData = new FuturesTickTradeData(1, 0, 0, 0, 0, 5150.25m, 1, 0, 0, 0)
        };

        var act = async () => await tick.ExecuteAsync(context);

        await act.Should().NotThrowAsync();
        routes.UnroutedTicks.Should().Be(1);
        actorService.ReceivedCalls().Should().BeEmpty();
    }
}
