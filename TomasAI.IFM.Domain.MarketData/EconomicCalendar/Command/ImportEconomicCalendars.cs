using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Model;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command;

/// <summary>Records a guarded import request; the projector performs downloading and storage policy enforcement.</summary>
public static class ImportEconomicCalendars
{
    /// <summary>Computes request parameters and records one operation marker without rebuilding external records.</summary>
    /// <param name="command">The import intent and duplicate policy.</param>
    /// <param name="state">The authoritative command state.</param>
    /// <returns>The command ID on success, or the request/application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this ImportEconomicCalendarsCommand command, EconomicCalendarCommandState state)
    {
        var errorMsg = "EconomicCalendar.IMPORT.STATE.APPLY_FAILED";
        var computed = command.Compute(out var economicCalendarImport);
        var updated = computed switch
        {
            _ when !computed => command.UpdateFailed(ref errorMsg, "EconomicCalendar.IMPORT.REQUEST.INVALID"),
            _ => state.Update(command.CreateEconomicCalendarsImportedEvent(economicCalendarImport), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes immutable request parameters without downloading data or changing state.</summary>
    /// <param name="command">The import intent.</param>
    /// <param name="economicCalendarImport">The computed request parameters.</param>
    /// <returns>True when required request parameters and ownership are valid.</returns>
    internal static bool Compute(this ImportEconomicCalendarsCommand command, out EconomicCalendarImport economicCalendarImport)
    {
        economicCalendarImport = new(command.ImportedDate, command.DuplicatePolicy, command.CountryCodes is null ? [] : [.. command.CountryCodes]);
        return command.EntityId is not null && command.EntityId.EventDate == economicCalendarImport.ImportedDate && economicCalendarImport.ImportedDate != default && Enum.IsDefined(economicCalendarImport.DuplicatePolicy) && command.CountryCodes is not null && economicCalendarImport.CountryCodes.All(country => !string.IsNullOrWhiteSpace(country) && country.Trim().Length is >= 2 and <= 3 && country.Trim().All(char.IsAsciiLetter));
    }

    /// <summary>Creates the source event carrying the guarded import request parameters.</summary>
    /// <param name="command">The originating command and route.</param>
    /// <param name="economicCalendarImport">The computed import request.</param>
    /// <returns>The private request event persisted through State.Update.</returns>
    internal static EconomicCalendarsImportedEvent CreateEconomicCalendarsImportedEvent(this ImportEconomicCalendarsCommand command, EconomicCalendarImport economicCalendarImport) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, EconomicCalendarsImportedEvent.Actor, EconomicCalendarsImportedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        ImportedDate = economicCalendarImport.ImportedDate,
        DuplicatePolicy = economicCalendarImport.DuplicatePolicy,
        CountryCodes = economicCalendarImport.CountryCodes,
        RequestedOn = command.OriginatedOn,
        RequestedBy = command.OriginatedBy
    };
}
