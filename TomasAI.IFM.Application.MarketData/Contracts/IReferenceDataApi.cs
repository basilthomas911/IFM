using TomasAI.IFM.Framework.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>
/// Provides the application-level, vendor-neutral boundary for external
/// reference data used by market-data workflows.
/// </summary>
public interface IReferenceDataApi
{
    /// <summary>
    /// Gets the provider-neutral Treasury-curve service.
    /// </summary>
    ITreasuryCurve TreasuryCurve { get; }

    /// <summary>
    /// Gets the provider-neutral economic-calendar service.
    /// </summary>
    IEconomicCalendar EconomicCalendar { get; }
}
