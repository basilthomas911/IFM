namespace TomasAI.IFM.UI.Net.Views.Trade;

/// <summary>Buffers the trade section, including its native input controls.</summary>
sealed class BufferedTradePanel : Panel
{
    public BufferedTradePanel()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.ResizeRedraw, true);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            // Composite the entire section when a trade is inserted, resized or revealed.
            parameters.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
            return parameters;
        }
    }
}
