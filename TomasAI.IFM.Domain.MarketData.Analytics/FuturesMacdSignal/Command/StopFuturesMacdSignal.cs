using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command;

/// <summary>Handles one stop transition for the futures MACD signal.</summary>
public static class StopFuturesMacdSignal
{
    /// <summary>Computes and validates the Futures MACD Signal command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this StopFuturesMacdSignalCommand command, FuturesMacdSignalCommandState state)
    {
        var updated = state.Update(command.CreateFuturesMacdSignalStoppedEvent(), command);
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: unable to apply MACD lifecycle stop event");
    }

    /// <summary>Creates the Futures MACD Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesMacdSignalStoppedEvent CreateFuturesMacdSignalStoppedEvent(
        this StopFuturesMacdSignalCommand command) => new()
    {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesMacdSignalStoppedEvent.Actor,
                FuturesMacdSignalStoppedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            StoppedOn = command.OriginatedOn,
            StoppedBy = command.OriginatedBy
        };
}
