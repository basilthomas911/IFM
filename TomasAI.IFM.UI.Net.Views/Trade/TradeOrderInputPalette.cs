namespace TomasAI.IFM.UI.Net.Views.Trade;

internal static class TradeOrderInputPalette
{
    internal static void Apply(Control root) => DarkTradingTheme.Apply(root);
    internal static void DrawBlackComboBoxItem(object? sender, DrawItemEventArgs e)
        => DarkTradingTheme.DrawBlackComboBoxItem(sender, e);
}
