namespace TomasAI.IFM.Application.MarketData.FinancialModelingPrep;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serializer contract requires its existing array shape.")]
public sealed record FmpMarketDataImportRequest(
    DateOnly FromInclusive,
    DateOnly ToInclusive,
    bool IncludeTreasury = true,
    bool IncludeEconomicCalendar = true,
    string[]? CountryCodes = null);
