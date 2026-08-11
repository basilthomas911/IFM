using TomasAI.IFM.UI.Net.ViewModels.MarketData;

namespace TomasAI.IFM.UI.Net.ViewModels.App;

/// <summary>
/// Transitional boundary for live dashboard and trading views that are migrated in later S1.4 slices.
/// </summary>
/// <remarks>
/// Shell, status, error, menu, and close-request state are observable on <see cref="IFMAppViewModel"/> and are
/// intentionally excluded from this adapter.
/// </remarks>
public interface IIFMAppLiveViewAdapter
{
    /// <summary>Publishes the latest futures trade-signal snapshot.</summary>
    void UpdateTradeSignal(FuturesTradeSignalUIViewModel futuresTradeSignal);

    /// <summary>Publishes a trade-placement notification.</summary>
    void NotifyTradePlacement(PlaceTradeUIViewModel placeTrade);

    /// <summary>Closes all open trade blotters during application shutdown.</summary>
    ValueTask CloseTradeBlottersAsync();
}
