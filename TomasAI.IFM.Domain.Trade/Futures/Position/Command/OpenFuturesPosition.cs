using TomasAI.IFM.Domain.Trade.Futures.Position.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Command;

public static class OpenFuturesPosition
{
    /// <summary>
    /// Opens a futures position from an established one-leg futures trade.
    /// </summary>
    /// <param name="command">The command containing the established trade and effective UTC time.</param>
    /// <param name="state">The event-sourced futures-position state.</param>
    /// <returns>A successful result with a pending state-change event, or the rejected transition.</returns>
    public static ServiceResult<GuidResult> Execute(
        this OpenFuturesPositionCommand command,
        FuturesPositionCommandState state)
    {
        var stateMachine = CreateStateMachine(state);
        var decision = stateMachine.Open(
            command.Trade,
            command.EntityId.PositionId,
            command.EffectiveAtUtc);

        if (!decision.Accepted || decision.Value is null)
            return TradeCommandResult.Rejected(command.ErrorCode, decision);

        return command.UpdatedOk(() => state.Update(
            command.CreateFuturesPositionChangedEvent(decision.Value),
            command));
    }

    /// <summary>Rehydrates the position calculation model from the current aggregate state.</summary>
    /// <param name="state">The event-sourced futures-position state.</param>
    /// <returns>A calculation model ready to evaluate the open command.</returns>
    static StrategyPositionActorStateMachine CreateStateMachine(
        FuturesPositionCommandState state)
    {
        var stateMachine = new StrategyPositionActorStateMachine();
        if (state.Current is { } current)
            stateMachine.Replay(current);
        return stateMachine;
    }

    /// <summary>Creates the private event containing the accepted position snapshot.</summary>
    /// <param name="command">The originating open command.</param>
    /// <param name="snapshot">The accepted position snapshot.</param>
    /// <returns>The private futures-position state-change event.</returns>
    static FuturesPositionChangedEvent CreateFuturesPositionChangedEvent(
        this OpenFuturesPositionCommand command,
        StrategyPositionSnapshot snapshot) => new()
        {
            EntityId = command.EntityId,
            State = snapshot
        };
}
