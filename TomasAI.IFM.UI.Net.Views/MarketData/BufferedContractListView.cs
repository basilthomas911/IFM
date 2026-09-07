namespace TomasAI.IFM.UI.Net.Views.MarketData;

/// <summary>Double-buffered native virtual list for contract browsing.</summary>
internal sealed class BufferedContractListView : ListView
{
    public BufferedContractListView()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }
}
