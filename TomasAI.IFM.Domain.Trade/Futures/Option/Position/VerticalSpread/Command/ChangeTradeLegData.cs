using TomasAI.IFM.Domain.Trade.Futures.Position.Command.Model;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.Model;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventSourcing;

using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Shared.EventModelActor;
namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command;

public static class ChangeTradeLegData
{
    /// <summary>Computes and applies one accepted position change.</summary>
    /// <param name="command">The concrete position intent.</param>
    /// <param name="state">The authoritative position state.</param>
    /// <returns>The command acceptance or business failure.</returns>
    public static ServiceResult<GuidResult> Execute(this ChangeTradeLegDataCommand command, VerticalSpreadPositionCommandState state)
    {
        var errorMsg = "VerticalSpreadPosition.STATE.APPLY_FAILED: unable to apply ChangeTradeLegData event";
        var updated = command.Compute(state, out var positionChange) switch
        {
            _ when !positionChange.Accepted => command.UpdateFailed(ref errorMsg, $"{positionChange.RejectionCode};{positionChange.RejectionReason}"),
            _ when positionChange.PositionSnapshot is null => command.UpdateFailed(ref errorMsg, "VerticalSpreadPosition: the computed position snapshot is missing."),
            _ => state.Update(command.CreateVerticalSpreadPositionChangedEvent(positionChange.PositionSnapshot), command)
        };
        return updated ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId)) : command.UpdateFailed(errorMsg);
    }
    /// <summary>Computes the position transition in an isolated calculation workspace.</summary>
    /// <param name="command">The concrete position intent.</param>
    /// <param name="state">The current position snapshot owner.</param>
    /// <param name="positionChange">The proposed position snapshot or rejection.</param>
    /// <returns>True when the transition is accepted.</returns>
    internal static bool Compute(this ChangeTradeLegDataCommand command, VerticalSpreadPositionCommandState state, out PositionChange positionChange)
    {
        var positionDecision = VerticalSpreadPositionTransition.Compute(state.PositionSnapshot, machine => machine.UpdateLeg(
                command.TradeLegId,
                command.ContractId,
                command.Price,
                command.SourceSequence,
                command.EffectiveAtUtc,
                command.RouteGeneration));
        positionChange = new(positionDecision.Accepted, positionDecision.Value, positionDecision.Code, positionDecision.Detail);
        return positionChange.Accepted;
    }
    /// <summary>Creates the source event with the originating command ID.</summary>
    /// <param name="command">The originating command.</param>
    /// <param name="positionSnapshot">The accepted position snapshot.</param>
    /// <returns>The event ready for state application.</returns>
    internal static VerticalSpreadPositionChangedEvent CreateVerticalSpreadPositionChangedEvent(this ChangeTradeLegDataCommand command, StrategyPositionSnapshot positionSnapshot)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new(ActorType.Event, "FuturesVerticalSpreadTradePositionEvent", VerticalSpreadPositionChangedEvent.Verb, command.EntityId.Format()),
            ReceivedOn = DateTime.UtcNow,
            EntityId = command.EntityId,
            PositionSnapshot = positionSnapshot
        };
}
