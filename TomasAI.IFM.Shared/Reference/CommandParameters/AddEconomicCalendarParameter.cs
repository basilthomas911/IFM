using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.Reference.CommandParameters;

/// <summary>
/// Represents the parameters required to add an economic calendar.
/// </summary>
/// <param name="EconomicCalendar">The economic calendar details to add. Cannot be null.</param>
/// <param name="ErrorCode">The error code associated with the operation. Used to indicate specific error conditions or statuses.</param>
public record AddEconomicCalendarParameter(EconomicCalendarReadModel EconomicCalendar, int ErrorCode)
    : ICommandParameter
{
}
