using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.State;
using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;

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
        var normalized = new Dictionary<EconomicCalendarId, EconomicCalendarReadModel>();
        foreach (var economicCalendar in e.EconomicCalendars)
        {
            var id = economicCalendar.Id;
            if (normalized.TryGetValue(id, out var existing))
            {
                if (existing != economicCalendar)
                {
                    throw new MarketDataImportDuplicateException(
                        $"Conflicting economic-calendar entries were supplied for {id}.");
                }

                continue;
            }

            if (e.DuplicatePolicy == ImportDuplicatePolicy.Reject
                && state.EconomicCalendarExists(id))
            {
                throw new MarketDataImportDuplicateException(
                    $"Economic calendar {id} already exists.");
            }

            normalized.Add(id, economicCalendar);
        }

        return state.Update(
            e.CreateEconomicCalendarsImportedEvent(
                [.. normalized.Values
                    .OrderBy(calendar => calendar.EventDate)
                    .ThenBy(calendar => calendar.CountryCode, StringComparer.Ordinal)
                    .ThenBy(calendar => calendar.EventName, StringComparer.Ordinal)]),
            e);
    }

    /// <summary>
    /// Creates the normalized batch event used by import streams.
    /// </summary>
    /// <param name="e">The import economic calendars command.</param>
    /// <param name="economicCalendars">The normalized imported rows.</param>
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
           ImportedBy = e.OriginatedBy,
           DuplicatePolicy = e.DuplicatePolicy
       };

    public static string EconomicCalendarAlreadyExistsErrorMsg(this ICommand e) => e switch
    {
        AddEconomicCalendarCommand cmd => $"{cmd.CommandName}: economicCalendar {cmd.EntityId} already exists",
        ImportEconomicCalendarsCommand => $"{e.CommandName}: one or more economicCalendars already exist",
        _ => throw new NotSupportedException($"{e.CommandName}: unsupported command for existence check")
    };


}
