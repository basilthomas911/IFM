using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to import economic calendars.
/// </summary>
/// <param name="ImportedDate">The date when the economic calendars were imported.</param>
/// <param name="CountryCodes">Optional country filters. An empty array requests every country allowed by host policy.</param>
/// <param name="ErrorCode">The error code associated with the operation. Used to indicate specific error conditions or statuses.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve array-valued serialized message and query contracts, including their MessagePack slots.")]
public record ImportEconomicCalendarsParameter(DateTime ImportedDate, string[] CountryCodes, int ErrorCode)
    : ICommandParameter
{
}
