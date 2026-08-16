using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.FinancialModelingPrep;

/// <summary>
/// Exposes the host-selected Financial Modeling Prep reference-data services
/// through the vendor-neutral application boundary.
/// </summary>
public sealed class FinancialModelingPrepReferenceDataApi : IReferenceDataApi
{
    public FinancialModelingPrepReferenceDataApi(
        ITreasuryCurve treasuryCurve,
        IEconomicCalendar economicCalendar)
    {
        TreasuryCurve = treasuryCurve ?? throw new ArgumentNullException(nameof(treasuryCurve));
        EconomicCalendar = economicCalendar ?? throw new ArgumentNullException(nameof(economicCalendar));
    }

    public ITreasuryCurve TreasuryCurve { get; }

    public IEconomicCalendar EconomicCalendar { get; }
}
