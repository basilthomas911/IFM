using System.Drawing;
using System.Windows.Forms;

namespace TomasAI.IFM.UI.Net.Views.App;

internal static class DashboardTypography
{
    internal const string FontFamily = "Microsoft Sans Serif";
    internal const float FontSize = 8F;

    internal static Font Create(FontStyle style = FontStyle.Regular)
        => new(FontFamily, FontSize, style, GraphicsUnit.Point);

    internal static void ApplyFamilyAndSize(Control root)
    {
        root.Font = Create(root.Font.Style);
        foreach (Control child in root.Controls)
        {
            ApplyFamilyAndSize(child);
        }
    }
}
