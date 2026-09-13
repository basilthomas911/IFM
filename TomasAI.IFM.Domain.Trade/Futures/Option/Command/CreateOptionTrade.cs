using TomasAI.IFM.Domain.Trade.Futures.Option.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Command;

public static class CreateOptionTrade
{
    /// <summary>
    /// Validates the current aggregate state and creates or idempotently reapplies an option trade.
    /// </summary>
    /// <param name="command">The command containing the established option-trade definition.</param>
    /// <param name="state">The event-sourced option-trade state loaded for the command identity.</param>
    /// <returns>
    /// A successful result after applying an <see cref="OptionTradeChangedEvent"/>, or a failed
    /// result describing the first rejected business rule.
    /// </returns>
    public static ServiceResult<GuidResult> Execute(
        this CreateOptionTradeCommand command,
        FuturesOptionTradeCommandState state) => command switch
        {
            _ when state.Current is { } current &&
                   current.Id == command.Trade.Id &&
                   current.ExecutionAttemptId == command.Trade.ExecutionAttemptId =>
                command.UpdatedOk(() => state.Update(
                    command.CreateOptionTradeChangedEvent(current, false),
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
            _ when command.Trade.AssetFamily != TradeAssetFamily.FuturesOption =>
                command.UpdateFailed(
                    "TRADE.TYPE_MISMATCH;OptionTrade requires FuturesOption asset family."),
            _ when command.Trade.StrategyKind == TradeStrategyKind.FuturesOutright =>
                command.UpdateFailed(
                    "TRADE.TYPE_MISMATCH;OptionTrade cannot use FuturesOutright strategy."),
            _ => command.UpdatedOk(() => state.Update(
                command.CreateOptionTradeChangedEvent(command.Trade, true),
                command))
        };

    /// <summary>
    /// Creates the private event that records the supplied established option-trade state.
    /// </summary>
    /// <param name="command">The originating create command.</param>
    /// <param name="trade">The established trade state to persist.</param>
    /// <param name="isInitialEstablishment">
    /// Indicates whether the event establishes the trade for the first time.
    /// </param>
    /// <returns>The private option-trade state-change event.</returns>
    static OptionTradeChangedEvent CreateOptionTradeChangedEvent(
        this CreateOptionTradeCommand command,
        EstablishedTradeDefinition trade,
        bool isInitialEstablishment) => new()
        {
            EntityId = command.EntityId,
            State = trade,
            IsInitialEstablishment = isInitialEstablishment
        };
}
