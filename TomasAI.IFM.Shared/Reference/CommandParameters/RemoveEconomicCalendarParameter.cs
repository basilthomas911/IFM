using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Reference.CommandParameters;

/// <summary>
/// Represents the parameters required to remove an economic calendar.
/// </summary>
/// <param name="EconomicCalendarId">The unique identifier of the economic calendar to remove.</param>
/// <param name="Overwrite">Indicates whether to overwrite the existing economic calendar.</param>
/// <param name="ErrorCode">The error code associated with the operation. Used to indicate specific error conditions or statuses.</param>
public record RemoveEconomicCalendarParameter(EconomicCalendarId EconomicCalendarId, bool Overwrite, int ErrorCode)
    : ICommandParameter
{
}
