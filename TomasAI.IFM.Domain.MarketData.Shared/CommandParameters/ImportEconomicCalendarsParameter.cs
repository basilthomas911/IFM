using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to import economic calendars.
/// </summary>
/// <param name="ImportedDate">The date when the economic calendars were imported.</param>
/// <param name="CountryCodes">Optional country filters. An empty array requests every country allowed by host policy.</param>
/// <param name="ErrorCode">The error code associated with the operation. Used to indicate specific error conditions or statuses.</param>
public record ImportEconomicCalendarsParameter(DateTime ImportedDate, string[] CountryCodes, int ErrorCode)
    : ICommandParameter
{
}
