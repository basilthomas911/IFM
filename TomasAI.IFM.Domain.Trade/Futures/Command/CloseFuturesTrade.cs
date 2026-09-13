using TomasAI.IFM.Domain.Trade.Futures.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Command;

public static class CloseFuturesTrade
{
    /// <summary>Validates the current futures-trade status and completes its close transition.</summary>
    /// <param name="command">The command requesting final closure of the futures trade.</param>
    /// <param name="state">The event-sourced futures-trade state loaded for the command identity.</param>
    /// <returns>A successful result with a state-change event, or a rejected business rule.</returns>
    public static ServiceResult<GuidResult> Execute(
        this CloseFuturesTradeCommand command,
        FuturesTradeCommandState state) => command switch
        {
            _ when state.Current is null =>
                command.UpdateFailed("TRADE.NOT_FOUND;Trade does not exist."),
            _ when state.Current.Status != EstablishedTradeStatus.Closing =>
                command.UpdateFailed(
                    $"TRADE.INVALID_TRANSITION;Cannot close from {state.Current.Status}."),
            _ => command.UpdatedOk(() => state.Update(
                command.CreateFuturesTradeChangedEvent(state.Current!),
                command))
        };

    /// <summary>Creates the private event that changes the futures trade to closed status.</summary>
    /// <param name="command">The originating close command.</param>
    /// <param name="current">The current established futures-trade state.</param>
    /// <returns>The private futures-trade state-change event.</returns>
    static FuturesTradeChangedEvent CreateFuturesTradeChangedEvent(
        this CloseFuturesTradeCommand command,
        EstablishedTradeDefinition current) => new()
        {
            EntityId = command.EntityId,
            State = current with { Status = EstablishedTradeStatus.Closed },
            IsInitialEstablishment = false
        };
}
