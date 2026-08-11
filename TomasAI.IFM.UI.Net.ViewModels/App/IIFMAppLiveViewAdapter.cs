namespace TomasAI.IFM.UI.Net.ViewModels.App;

/// <summary>
/// Boundary for WinForms-only shell operations that cannot be represented as observable presentation state.
/// </summary>
/// <remarks>
/// All market-data and trading display state is observable on <see cref="IFMAppViewModel"/> and intentionally
/// excluded from this adapter.
/// </remarks>
public interface IIFMAppLiveViewAdapter
{
    /// <summary>Closes all open trade blotters during application shutdown.</summary>
    ValueTask CloseTradeBlottersAsync();
}
