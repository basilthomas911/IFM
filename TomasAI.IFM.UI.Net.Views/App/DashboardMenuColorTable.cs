namespace TomasAI.IFM.UI.Net.Views.App;

/// <summary>Draws navigation text according to the ToolStrip's current selection.</summary>
sealed class DashboardMenuRenderer() : ToolStripProfessionalRenderer(new DashboardMenuColorTable())
{
    static readonly HashSet<string> NavigationItemNames = new(StringComparer.Ordinal)
    {
        "tradeButton",
        "marketDataButton",
        "fundButton",
        "referenceButton",
        "systemAdminButton"
    };

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (!NavigationItemNames.Contains(e.Item.Name))
        {
            base.OnRenderItemText(e);
            return;
        }

        var selected = e.Item.Selected || e.Item.Pressed;
        using var font = new Font(
            e.TextFont,
            selected ? FontStyle.Bold : FontStyle.Regular);
        e.TextFont = font;
        e.TextColor = NavigationTextColor(selected, e.Item.Enabled);
        base.OnRenderItemText(e);
    }

    internal static Color NavigationTextColor(bool selected, bool enabled)
        => !enabled ? Color.DimGray : selected ? Color.White : Color.LightGray;

    internal static FontStyle NavigationFontStyle(bool selected)
        => selected ? FontStyle.Bold : FontStyle.Regular;
}

/// <summary>Keeps the dashboard command bar dark across Windows visual styles.</summary>
sealed class DashboardMenuColorTable : ProfessionalColorTable
{
    static readonly Color Black = Color.Black;
    static readonly Color Hover = Color.FromArgb(48, 48, 48);
    static readonly Color Pressed = Color.FromArgb(64, 64, 64);

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
