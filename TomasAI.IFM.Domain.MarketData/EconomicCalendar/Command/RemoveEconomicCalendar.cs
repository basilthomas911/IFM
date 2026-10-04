using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command;

/// <summary>Handles RemoveEconomicCalendar through computation, failure guards and state-owned event application.</summary>
public static class RemoveEconomicCalendar
{
    /// <summary>Applies the computed business data only when ownership and lifecycle guards succeed.</summary>
    /// <param name="command">The concrete EconomicCalendar intent.</param>
    /// <param name="state">The authoritative EconomicCalendar state.</param>
    /// <returns>The command ID on success, or the guarded/application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this RemoveEconomicCalendarCommand command, EconomicCalendarCommandState state)
    {
        var errorMsg = "EconomicCalendar.STATE.APPLY_FAILED";
        var computed = command.Compute(out var economicCalendarId);
        var updated = computed switch
        {
            _ when !computed => command.UpdateFailed(ref errorMsg, "EconomicCalendar.COMPUTED_DATA.INVALID"),
            _ when !state.EconomicCalendarExists(command.EntityId) && !command.Overwrite
                => command.UpdateFailed(ref errorMsg, $"{command.CommandName}: economic calendar {command.EntityId} does not exist"),
            _ => state.Update(command.CreateEconomicCalendarRemovedEvent(economicCalendarId), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes business data without modifying actor state or command inputs.</summary>
    /// <param name="command">The proposed EconomicCalendar change.</param>
    /// <param name="economicCalendarId">The computed business data to carry in its event.</param>
    /// <returns>True when the computed data belongs to the commanded entity.</returns>
    internal static bool Compute(this RemoveEconomicCalendarCommand command, out EconomicCalendarId economicCalendarId)
    {
        economicCalendarId = command.EconomicCalendarId;
        return economicCalendarId is not null && economicCalendarId == command.EntityId && economicCalendarId.EventDate != default && !string.IsNullOrWhiteSpace(economicCalendarId.CountryCode) && !string.IsNullOrWhiteSpace(economicCalendarId.EventName);
    }

    /// <summary>Creates the private event from guarded business data without changing state.</summary>
    /// <param name="command">The originating command and route.</param>
    /// <param name="economicCalendarId">The computed, guarded business data.</param>
    /// <returns>The source event applied and persisted through State.Update.</returns>
    internal static EconomicCalendarRemovedEvent CreateEconomicCalendarRemovedEvent(this RemoveEconomicCalendarCommand command, EconomicCalendarId economicCalendarId) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, EconomicCalendarRemovedEvent.Actor, EconomicCalendarRemovedEvent.Verb, command.EntityId.Format()),
        EntityId = economicCalendarId,
        RemovedOn = command.OriginatedOn,
        RemovedBy = command.OriginatedBy
    };
}
