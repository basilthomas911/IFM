using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.State;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command;

public static class ImportEconomicCalendars
{
    /// <summary>
    /// Validates the complete import before mutating state, then records one cumulative batch snapshot event.
    /// </summary>
    /// <param name="e">The import economic calendars command.</param>
    /// <param name="state">The economic calendar command state.</param>
    /// <returns>true if all economic calendars are processed successfully; otherwise, false.</returns>
    /// <exception cref="AddEconomicCalendarException">Thrown if an economic calendar with the same entity identifier already exists in the state.</exception>
    public static bool Execute(this ImportEconomicCalendarsCommand e, EconomicCalendarCommandState state)
    {
        var importedIds = new HashSet<EconomicCalendarId>();
        foreach (var economicCalendar in e.EconomicCalendars)
        {
            var id = economicCalendar.Id;
            if (!importedIds.Add(id) || state.EconomicCalendarExists(id))
                throw new AddEconomicCalendarException(e.EconomicCalendarAlreadyExistsErrorMsg());
        }

        var snapshot = new EconomicCalendarReadModel[state.Count + e.EconomicCalendars.Length];
        state.CopyEconomicCalendarsTo(snapshot);
        e.EconomicCalendars.CopyTo(snapshot, state.Count);
        return state.Update(e.CreateEconomicCalendarsImportedEvent(snapshot), e);
    }

    /// <summary>
    /// Creates the cumulative batch snapshot used by import streams.
    /// </summary>
    /// <param name="e">The import economic calendars command.</param>
    /// <param name="economicCalendars">The complete imported state represented by the snapshot.</param>
    /// <returns>The created batch import event.</returns>
    internal static EconomicCalendarsImportedEvent CreateEconomicCalendarsImportedEvent(
        this ImportEconomicCalendarsCommand e,
        EconomicCalendarReadModel[] economicCalendars)
       => new()
       {
           CommandId = e.CommandId,
           Subject = new ActorSubject(ActorType.Event, EconomicCalendarsImportedEvent.Actor, EconomicCalendarsImportedEvent.Verb, e.EntityId.Format()),
           EntityId = e.EntityId,
           EconomicCalendars = economicCalendars,
           ImportedOn = e.OriginatedOn,
           ImportedBy = e.OriginatedBy
       };

    public static string EconomicCalendarAlreadyExistsErrorMsg(this ICommand e) => e switch
    {
        AddEconomicCalendarCommand cmd => $"{cmd.CommandName}: economicCalendar {cmd.EntityId} already exists",
        ImportEconomicCalendarsCommand => $"{e.CommandName}: one or more economicCalendars already exist",
        _ => throw new NotSupportedException($"{e.CommandName}: unsupported command for existence check")
    };


}
