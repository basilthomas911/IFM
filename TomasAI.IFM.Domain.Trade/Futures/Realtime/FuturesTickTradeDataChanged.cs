using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Realtime;

/// <summary>Routes one Futures trade tick to every matching open outright position leg.</summary>
public static partial class FuturesTickTradeDataChanged
{
    /// <summary>Maps the market tick and sends one position update command for every eligible route.</summary>
    public static async ValueTask ExecuteAsync(
        this FuturesTickTradeDataChangedEvent changed,
        IFuturesRealtimeContext context)
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
            if (route.TradeType != TradeStrategyKind.FuturesOutright)
                continue;

            var positionId = new StrategyPositionId(
                new TradeEntityId(
                    route.PortfolioId,
                    route.FundId,
                    route.OrderId,
                    route.TradeId),
                route.StrategyPositionId);
            var command = new ChangeTradeLegDataCommand
            {
                CommandId = TickCommandId(changed.Id, route.StrategyPositionId, route.TradeLegId),
                Subject = new ActorSubject(
                    ActorType.Command,
                    FuturesTradePositionCommandActor.ActorName,
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
                RouteSendFailed(
                    context.Logger,
                    tick.ContractId,
                    positionId.Format(),
                    route.TradeLegId,
                    result.ErrorCode,
                    result.ErrorMessage ?? string.Empty);
        }
    }

    /// <summary>Logs an expected unroutable tick subject to the route index rate limit.</summary>
    static void LogIgnoredTick(
        IFuturesRealtimeContext context,
        string contractId,
        MarketRouteLookupOutcome outcome)
    {
        if (!context.RouteIndex.ShouldLog(contractId, outcome))
            return;

        if (outcome == MarketRouteLookupOutcome.UnknownInstrument)
            UnknownContract(context.Logger, contractId);
        else if (outcome == MarketRouteLookupOutcome.NoOpenPosition)
            NoOpenPosition(context.Logger, contractId);
    }

    /// <summary>Derives the idempotent position command identifier from the source event and route identity.</summary>
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

    [LoggerMessage(
        LogLevel.Information,
        "Ignoring Futures tick for ContractId {ContractId}; no route is registered.")]
    static partial void UnknownContract(ILogger logger, string contractId);

    [LoggerMessage(
        LogLevel.Information,
        "Ignoring Futures tick because ContractId {ContractId} has no open position.")]
    static partial void NoOpenPosition(ILogger logger, string contractId);

    [LoggerMessage(
        LogLevel.Error,
        "Failed to route Futures tick for ContractId {ContractId} to position {PositionId}, leg {TradeLegId}. Error {ErrorCode}: {ErrorMessage}")]
    static partial void RouteSendFailed(
        ILogger logger,
        string contractId,
        string positionId,
        Guid tradeLegId,
        int errorCode,
        string errorMessage);
}
