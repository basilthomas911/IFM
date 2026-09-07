namespace TomasAI.IFM.UI.Net.Views.Trade;

internal static class TradeOrderTypography
{
    internal const string FontFamily = DarkTradingTheme.FontFamily;
    internal const float FontSize = DarkTradingTheme.FontSize;
    internal static void Apply(Control root) => DarkTradingTypography.Apply(root);
}
