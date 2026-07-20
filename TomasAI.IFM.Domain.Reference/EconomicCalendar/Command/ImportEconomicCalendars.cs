using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.Commands;
using TomasAI.IFM.Shared.Reference.Events;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Domain.Reference.EconomicCalendar.Command.Exceptions;
using TomasAI.IFM.Domain.Reference.EconomicCalendar.Command.State;

namespace TomasAI.IFM.Domain.Reference.EconomicCalendar.Command;

public static class ImportEconomicCalendars
{
    /// <summary>
    /// Executes the ImportEconomicCalendarsCommand by checking if each economic calendar already exists in the state.
    /// If it does, an AddEconomicCalendarException is thrown. Otherwise, an EconomicCalendarAddedEvent is created and the state is updated.
    /// </summary>
    /// <param name="e">The import economic calendars command.</param>
    /// <param name="state">The economic calendar command state.</param>
    /// <returns>true if all economic calendars are processed successfully; otherwise, false.</returns>
    /// <exception cref="AddEconomicCalendarException">Thrown if an economic calendar with the same entity identifier already exists in the state.</exception>
    public static bool Execute(this ImportEconomicCalendarsCommand e, EconomicCalendarCommandState state)
    {
        foreach (var economicCalendar in e.EconomicCalendars)
        {
            _ = economicCalendar switch
            {
                _ when state.EconomicCalendarExists(e.EntityId) => throw new AddEconomicCalendarException(e.EconomicCalendarAlreadyExistsErrorMsg()),
                _ => state.Update(e.CreateEconomicCalendarAddedEvent(economicCalendar), e)
            };
        }
        return true;
    }

    /// <summary>
    /// Creates an EconomicCalendarAddedEvent from the ImportEconomicCalendarsCommand and the EconomicCalendarReadModel.
    /// </summary>
    /// <param name="e">The import economic calendars command.</param>
    /// <param name="economicCalendar">The economic calendar read model.</param>
    /// <returns>The created economic calendar added event.</returns>
    internal static EconomicCalendarAddedEvent CreateEconomicCalendarAddedEvent(this ImportEconomicCalendarsCommand e, EconomicCalendarReadModel economicCalendar)
       => new()
       {
           CommandId = e.CommandId,
           Subject = new ActorSubject(ActorType.Event, EconomicCalendarAddedEvent.Actor, EconomicCalendarAddedEvent.Verb, e.EntityId.Format()),
           EntityId = e.EntityId,
           EconomicCalendar = economicCalendar,
           CreatedOn = e.OriginatedOn,
           CreatedBy = e.OriginatedBy
       };

    public static string EconomicCalendarAlreadyExistsErrorMsg(this ICommand e) => e switch
    {
        AddEconomicCalendarCommand cmd => $"{cmd.CommandName}: economicCalendar {cmd.EntityId} already exists",
        ImportEconomicCalendarsCommand => $"{e.CommandName}: one or more economicCalendars already exist",
        _ => throw new NotSupportedException($"{e.CommandName}: unsupported command for existence check")
    };


}
