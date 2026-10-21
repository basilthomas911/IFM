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
    public static async ValueTask ExecuteAsync(this FuturesTickTradeDataChangedEvent changed, IFuturesOptionRealtimeContext context)
    {
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(context);
        // Quotes and pricing completions both reach the position owner; completions do not create another price mark.
        if (changed.OptionMarketPriceObservation?.OptionTickData is { } data)
        {
            if (!StrategyPositionLegOptionData.IsValidQuote(data, data.ContractId)) return;
            foreach (var snapshot in context.RealtimeCache?.Apply(data) ?? [])
                await context.SendAsync<TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshotChangedEvent, TradeEntityId>(
                    TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshotChangedEvent.From(snapshot)).ConfigureAwait(false);
        }
        var receivedAtUtc = changed.ReceivedOn.Kind == DateTimeKind.Utc ? changed.ReceivedOn : DateTime.SpecifyKind(changed.ReceivedOn, DateTimeKind.Utc);
        var observation = changed.OptionMarketPriceObservation;
        var tick = new PositionMarketTick(observation?.OptionTickData.ContractId ?? changed.TickDataId.ContractId, observation is null ? changed.TradeData.Price : (decimal)observation.OptionTickData.OptionPrice, observation?.SourceSequence ?? changed.TradeData.SourceSequence, receivedAtUtc);
        PortfolioFundTradeLeg[] routes;
        // Option ingress already validates observations. Route every arrival, including repeated or reset sequences.
        var outcome = context.RouteIndex.TryRoute(in tick, out routes, enforceSequenceOrdering: false);
        if (outcome != MarketRouteLookupOutcome.Routed)
        {
            LogIgnoredTick(context, tick.ContractId, outcome);
            return;
        }

        foreach (var route in routes)
        {
            if (!FuturesOptionRoutePolicy.IsSupported(route))
                continue;
            var positionId = new StrategyPositionId(new TradeEntityId(route.PortfolioId, route.FundId, route.OrderId, route.TradeId), route.StrategyPositionId);
            await SendTradeLegUpdateAsync(changed, tick, route, positionId, context).ConfigureAwait(false);
        }
    }

    static async ValueTask SendTradeLegUpdateAsync(FuturesTickTradeDataChangedEvent changed, PositionMarketTick tick, PortfolioFundTradeLeg route, StrategyPositionId positionId, IFuturesOptionRealtimeContext context)
    {
        var actorName = route.TradeType == TradeStrategyKind.IronCondor ? FuturesIronCondorTradePositionCommandActor.ActorName : FuturesVerticalSpreadTradePositionCommandActor.ActorName;
        var command = new ChangeTradeLegDataCommand
        {
            CommandId = TickCommandId(changed.OptionMarketPriceObservation?.OptionTickData is { } observation
                ? TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.TradePlanContractIdentity.DeterministicId($"{changed.Id:N}|{observation.SourceGeneration:N}|{observation.ObservationRevision}|{observation.IsPricingUpdate}")
                : changed.Id, route.StrategyPositionId, route.TradeLegId),
            Subject = new ActorSubject(ActorType.Command, actorName, ChangeTradeLegDataCommand.Verb, positionId.Format()),
            EntityId = positionId,
            TradeLegId = route.TradeLegId,
            ContractId = tick.ContractId,
            Price = tick.Price,
            SourceSequence = tick.SourceSequence,
            RouteGeneration = route.Generation,
            EffectiveAtUtc = tick.OccurredAtUtc,
            OptionTick = changed.OptionMarketPriceObservation?.OptionTickData,
            TradeType = route.TradeType
        };
        var result = await context.ActorService.SendAsync<ChangeTradeLegDataCommand, StrategyPositionId>(command, positionId).ConfigureAwait(false);
        context.RoutingProgress?.Record(tick.ContractId, result.Success, DateTime.UtcNow);
        if (!result.Success)
            FuturesTickTradeDataChangedLogging.RouteSendFailed(context.Logger, tick.ContractId, positionId.Format(), route.TradeLegId, result.ErrorCode, result.ErrorMessage ?? string.Empty);
    }

    static void LogIgnoredTick(IFuturesOptionRealtimeContext context, string contractId, MarketRouteLookupOutcome outcome)
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
