using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.Reference.CommandParameters;

/// <summary>
/// Represents the parameters required to import economic calendars.
/// </summary>
/// <param name="ImportedDate">The date when the economic calendars were imported.</param>
/// <param name="EconomicCalendars">The array of economic calendars to import. Cannot be null.</param>
/// <param name="ErrorCode">The error code associated with the operation. Used to indicate specific error conditions or statuses.</param>
public record ImportEconomicCalendarsParameter(DateTime ImportedDate, EconomicCalendarReadModel[] EconomicCalendars, int ErrorCode)
    : ICommandParameter
{
}
