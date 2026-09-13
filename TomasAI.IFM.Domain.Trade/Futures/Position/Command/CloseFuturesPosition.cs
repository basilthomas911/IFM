using TomasAI.IFM.Domain.Trade.Futures.Position.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Command;

public static class CloseFuturesPosition
{
    /// <summary>
    /// Closes an open futures position and fixes its realized profit or loss.
    /// </summary>
    /// <param name="command">The command containing the close UTC time.</param>
    /// <param name="state">The event-sourced futures-position state.</param>
    /// <returns>A successful result with a pending state-change event, or the rejected transition.</returns>
    public static ServiceResult<GuidResult> Execute(
        this CloseFuturesPositionCommand command,
        FuturesPositionCommandState state)
    {
        var stateMachine = CreateStateMachine(state);
        var decision = stateMachine.Close(command.EffectiveAtUtc);

        if (!decision.Accepted || decision.Value is null)
            return TradeCommandResult.Rejected(command.ErrorCode, decision);

        return command.UpdatedOk(() => state.Update(
            command.CreateFuturesPositionChangedEvent(decision.Value),
            command));
    }

    /// <summary>Rehydrates the position calculation model from the current aggregate state.</summary>
    /// <param name="state">The event-sourced futures-position state.</param>
    /// <returns>A calculation model ready to evaluate the close transition.</returns>
    static StrategyPositionActorStateMachine CreateStateMachine(
        FuturesPositionCommandState state)
    {
        var stateMachine = new StrategyPositionActorStateMachine();
        if (state.Current is { } current)
            stateMachine.Replay(current);
        return stateMachine;
    }

    /// <summary>Creates the private event containing the accepted position snapshot.</summary>
    /// <param name="command">The originating close command.</param>
    /// <param name="snapshot">The accepted position snapshot.</param>
    /// <returns>The private futures-position state-change event.</returns>
    static FuturesPositionChangedEvent CreateFuturesPositionChangedEvent(
        this CloseFuturesPositionCommand command,
        StrategyPositionSnapshot snapshot) => new()
        {
            EntityId = command.EntityId,
            State = snapshot
        };
}
