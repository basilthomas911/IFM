namespace TomasAI.IFM.UI.Net.Views.MarketData;

/// <summary>Keeps disabled reference values readable without making them editable.</summary>
sealed class MarketDataTextBox : TextBox
{
    public MarketDataTextBox()
    {
        BackColor = Color.Black;
        ForeColor = Color.White;
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (Enabled || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        if (message.Msg == 0x000F) // WM_PAINT
        {
            using var graphics = CreateGraphics();
            DrawDisabledText(graphics);
        }
        else if (message.Msg == 0x0318 && message.WParam != IntPtr.Zero) // WM_PRINTCLIENT
        {
            using var graphics = Graphics.FromHdc(message.WParam);
            DrawDisabledText(graphics);
        }
    }

    void DrawDisabledText(Graphics graphics)
    {
        graphics.Clear(BackColor);
        var bounds = Rectangle.Inflate(ClientRectangle, -2, 0);
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl;
        flags |= TextAlign switch
        {
            HorizontalAlignment.Center => TextFormatFlags.HorizontalCenter,
            HorizontalAlignment.Right => TextFormatFlags.Right,
            _ => TextFormatFlags.Left,
        };
        flags |= Multiline
            ? (WordWrap ? TextFormatFlags.WordBreak : TextFormatFlags.Default)
            : TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
        TextRenderer.DrawText(graphics, Text, Font, bounds, ForeColor, BackColor, flags);
    }
}
