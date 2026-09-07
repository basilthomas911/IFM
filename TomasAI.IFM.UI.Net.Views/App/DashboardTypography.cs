namespace TomasAI.IFM.UI.Net.Views.App;

internal static class DashboardTypography
{
    internal const string FontFamily = DarkTradingTheme.FontFamily;
    internal const float FontSize = DarkTradingTheme.FontSize;
    internal static Font Create(FontStyle style = FontStyle.Regular) => DarkTradingTheme.CreateFont(style);
    internal static void ApplyFamilyAndSize(Control root) => DarkTradingTypography.Apply(root);
}
