using TomasAI.IFM.Domain.Trade.Futures.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Command;

public static class CreateFuturesTrade
{
    /// <summary>
    /// Validates the current aggregate state and creates or idempotently reapplies a futures trade.
    /// </summary>
    /// <param name="command">The command containing the established futures-trade definition.</param>
    /// <param name="state">The event-sourced futures-trade state loaded for the command identity.</param>
    /// <returns>A successful result with a state-change event, or the first rejected business rule.</returns>
    public static ServiceResult<GuidResult> Execute(
        this CreateFuturesTradeCommand command,
        FuturesTradeCommandState state) => command switch
        {
            _ when state.Current is { } current &&
                   current.Id == command.Trade.Id &&
                   current.ExecutionAttemptId == command.Trade.ExecutionAttemptId =>
                command.UpdatedOk(() => state.Update(
                    command.CreateFuturesTradeChangedEvent(current, false),
                    command)),
            _ when state.Current is not null =>
                command.UpdateFailed(
                    "TRADE.ALREADY_EXISTS;A different established Trade already owns this identity."),
            _ when !command.Trade.Id.IsValid ||
                   command.Trade.SourceComponentId == Guid.Empty ||
                   command.Trade.ExecutionAttemptId == Guid.Empty ||
                   command.Trade.Legs.Length == 0 ||
                   command.Trade.OriginalFills.Length == 0 ||
                   command.Trade.EstablishedAtUtc.Kind != DateTimeKind.Utc =>
                command.UpdateFailed(
                    "TRADE.INVALID;Trade identity, component, execution, UTC time, Legs, and OriginalFills are required."),
            _ when command.Trade.AssetFamily != TradeAssetFamily.Futures =>
                command.UpdateFailed(
                    "TRADE.TYPE_MISMATCH;Futures Trade requires Futures asset family."),
            _ when command.Trade.StrategyKind != TradeStrategyKind.FuturesOutright =>
                command.UpdateFailed(
                    "TRADE.TYPE_MISMATCH;Futures Trade requires FuturesOutright strategy."),
            _ => command.UpdatedOk(() => state.Update(
                command.CreateFuturesTradeChangedEvent(command.Trade, true),
                command))
        };

    /// <summary>Creates the private event containing the accepted futures-trade state.</summary>
    /// <param name="command">The originating create command.</param>
    /// <param name="trade">The established futures-trade state.</param>
    /// <param name="isInitialEstablishment">Whether the event first establishes the trade.</param>
    /// <returns>The private futures-trade state-change event.</returns>
    static FuturesTradeChangedEvent CreateFuturesTradeChangedEvent(
        this CreateFuturesTradeCommand command,
        EstablishedTradeDefinition trade,
        bool isInitialEstablishment) => new()
        {
            EntityId = command.EntityId,
            State = trade,
            IsInitialEstablishment = isInitialEstablishment
        };
}
