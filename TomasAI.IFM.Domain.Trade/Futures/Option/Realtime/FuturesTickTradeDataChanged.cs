using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Logging;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;

public static class FuturesTickTradeDataChanged
{
    public static async ValueTask ExecuteAsync(
        this FuturesTickTradeDataChangedEvent changed,
        IFuturesOptionRealtimeContext context)
    {
        var receivedAtUtc = changed.ReceivedOn.Kind == DateTimeKind.Utc
            ? changed.ReceivedOn
            : DateTime.SpecifyKind(changed.ReceivedOn, DateTimeKind.Utc);
        var tick = new PositionMarketTick(
            changed.TickDataId.ContractId,
            changed.TradeData.Price,
            changed.TradeData.SourceSequence,
            receivedAtUtc);
        var outcome = context.RouteIndex.TryRoute(in tick, out var routes);
        if (outcome != MarketRouteLookupOutcome.Routed)
        {
            LogIgnoredTick(context, changed.TickDataId.ContractId, outcome);
            return;
        }

        foreach (var route in routes)
        {
            if (!FuturesOptionRoutePolicy.IsSupported(route))
                continue;

            var positionId = new StrategyPositionId(
                new TradeEntityId(
                    route.PortfolioId,
                    route.FundId,
                    route.OrderId,
                    route.TradeId),
                route.StrategyPositionId);
            await SendTradeLegUpdateAsync(changed, tick, route, positionId, context)
                .ConfigureAwait(false);
        }
    }

    static async ValueTask SendTradeLegUpdateAsync(
        FuturesTickTradeDataChangedEvent changed,
        PositionMarketTick tick,
        PortfolioFundTradeLeg route,
        StrategyPositionId positionId,
        IFuturesOptionRealtimeContext context)
    {
        var actorName = route.TradeType == TradeStrategyKind.IronCondor
            ? FuturesIronCondorTradePositionCommandActor.ActorName
            : FuturesVerticalSpreadTradePositionCommandActor.ActorName;
        var command = new ChangeTradeLegDataCommand
        {
            CommandId = TickCommandId(changed.Id, route.StrategyPositionId, route.TradeLegId),
            Subject = new ActorSubject(
                ActorType.Command,
                actorName,
                ChangeTradeLegDataCommand.Verb,
                positionId.Format()),
            EntityId = positionId,
            TradeLegId = route.TradeLegId,
            ContractId = tick.ContractId,
            Price = tick.Price,
            SourceSequence = tick.SourceSequence,
            RouteGeneration = route.Generation,
            EffectiveAtUtc = tick.OccurredAtUtc,
            TradeType = route.TradeType
        };
        var result = await context.ActorService
            .SendAsync<ChangeTradeLegDataCommand, StrategyPositionId>(command, positionId)
            .ConfigureAwait(false);
        if (!result.Success)
            FuturesTickTradeDataChangedLogging.RouteSendFailed(
                context.Logger,
                tick.ContractId,
                positionId.Format(),
                route.TradeLegId,
                result.ErrorCode,
                result.ErrorMessage ?? string.Empty);
    }

    static void LogIgnoredTick(
        IFuturesOptionRealtimeContext context,
        string contractId,
        MarketRouteLookupOutcome outcome)
    {
        if (!context.RouteIndex.ShouldLog(contractId, outcome))
            return;

        if (outcome == MarketRouteLookupOutcome.UnknownInstrument)
            FuturesTickTradeDataChangedLogging.UnknownContract(context.Logger, contractId);
        else if (outcome == MarketRouteLookupOutcome.NoOpenPosition)
            FuturesTickTradeDataChangedLogging.NoOpenPosition(context.Logger, contractId);
    }

    static Guid TickCommandId(Guid sourceEventId, Guid positionId, Guid legId)
    {
        Span<byte> result = stackalloc byte[16];
        Span<byte> value = stackalloc byte[16];
        sourceEventId.TryWriteBytes(result);
        positionId.TryWriteBytes(value);
        for (var index = 0; index < 16; index++)
            result[index] ^= value[index];

        legId.TryWriteBytes(value);
        for (var index = 0; index < 16; index++)
            result[index] ^= value[index];

        return new Guid(result);
    }
}
