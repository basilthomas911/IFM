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
    const int DefaultHorizontalPadding = 12;
    const int ClosableHorizontalPadding = 22;
    const int CloseGlyphSize = 10;
    const int CloseGlyphRightInset = 8;
    const int CloseGlyphTextGap = 6;
    const FontStyle SelectedTabFontStyle = FontStyle.Bold;
    const FontStyle InactiveTabFontStyle = FontStyle.Regular;
    bool _showCloseButtons;

    /// <summary>Raised when the close glyph on a tab header is clicked.</summary>
    public event EventHandler<TabCloseRequestedEventArgs>? TabCloseRequested;

    /// <summary>Gets or sets whether each tab header reserves space for and renders a close glyph.</summary>
    [System.ComponentModel.DefaultValue(false)]
    public bool ShowCloseButtons
    {
        get => _showCloseButtons;
        set
        {
            if (_showCloseButtons == value)
                return;

            _showCloseButtons = value;
            Padding = new Point(
                value ? ClosableHorizontalPadding : DefaultHorizontalPadding,
                4);
            Invalidate();
        }
    }

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
        Padding = new Point(DefaultHorizontalPadding, 4);
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

            var textBounds = bounds;
            if (ShowCloseButtons)
            {
                var closeBounds = GetCloseGlyphBounds(bounds);
                textBounds.Width = Math.Max(
                    0,
                    closeBounds.Left - CloseGlyphTextGap - textBounds.Left);
                DrawCloseGlyph(e.Graphics, closeBounds, index == SelectedIndex
                    ? ForeColor
                    : InactiveTabTextColor);
            }
            TextRenderer.DrawText(
                e.Graphics,
                TabPages[index].Text,
                index == SelectedIndex ? selectedFont : inactiveFont,
                textBounds,
                index == SelectedIndex ? ForeColor : InactiveTabTextColor,
                TextFormatFlags.HorizontalCenter
                | TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis);
        }

    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (ShowCloseButtons && e.Button == MouseButtons.Left)
        {
            for (var index = 0; index < TabPages.Count; index++)
            {
                var bounds = Rectangle.Intersect(GetTabRect(index), ClientRectangle);
                if (bounds.Width <= 0
                    || bounds.Height <= 0
                    || !GetCloseGlyphBounds(bounds).Contains(e.Location))
                    continue;

                SelectedIndex = index;
                TabCloseRequested?.Invoke(
                    this,
                    new TabCloseRequestedEventArgs(TabPages[index]));
                return;
            }
        }

        base.OnMouseDown(e);
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        Invalidate();
    }

    static Rectangle GetCloseGlyphBounds(Rectangle tabBounds)
        => new(
            tabBounds.Right - CloseGlyphRightInset - CloseGlyphSize,
            tabBounds.Top + Math.Max(0, (tabBounds.Height - CloseGlyphSize) / 2),
            CloseGlyphSize,
            CloseGlyphSize);

    static void DrawCloseGlyph(Graphics graphics, Rectangle bounds, Color color)
    {
        using var pen = new Pen(color, 1.5F);
        graphics.DrawLine(pen, bounds.Left + 1, bounds.Top + 1, bounds.Right - 2, bounds.Bottom - 2);
        graphics.DrawLine(pen, bounds.Right - 2, bounds.Top + 1, bounds.Left + 1, bounds.Bottom - 2);
    }
}

sealed class TabCloseRequestedEventArgs(TabPage tabPage) : EventArgs
{
    public TabPage TabPage { get; } = tabPage;
}
