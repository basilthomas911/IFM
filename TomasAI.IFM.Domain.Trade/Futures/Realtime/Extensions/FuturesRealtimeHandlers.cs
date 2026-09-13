using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.Model;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Realtime.Extensions;

public static partial class FuturesRealtimeHandlers
{
    public static ValueTask ExecuteAsync(
        this OpenPositionRoutesChangedEvent changed,
        IFuturesRealtimeContext context)
    {
        context.RouteIndex.RemovePosition(changed.Position.Id.PositionId);
        if (!changed.Position.IsOpen || changed.Position.StrategyKind != TradeStrategyKind.FuturesOutright)
            return ValueTask.CompletedTask;

        foreach (var leg in changed.Position.Legs)
        {
            context.RouteIndex.Add(new MarketPositionRoute(
                changed.Position.Id.Trade.PortfolioId,
                changed.Position.Id.Trade.FundId,
                changed.Position.Id.Trade.OrderId,
                changed.Position.Id.Trade.TradeId,
                changed.Position.Id.PositionId,
                leg.TradeLegId,
                TradeStrategyKind.FuturesOutright,
                FuturesTradePositionCommandActor.ActorName,
                changed.Position.Id.Format(),
                changed.Position.RouteGeneration), leg.MarketInstrumentId);
        }
        return ValueTask.CompletedTask;
    }

    public static async ValueTask ExecuteAsync(
        this FuturesTickTradeDataChangedEvent changed,
        IFuturesRealtimeContext context)
    {
        var receivedAtUtc = changed.ReceivedOn.Kind == DateTimeKind.Utc
            ? changed.ReceivedOn
            : DateTime.SpecifyKind(changed.ReceivedOn, DateTimeKind.Utc);
        var tick = new PositionMarketTick(
            changed.InstrumentId,
            changed.TradeData.Price,
            changed.TradeData.SourceSequence,
            receivedAtUtc);
        var outcome = context.RouteIndex.TryRoute(in tick, out var routes);
        if (outcome != MarketRouteLookupOutcome.Routed)
        {
            LogIgnoredTick(context, changed.InstrumentId, outcome);
            return;
        }

        foreach (var route in routes)
        {
            if (route.StrategyKind != TradeStrategyKind.FuturesOutright) continue;
            var positionId = new StrategyPositionId(
                new TradeEntityId(route.PortfolioId, route.FundId, route.OrderId, route.TradeId),
                route.StrategyPositionId);
            var command = new UpdateFuturesPositionMarketPriceCommand
            {
                CommandId = TickCommandId(changed.Id, route.StrategyPositionId, route.TradeLegId),
                Subject = new ActorSubject(
                    ActorType.Command,
                    FuturesTradePositionCommandActor.ActorName,
                    UpdateFuturesPositionMarketPriceCommand.Verb,
                    route.PositionActorThreadId),
                EntityId = positionId,
                TradeLegId = route.TradeLegId,
                Price = tick.Price,
                SourceSequence = tick.SourceSequence,
                RouteGeneration = route.Generation,
                EffectiveAtUtc = tick.OccurredAtUtc
            };
            var result = await context.ActorService
                .SendAsync<UpdateFuturesPositionMarketPriceCommand, StrategyPositionId>(command, positionId)
                .ConfigureAwait(false);
            if (!result.Success)
                throw new InvalidOperationException(
                    $"FUTURES_POSITION.ROUTE_SEND_FAILED;{result.ErrorCode};{result.ErrorMessage}");
        }
    }

    static void LogIgnoredTick(
        IFuturesRealtimeContext context,
        uint marketInstrumentId,
        MarketRouteLookupOutcome outcome)
    {
        if (!context.RouteIndex.ShouldLog(marketInstrumentId, outcome)) return;
        if (outcome == MarketRouteLookupOutcome.UnknownInstrument)
            UnknownInstrument(context.Logger, marketInstrumentId);
        else if (outcome == MarketRouteLookupOutcome.NoOpenPosition)
            NoOpenPosition(context.Logger, marketInstrumentId);
    }

    static Guid TickCommandId(Guid sourceEventId, Guid positionId, Guid legId)
    {
        Span<byte> result = stackalloc byte[16];
        Span<byte> value = stackalloc byte[16];
        sourceEventId.TryWriteBytes(result);
        positionId.TryWriteBytes(value);
        for (var index = 0; index < 16; index++) result[index] ^= value[index];
        legId.TryWriteBytes(value);
        for (var index = 0; index < 16; index++) result[index] ^= value[index];
        return new Guid(result);
    }

    [LoggerMessage(LogLevel.Warning, "Ignoring Futures tick for unknown market instrument {MarketInstrumentId}.")]
    static partial void UnknownInstrument(ILogger logger, uint marketInstrumentId);

    [LoggerMessage(LogLevel.Information, "Ignoring Futures tick because market instrument {MarketInstrumentId} has no open position.")]
    static partial void NoOpenPosition(ILogger logger, uint marketInstrumentId);
}
