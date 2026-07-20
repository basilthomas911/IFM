using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.Reference.CommandParameters;

/// <summary>
/// Represents the parameters required to change an economic calendar.
/// </summary>
/// <param name="EconomicCalendarId">The unique identifier of the economic calendar to change.</param>
/// <param name="EconomicCalendar">The updated economic calendar details. Cannot be null.</param>
/// <param name="Overwrite">Indicates whether to overwrite the existing economic calendar.</param>
/// <param name="ErrorCode">The error code associated with the operation. Used to indicate specific error conditions or statuses.</param>
public record ChangeEconomicCalendarParameter(EconomicCalendarId EconomicCalendarId, EconomicCalendarReadModel EconomicCalendar, bool Overwrite, int ErrorCode)
    : ICommandParameter
{
}
