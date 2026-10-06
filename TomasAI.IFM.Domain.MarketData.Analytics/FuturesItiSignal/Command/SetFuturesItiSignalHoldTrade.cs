using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command;

/// <summary>Owns the concrete command handler and event factories for this analytics operation.</summary>
public static class SetFuturesItiSignalHoldTrade
{
    /// <summary>Computes and validates the Futures Iti Signal command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this SetFuturesItiSignalHoldTradeCommand command, FuturesItiSignalCommandState state)
    {
        var errorMsg = "unable to set ITI hold-trade state";
        var updated = command.Compute(state.FuturesItiSignal, out var futuresItiSignal) switch
        {
            _ when !state.Exists(command.EntityId)
                => command.UpdateFailed(ref errorMsg, "ITI signal does not exist for this identity"),
            _ when !state.IsTradeInReadyState
                => command.UpdateFailed(ref errorMsg, "ITI trade state does not permit this hold transition"),
            _ when futuresItiSignal is null
                => command.UpdateFailed(ref errorMsg, "computed ITI hold-trade signal is missing"),
            _ => state.Update(command.CreateSetHoldTradeEvent(futuresItiSignal), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the proposed Futures Iti Signal result from the supplied business inputs without mutating actor state or pending events.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="currentFuturesItiSignal">The previously accepted current futures iti signal used only as computation input.</param>
    /// <param name="futuresItiSignal">The futures iti signal business data used by this operation.</param>
    /// <returns>True when the proposed business result advances or is accepted; otherwise, false. The output retains the no-change or rejection decision.</returns>
    /// <remarks>Accumulator validation exceptions propagate to the command actor exception boundary.</remarks>
    internal static bool Compute(this SetFuturesItiSignalHoldTradeCommand command, FuturesItiSignalV2ReadModel? currentFuturesItiSignal,
        out FuturesItiSignalV2ReadModel? futuresItiSignal)
    {
        futuresItiSignal = currentFuturesItiSignal is null ? null : currentFuturesItiSignal with
        {
            ContractId = command.ContractId,
            ValueDate = command.ValueDate,
            SequenceId = 0,
            IntrinsicTime = command.Timestamp,
            IntrinsicTimeLength = 0,
            IntrinsicTimeMode = IntrinsicTimeModeType.HoldTradeChanged,
            TradeState = IntrinsicTimeTradeState.Hold
        };
        return futuresItiSignal is not null;
    }

    /// <summary>Creates the Futures Iti Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresItiSignal">The futures iti signal business data used by this operation.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesItiSignalHoldTradeSetEvent CreateSetHoldTradeEvent(this SetFuturesItiSignalHoldTradeCommand command,
        FuturesItiSignalV2ReadModel futuresItiSignal) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, FuturesItiSignalHoldTradeSetEvent.Actor, FuturesItiSignalHoldTradeSetEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        FuturesItiSignal = futuresItiSignal,
        CreatedOn = command.OriginatedOn,
        CreatedBy = command.OriginatedBy
    };
}
