using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.State;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command;

public static class ImportEconomicCalendars
{
    /// <summary>
    /// Validates and normalizes the complete import before mutating state, then records one batch event.
    /// </summary>
    /// <param name="e">The import economic calendars command.</param>
    /// <param name="state">The economic calendar command state.</param>
    /// <returns>true if all economic calendars are processed successfully; otherwise, false.</returns>
    /// <exception cref="AddEconomicCalendarException">Thrown if an economic calendar with the same entity identifier already exists in the state.</exception>
    public static bool Execute(this ImportEconomicCalendarsCommand e, EconomicCalendarCommandState state)
    {
        return state.Update(e.CreateEconomicCalendarsImportedEvent(), e);
    }

    /// <summary>
    /// Creates the parameter-only request event used by the import workflow.
    /// </summary>
    /// <param name="e">The import economic calendars command.</param>
    /// <returns>The created batch import event.</returns>
    internal static EconomicCalendarsImportedEvent CreateEconomicCalendarsImportedEvent(
        this ImportEconomicCalendarsCommand e)
       => new()
       {
           CommandId = e.CommandId,
           Subject = new ActorSubject(ActorType.Event, EconomicCalendarsImportedEvent.Actor, EconomicCalendarsImportedEvent.Verb, e.EntityId.Format()),
           EntityId = e.EntityId,
           ImportedDate = e.ImportedDate,
           CountryCodes = e.CountryCodes,
           RequestedOn = e.OriginatedOn,
           RequestedBy = e.OriginatedBy,
           DuplicatePolicy = e.DuplicatePolicy
       };

    public static string EconomicCalendarAlreadyExistsErrorMsg(this ICommand e) => e switch
    {
        AddEconomicCalendarCommand cmd => $"{cmd.CommandName}: economicCalendar {cmd.EntityId} already exists",
        ImportEconomicCalendarsCommand => $"{e.CommandName}: one or more economicCalendars already exist",
        _ => throw new NotSupportedException($"{e.CommandName}: unsupported command for existence check")
    };


}
