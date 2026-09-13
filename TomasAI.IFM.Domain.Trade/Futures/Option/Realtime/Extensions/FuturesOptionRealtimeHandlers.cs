using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Model;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Extensions;

public static partial class FuturesOptionRealtimeHandlers
{
    public static ValueTask ExecuteAsync(
        this OpenPositionRoutesChangedEvent changed,
        IFuturesOptionRealtimeContext context)
    {
        context.RouteIndex.RemovePosition(changed.Position.Id.PositionId);
        if (!changed.Position.IsOpen) return ValueTask.CompletedTask;

        foreach (var leg in changed.Position.Legs)
        {
            var route = new MarketPositionRoute(
                changed.Position.Id.Trade.PortfolioId,
                changed.Position.Id.Trade.FundId,
                changed.Position.Id.Trade.OrderId,
                changed.Position.Id.Trade.TradeId,
                changed.Position.Id.PositionId,
                leg.TradeLegId,
                changed.Position.StrategyKind,
                ActorName(changed.Position.StrategyKind),
                changed.Position.Id.Format(),
                changed.Position.RouteGeneration);
            if (IsSupported(route)) context.RouteIndex.Add(route, leg.MarketInstrumentId);
        }
        return ValueTask.CompletedTask;
    }

    public static async ValueTask ExecuteAsync(
        this FuturesTickTradeDataChangedEvent changed,
        IFuturesOptionRealtimeContext context)
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
            if (!IsSupported(route)) continue;
            var positionId = new StrategyPositionId(
                new TradeEntityId(route.PortfolioId, route.FundId, route.OrderId, route.TradeId),
                route.StrategyPositionId);
            if (route.StrategyKind == TradeStrategyKind.IronCondor)
                await SendIronCondorUpdateAsync(changed, tick, route, positionId, context).ConfigureAwait(false);
            else
                await SendVerticalSpreadUpdateAsync(changed, tick, route, positionId, context).ConfigureAwait(false);
        }
    }

    static async ValueTask SendIronCondorUpdateAsync(
        FuturesTickTradeDataChangedEvent changed,
        PositionMarketTick tick,
        MarketPositionRoute route,
        StrategyPositionId positionId,
        IFuturesOptionRealtimeContext context)
    {
        var command = new UpdateIronCondorPositionLegMarketPriceCommand
        {
            CommandId = TickCommandId(changed.Id, route.StrategyPositionId, route.TradeLegId),
            Subject = new ActorSubject(
                ActorType.Command,
                FuturesIronCondorTradePositionCommandActor.ActorName,
                UpdateIronCondorPositionLegMarketPriceCommand.Verb,
                route.PositionActorThreadId),
            EntityId = positionId,
            TradeLegId = route.TradeLegId,
            Price = tick.Price,
            SourceSequence = tick.SourceSequence,
            RouteGeneration = route.Generation,
            EffectiveAtUtc = tick.OccurredAtUtc
        };
        var result = await context.ActorService
            .SendAsync<UpdateIronCondorPositionLegMarketPriceCommand, StrategyPositionId>(command, positionId)
            .ConfigureAwait(false);
        EnsureSent(result, "IRON_CONDOR_POSITION.ROUTE_SEND_FAILED");
    }

    static async ValueTask SendVerticalSpreadUpdateAsync(
        FuturesTickTradeDataChangedEvent changed,
        PositionMarketTick tick,
        MarketPositionRoute route,
        StrategyPositionId positionId,
        IFuturesOptionRealtimeContext context)
    {
        var command = new UpdateVerticalSpreadPositionLegMarketPriceCommand
        {
            CommandId = TickCommandId(changed.Id, route.StrategyPositionId, route.TradeLegId),
            Subject = new ActorSubject(
                ActorType.Command,
                FuturesVerticalSpreadTradePositionCommandActor.ActorName,
                UpdateVerticalSpreadPositionLegMarketPriceCommand.Verb,
                route.PositionActorThreadId),
            EntityId = positionId,
            TradeLegId = route.TradeLegId,
            Price = tick.Price,
            SourceSequence = tick.SourceSequence,
            RouteGeneration = route.Generation,
            EffectiveAtUtc = tick.OccurredAtUtc
        };
        var result = await context.ActorService
            .SendAsync<UpdateVerticalSpreadPositionLegMarketPriceCommand, StrategyPositionId>(command, positionId)
            .ConfigureAwait(false);
        EnsureSent(result, "VERTICAL_SPREAD_POSITION.ROUTE_SEND_FAILED");
    }

    static void LogIgnoredTick(
        IFuturesOptionRealtimeContext context,
        uint marketInstrumentId,
        MarketRouteLookupOutcome outcome)
    {
        if (!context.RouteIndex.ShouldLog(marketInstrumentId, outcome)) return;
        if (outcome == MarketRouteLookupOutcome.UnknownInstrument)
            UnknownInstrument(context.Logger, marketInstrumentId);
        else if (outcome == MarketRouteLookupOutcome.NoOpenPosition)
            NoOpenPosition(context.Logger, marketInstrumentId);
    }

    static void EnsureSent(ServiceResult<Guid> result, string code)
    {
        if (!result.Success)
            throw new InvalidOperationException($"{code};{result.ErrorCode};{result.ErrorMessage}");
    }

    static bool IsSupported(MarketPositionRoute route) =>
        route.StrategyKind is TradeStrategyKind.IronCondor or TradeStrategyKind.VerticalSpread;

    static string ActorName(TradeStrategyKind strategyKind) =>
        strategyKind == TradeStrategyKind.IronCondor
            ? FuturesIronCondorTradePositionCommandActor.ActorName
            : FuturesVerticalSpreadTradePositionCommandActor.ActorName;

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

    [LoggerMessage(LogLevel.Warning, "Ignoring Futures Option tick for unknown market instrument {MarketInstrumentId}.")]
    static partial void UnknownInstrument(ILogger logger, uint marketInstrumentId);

    [LoggerMessage(LogLevel.Information, "Ignoring Futures Option tick because market instrument {MarketInstrumentId} has no open position.")]
    static partial void NoOpenPosition(ILogger logger, uint marketInstrumentId);
}
