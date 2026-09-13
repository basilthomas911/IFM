using TomasAI.IFM.Domain.Trade.Futures.Option.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Command;

public static class BeginCloseOptionTrade
{
    /// <summary>
    /// Validates the current option-trade status and begins its close transition.
    /// </summary>
    /// <param name="command">The command requesting that the option trade begin closing.</param>
    /// <param name="state">The event-sourced option-trade state loaded for the command identity.</param>
    /// <returns>
    /// A successful result after applying an <see cref="OptionTradeChangedEvent"/>, or a failed
    /// result when the trade is missing or cannot enter the closing state.
    /// </returns>
    public static ServiceResult<GuidResult> Execute(
        this BeginCloseOptionTradeCommand command,
        FuturesOptionTradeCommandState state) => command switch
        {
            _ when state.Current is null =>
                command.UpdateFailed("TRADE.NOT_FOUND;Trade does not exist."),
            _ when state.Current.Status is EstablishedTradeStatus.Closing or
                                           EstablishedTradeStatus.Closed =>
                command.UpdateFailed(
                    $"TRADE.INVALID_TRANSITION;Cannot begin close from {state.Current.Status}."),
            _ => command.UpdatedOk(() => state.Update(
                command.CreateOptionTradeChangedEvent(state.Current!),
                command))
        };

    /// <summary>
    /// Creates the private event that changes the established option trade to closing status.
    /// </summary>
    /// <param name="command">The originating begin-close command.</param>
    /// <param name="current">The current established option-trade state.</param>
    /// <returns>The private option-trade state-change event.</returns>
    static OptionTradeChangedEvent CreateOptionTradeChangedEvent(
        this BeginCloseOptionTradeCommand command,
        EstablishedTradeDefinition current) => new()
        {
            EntityId = command.EntityId,
            State = current with { Status = EstablishedTradeStatus.Closing },
            IsInitialEstablishment = false
        };
}
