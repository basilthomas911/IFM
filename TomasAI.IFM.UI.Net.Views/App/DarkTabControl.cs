namespace TomasAI.IFM.UI.Net.Views.App;

/// <summary>
/// Paints the complete tab surface because the native WinForms tab renderer
/// ignores <see cref="Control.BackColor"/> around the tab pages.
/// </summary>
sealed class DarkTabControl : TabControl
{
    static readonly Color ChromeBackColor = Color.Black;
    static readonly Color SelectedTabColor = Color.FromArgb(48, 48, 48);
    static readonly Color ActiveTabBorderColor = Color.Gray;
    static readonly Color InactiveTabTextColor = Color.LightGray;
    const FontStyle SelectedTabFontStyle = FontStyle.Bold;
    const FontStyle InactiveTabFontStyle = FontStyle.Regular;

    public DarkTabControl()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
        BackColor = Color.Black;
        ForeColor = Color.White;
        Padding = new Point(12, 4);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
        => e.Graphics.Clear(ChromeBackColor);

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(ChromeBackColor);
        using var selectedFont = new Font(Font, SelectedTabFontStyle);
        using var inactiveFont = new Font(Font, InactiveTabFontStyle);

        for (var index = 0; index < TabPages.Count; index++)
        {
            var bounds = Rectangle.Intersect(GetTabRect(index), ClientRectangle);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                continue;
            }

            using var background = new SolidBrush(
                index == SelectedIndex ? SelectedTabColor : ChromeBackColor);
            e.Graphics.FillRectangle(background, bounds);
            if (index == SelectedIndex)
            {
                using var border = new Pen(ActiveTabBorderColor);
                e.Graphics.DrawRectangle(
                    border,
                    bounds.X,
                    bounds.Y,
                    Math.Max(0, bounds.Width - 1),
                    Math.Max(0, bounds.Height - 1));
            }
            TextRenderer.DrawText(
                e.Graphics,
                TabPages[index].Text,
                index == SelectedIndex ? selectedFont : inactiveFont,
                bounds,
                index == SelectedIndex ? ForeColor : InactiveTabTextColor,
                TextFormatFlags.HorizontalCenter
                | TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis);
        }

    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        Invalidate();
    }
}
