using TomasAI.IFM.Domain.Trade.Futures.Position.Command.Model;
using TomasAI.IFM.Shared.EventModelActor;
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
        var errorMsg = "FuturesPosition.STATE.APPLY_FAILED: unable to apply OpenFuturesPosition event";
        var updated = command.Compute(state, out var positionChange) switch
        {
            _ when !positionChange.Accepted => command.UpdateFailed(ref errorMsg, $"{positionChange.RejectionCode};{positionChange.RejectionReason}"),
            _ when positionChange.PositionSnapshot is null => command.UpdateFailed(ref errorMsg, "FuturesPosition: the computed position snapshot is missing."),
            _ => state.Update(command.CreateFuturesPositionChangedEvent(positionChange.PositionSnapshot), command)
        };
        return updated ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId)) : command.UpdateFailed(errorMsg);
    }

    /// <summary>Rehydrates the position calculation model from the current aggregate state.</summary>
    /// <param name="state">The event-sourced futures-position state.</param>
    /// <returns>A calculation model ready to evaluate the open command.</returns>
    static StrategyPositionActorStateMachine CreateStateMachine(
        FuturesPositionCommandState state)
    {
        var stateMachine = new StrategyPositionActorStateMachine();
        if (state.PositionSnapshot is { } current)
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
            CommandId = command.CommandId,
            Subject = new(ActorType.Event, "FuturesTradePositionEvent", FuturesPositionChangedEvent.Verb, command.EntityId.Format()),
            ReceivedOn = DateTime.UtcNow,
            EntityId = command.EntityId,
            PositionSnapshot = snapshot
        };
    /// <summary>Computes a position change in an isolated calculation workspace.</summary>
    /// <param name="command">The concrete position intent.</param>
    /// <param name="state">The authoritative position state, read without mutation.</param>
    /// <param name="positionChange">The proposed position snapshot or business rejection.</param>
    /// <returns>True when the position change is accepted.</returns>
    internal static bool Compute(this OpenFuturesPositionCommand command, FuturesPositionCommandState state, out PositionChange positionChange)
    {
        var stateMachine = CreateStateMachine(state);
        var positionDecision = stateMachine.Open(
            command.Trade,
            command.EntityId.PositionId,
            command.EffectiveAtUtc);

        positionChange = new(positionDecision.Accepted, positionDecision.Value, positionDecision.Code, positionDecision.Detail);
        return positionChange.Accepted;
    }
}
