using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Application.MarketData.FinancialModelingPrep;

public sealed class MarketDataImportPolicyOptions
{
    public ImportDuplicatePolicy Treasury { get; set; } = ImportDuplicatePolicy.Overwrite;

    public ImportDuplicatePolicy EconomicCalendar { get; set; } = ImportDuplicatePolicy.Overwrite;

    public MarketDataImportPolicyOptions Validate()
    {
        if (!Enum.IsDefined(Treasury))
            throw new ArgumentOutOfRangeException(nameof(Treasury));
        if (!Enum.IsDefined(EconomicCalendar))
            throw new ArgumentOutOfRangeException(nameof(EconomicCalendar));
        return this;
    }
}

public static class FmpMarketDataRoutes
{
    public const string Import = "/api/marketdata/fmp/import";
}
