namespace TomasAI.IFM.UI.Net.Views.App;

/// <summary>Draws navigation text according to the ToolStrip's current selection.</summary>
sealed class DashboardMenuRenderer() : ToolStripProfessionalRenderer(new DashboardMenuColorTable())
{
    const string MarketDataFeedButtonName = "marketDataFeedButton";
    const string MarketDataFeedHealthIndicatorName = "marketDataFeedHealthIndicator";
    static readonly HashSet<string> NavigationItemNames = new(StringComparer.Ordinal)
    {
        "tradeButton",
        "marketDataButton",
        "fundButton",
        "referenceButton",
        "systemAdminButton"
    };

    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
    {
        if (!string.Equals(e.Item.Name, MarketDataFeedButtonName, StringComparison.Ordinal))
        {
            base.OnRenderButtonBackground(e);
            return;
        }

        using var background = new SolidBrush(e.Item.BackColor);
        e.Graphics.FillRectangle(background, new Rectangle(Point.Empty, e.Item.Size));
    }

    protected override void OnRenderLabelBackground(ToolStripItemRenderEventArgs e)
    {
        if (!string.Equals(e.Item.Name, MarketDataFeedHealthIndicatorName, StringComparison.Ordinal))
        {
            base.OnRenderLabelBackground(e);
            return;
        }

        using var background = new SolidBrush(e.Item.BackColor);
        e.Graphics.FillRectangle(background, new Rectangle(Point.Empty, e.Item.Size));
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (string.Equals(e.Item.Name, MarketDataFeedHealthIndicatorName, StringComparison.Ordinal))
        {
            e.TextColor = e.Item.ForeColor;
            base.OnRenderItemText(e);
            return;
        }

        if (DarkTradingTheme.IsCommandItem(e.Item) && !DarkTradingTheme.IsCommandEnabled(e.Item))
        {
            // The native disabled renderer substitutes SystemColors.GrayText for TextColor.
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, e.TextRectangle,
                DarkTradingTheme.DisabledText, e.TextFormat);
            return;
        }

        if (!NavigationItemNames.Contains(e.Item.Name))
        {
            if (DarkTradingTheme.IsCommandItem(e.Item))
                e.TextColor = DarkTradingTheme.ButtonTextColor(DarkTradingTheme.IsCommandEnabled(e.Item));
            base.OnRenderItemText(e);
            return;
        }

        var selected = e.Item.Selected || e.Item.Pressed;
        using var font = new Font(
            e.TextFont,
            selected ? FontStyle.Bold : FontStyle.Regular);
        e.TextFont = font;
        e.TextColor = NavigationTextColor(selected, DarkTradingTheme.IsCommandEnabled(e.Item));
        base.OnRenderItemText(e);
    }

    internal static Color NavigationTextColor(bool selected, bool enabled)
        => DarkTradingTheme.ButtonTextColor(enabled);

    internal static FontStyle NavigationFontStyle(bool selected)
        => selected ? FontStyle.Bold : FontStyle.Regular;
}

/// <summary>Keeps the dashboard command bar dark across Windows visual styles.</summary>
sealed class DashboardMenuColorTable : ProfessionalColorTable
{
    static readonly Color Black = Color.Black;
    static readonly Color Hover = DarkTradingTheme.HoverSurface;
    static readonly Color Pressed = DarkTradingTheme.PressedSurface;

    public override Color ToolStripGradientBegin => Black;
    public override Color ToolStripGradientMiddle => Black;
    public override Color ToolStripGradientEnd => Black;
    public override Color ToolStripBorder => Black;
    public override Color ToolStripContentPanelGradientBegin => Black;
    public override Color ToolStripContentPanelGradientEnd => Black;
    public override Color ImageMarginGradientBegin => Black;
    public override Color ImageMarginGradientMiddle => Black;
    public override Color ImageMarginGradientEnd => Black;
    public override Color MenuBorder => Color.Gray;
    public override Color MenuItemBorder => Color.Gray;
    public override Color MenuItemSelected => Hover;
    public override Color MenuItemSelectedGradientBegin => Hover;
    public override Color MenuItemSelectedGradientEnd => Hover;
    public override Color MenuItemPressedGradientBegin => Pressed;
    public override Color MenuItemPressedGradientMiddle => Pressed;
    public override Color MenuItemPressedGradientEnd => Pressed;
    public override Color ButtonSelectedBorder => Color.Gray;
    public override Color ButtonSelectedGradientBegin => Hover;
    public override Color ButtonSelectedGradientMiddle => Hover;
    public override Color ButtonSelectedGradientEnd => Hover;
    public override Color ButtonPressedBorder => Color.Gray;
    public override Color ButtonPressedGradientBegin => Pressed;
    public override Color ButtonPressedGradientMiddle => Pressed;
    public override Color ButtonPressedGradientEnd => Pressed;
    public override Color SeparatorDark => Color.Gray;
    public override Color SeparatorLight => Black;
}
