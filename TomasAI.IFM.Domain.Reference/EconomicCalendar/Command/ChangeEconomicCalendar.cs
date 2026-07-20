using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Reference.Commands;
using TomasAI.IFM.Shared.Reference.Events;
using TomasAI.IFM.Domain.Reference.EconomicCalendar.Command.Exceptions;
using TomasAI.IFM.Domain.Reference.EconomicCalendar.Command.State;

namespace TomasAI.IFM.Domain.Reference.EconomicCalendar.Command;

public static class ChangeEconomicCalendar
{
    public static string EconomicCalendarDoesNotExist(ChangeEconomicCalendarCommand e) => $"{e.CommandName}: economicCalendar {e.EntityId} does not exist";

    /// <summary>
    /// Executes the ChangeEconomicCalendarCommand against the provided EconomicCalendarCommandState.
    /// </summary>
    /// <param name="e">The change economic calendar command.</param>
    /// <param name="state">The economic calendar command state.</param>
    /// <returns></returns>
    /// <exception cref="ChangeEconomicCalendarException"></exception>
    public static bool Execute(this ChangeEconomicCalendarCommand e, EconomicCalendarCommandState state)
    {
        return e switch
        {
            _ when !state.EconomicCalendarExists(e.EntityId) && !e.Overwrite => throw new ChangeEconomicCalendarException(EconomicCalendarDoesNotExist(e)),
            _ => state.Update(e.CreateEconomicCalendarChangedEvent(), e)
        };
    }
    /// <summary>
    /// Creates an EconomicCalendarChangedEvent from a ChangeEconomicCalendarCommand.
    /// </summary>
    /// <param name="e">The change economic calendar command.</param>
    /// <returns>The economic calendar changed event.</returns>
    internal static EconomicCalendarChangedEvent CreateEconomicCalendarChangedEvent(this ChangeEconomicCalendarCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, EconomicCalendarChangedEvent.Actor, EconomicCalendarChangedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EconomicCalendarId,
            EconomicCalendar = e.EconomicCalendar,
            UpdatedOn = e.OriginatedOn,
            UpdatedBy = e.OriginatedBy
        };
}
