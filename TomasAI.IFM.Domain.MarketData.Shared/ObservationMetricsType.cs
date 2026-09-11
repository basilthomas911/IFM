namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>
/// Identifies current market, contract, session, or intrinsic-event state that is
/// not defined by both a timeframe and period length.
/// </summary>
public enum ObservationMetricsType : byte
{
    /// <summary>No market observation is selected.</summary>
    Unknown = 0,

    /// <summary>
    /// Current VX futures term structure, including the front contract, second contract,
    /// their prices, and the derived front-to-second relationship.
    /// </summary>
    VxTermStructure = 1,

    /// <summary>Current futures-session Volume Weighted Average Price state.</summary>
    Vwap = 2,

    /// <summary>Current Intrinsic Time Indicator state.</summary>
    Iti = 3
}
