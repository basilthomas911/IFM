using TomasAI.IFM.Domain.Trade.Futures.Position.Command.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Command;

public static class ChangeTradeLegData
{
    /// <summary>
    /// Applies routed contract and market-price data to one futures-position leg.
    /// </summary>
    /// <param name="command">The routed leg-data command.</param>
    /// <param name="state">The resident event-sourced futures-position state.</param>
    /// <returns>A successful result with a pending state-change event, or the rejected transition.</returns>
    public static ServiceResult<GuidResult> Execute(
        this ChangeTradeLegDataCommand command,
        FuturesPositionCommandState state)
    {
        var errorMsg = "FuturesPosition.STATE.APPLY_FAILED: unable to apply ChangeTradeLegData event";
        var updated = command.Compute(state, out var positionChange) switch
        {
            _ when !positionChange.Accepted => command.UpdateFailed(ref errorMsg, $"{positionChange.RejectionCode};{positionChange.RejectionReason}"),
            _ when positionChange.PositionSnapshot is null => command.UpdateFailed(ref errorMsg, "FuturesPosition: the computed position snapshot is missing."),
            _ => state.Update(command.CreateFuturesPositionChangedEvent(positionChange.PositionSnapshot), command)
        };
        return updated ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId)) : command.UpdateFailed(errorMsg);
    }

    /// <summary>Rehydrates the position calculation model from the current aggregate state.</summary>
    /// <param name="state">The resident event-sourced futures-position state.</param>
    /// <returns>A calculation model ready to evaluate the routed leg update.</returns>
    static StrategyPositionActorStateMachine CreateStateMachine(
        FuturesPositionCommandState state)
    {
        var stateMachine = new StrategyPositionActorStateMachine();
        if (state.PositionSnapshot is { } current)
            stateMachine.Replay(current);
        return stateMachine;
    }

    /// <summary>Creates the private event containing the accepted position snapshot.</summary>
    /// <param name="command">The originating routed leg-data command.</param>
    /// <param name="snapshot">The accepted position snapshot.</param>
    /// <returns>The private futures-position state-change event.</returns>
    static FuturesPositionChangedEvent CreateFuturesPositionChangedEvent(
        this ChangeTradeLegDataCommand command,
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
    internal static bool Compute(this ChangeTradeLegDataCommand command, FuturesPositionCommandState state, out PositionChange positionChange)
    {
        var stateMachine = CreateStateMachine(state);
        var positionDecision = stateMachine.UpdateLeg(
            command.TradeLegId,
            command.ContractId,
            command.Price,
            command.SourceSequence,
            command.EffectiveAtUtc,
            command.RouteGeneration);

        positionChange = new(positionDecision.Accepted, positionDecision.Value, positionDecision.Code, positionDecision.Detail);
        return positionChange.Accepted;
    }
}
