namespace TomasAI.IFM.UI.Net.Views.Trade;

internal static class TradeOrderInputPalette
{
    internal static void Apply(Control root) => DarkTradingTheme.Apply(root);
    internal static void ApplyBlackBackgrounds(Control root, bool includeButtons = true, Control? excludedControl = null)
    {
        if ((root is Label or TextBoxBase or ComboBox or NumericUpDown or ButtonBase
            or Panel or GroupBox or UserControl) && (includeButtons || root is not ButtonBase) && root != excludedControl)
        {
            root.BackColor = Color.Black;
            if (root is ButtonBase button) button.UseVisualStyleBackColor = false;
            root.BackColorChanged -= KeepBlackBackground;
            root.BackColorChanged += KeepBlackBackground;
        }
        foreach (Control child in root.Controls) ApplyBlackBackgrounds(child, includeButtons, excludedControl);
    }

    private static void KeepBlackBackground(object? sender, EventArgs e)
    {
        if (sender is Control control && control.BackColor != Color.Black)
            control.BackColor = Color.Black;
    }

    internal static void DrawBlackComboBoxItem(object? sender, DrawItemEventArgs e)
        => DarkTradingTheme.DrawBlackComboBoxItem(sender, e);
}
