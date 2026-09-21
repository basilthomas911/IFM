using TomasAI.IFM.Domain.Trade.Futures.Option.Command.State;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Command;

public static class CloseOptionTrade
{
    /// <summary>
    /// Validates the current option-trade status and completes its close transition.
    /// </summary>
    /// <param name="command">The command requesting final closure of the option trade.</param>
    /// <param name="state">The event-sourced option-trade state loaded for the command identity.</param>
    /// <returns>
    /// A successful result after applying an <see cref="OptionTradeChangedEvent"/>, or a failed
    /// result when the trade is missing or is not currently closing.
    /// </returns>
    public static ServiceResult<GuidResult> Execute(
        this CloseOptionTradeCommand command,
        FuturesOptionTradeCommandState state) => command switch
        {
            _ when state.Current is null =>
                command.UpdateFailed("TRADE.NOT_FOUND;Trade does not exist."),
            _ when state.Current.Status != EstablishedTradeStatus.Closing =>
                command.UpdateFailed(
                    $"TRADE.INVALID_TRANSITION;Cannot close from {state.Current.Status}."),
            _ when !TradeCloseEvidence.TryApply(state.Current, command.ClosingFills, command.ClosedAtUtc, out _) =>
                command.UpdateFailed(
                    "TRADE.INVALID_CLOSE_EVIDENCE;Closing fills and a UTC close time are required."),
            _ => command.UpdatedOk(() => state.Update(
                command.CreateOptionTradeChangedEvent(state.Current!),
                command))
        };

    /// <summary>
    /// Creates the private event that changes the established option trade to closed status.
    /// </summary>
    /// <param name="command">The originating close command.</param>
    /// <param name="current">The current established option-trade state.</param>
    /// <returns>The private option-trade state-change event.</returns>
    static OptionTradeChangedEvent CreateOptionTradeChangedEvent(
        this CloseOptionTradeCommand command,
        EstablishedTradeDefinition current) => new()
        {
            EntityId = command.EntityId,
            State = ApplyClosingEvidence(current, command),
            IsInitialEstablishment = false
        };

    static EstablishedTradeDefinition ApplyClosingEvidence(EstablishedTradeDefinition current, CloseOptionTradeCommand command)
    {
        if (!TradeCloseEvidence.TryApply(current, command.ClosingFills, command.ClosedAtUtc, out var updated))
            throw new InvalidOperationException("Validated closing evidence changed.");
        return updated;
    }
}
