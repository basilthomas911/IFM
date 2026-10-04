using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Model;

/// <summary>Computed parameters for a durable EconomicCalendar import request; contains no mutable actor state.</summary>
internal sealed record EconomicCalendarImport(DateTime ImportedDate, ImportDuplicatePolicy DuplicatePolicy, string[] CountryCodes);
