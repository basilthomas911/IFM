using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command;

/// <summary>Handles one stop transition for the futures ADX signal.</summary>
public static class StopFuturesAdxSignal
{
    /// <summary>Computes and validates the Futures ADX Signal command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this StopFuturesAdxSignalCommand command, FuturesAdxSignalCommandState state)
    {
        var errorMsg = "unable to apply ADX stop event";
        var updated = command.EntityId.PeriodLength switch
        {
            _ when command.EntityId.PeriodLength <= 0
                => command.UpdateFailed(ref errorMsg, "ADX period length must be positive"),
            _ => state.Update(command.CreateFuturesAdxSignalStoppedEvent(), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Creates the Futures ADX Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesAdxSignalStoppedEvent CreateFuturesAdxSignalStoppedEvent(
        this StopFuturesAdxSignalCommand command) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, FuturesAdxSignalStoppedEvent.Actor, FuturesAdxSignalStoppedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        StoppedOn = command.OriginatedOn,
        StoppedBy = command.OriginatedBy
    };

}
