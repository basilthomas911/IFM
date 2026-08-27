using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.State;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command;

public static class RemoveEconomicCalendar
{
    /// <summary>
    /// Executes the RemoveEconomicCalendarCommand against the provided EconomicCalendarCommandState.
    /// </summary>
    /// <param name="e">The remove economic calendar command.</param>
    /// <param name="state">The economic calendar command state.</param>
    /// <returns>true if the economic calendar was successfully removed; otherwise, false.</returns>
    /// <exception cref="RemoveEconomicCalendarException">Thrown if the economic calendar does not exist in the state.</exception>
    public static ServiceResult<GuidResult> Execute(this RemoveEconomicCalendarCommand e, EconomicCalendarCommandState state)
    {
        return e switch
        {
            _ when !state.EconomicCalendarExists(e.EntityId) && !e.Overwrite => throw new RemoveEconomicCalendarException(EconomicCalendarDoesNotExist(e)),
            _ => e.UpdateResult(() => state.Update(e.CreateEconomicCalendarRemovedEvent(), e))
        };
        static string EconomicCalendarDoesNotExist(RemoveEconomicCalendarCommand e) => $"{e.CommandName}: economicCalendar {e.EntityId} does not exist";
    }

    /// <summary>
    /// Creates an EconomicCalendarRemovedEvent from a RemoveEconomicCalendarCommand.
    /// </summary>
    /// <param name="e">The remove economic calendar command.</param>
    /// <returns>The created economic calendar removed event.</returns>
    internal static EconomicCalendarRemovedEvent CreateEconomicCalendarRemovedEvent(this RemoveEconomicCalendarCommand e)
       => new()
       {
           CommandId = e.CommandId,
           Subject = new ActorSubject(ActorType.Event, EconomicCalendarRemovedEvent.Actor, EconomicCalendarRemovedEvent.Verb, e.EntityId.Format()),
           EntityId = e.EconomicCalendarId,
           RemovedOn = e.OriginatedOn,
           RemovedBy = e.OriginatedBy
       };
}
