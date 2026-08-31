using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace TomasAI.IFM.UI.Net.Views.App;

internal static class DashboardTypography
{
    internal const string FontFamily = "Microsoft Sans Serif";
    internal const float FontSize = 10F;

    internal static Font Create(FontStyle style = FontStyle.Regular)
        => new(FontFamily, FontSize, style, GraphicsUnit.Point);

    internal static void ApplyFamilyAndSize(Control root)
    {
        root.Font = Create(root.Font.Style);
        ApplySpecializedTypography(root);
        foreach (Control child in root.Controls)
        {
            ApplyFamilyAndSize(child);
        }
    }

    static void ApplySpecializedTypography(Control control)
    {
        if (control is DataGridView grid)
        {
            grid.DefaultCellStyle.Font = Create(grid.DefaultCellStyle.Font?.Style ?? FontStyle.Regular);
            grid.ColumnHeadersDefaultCellStyle.Font = Create(
                grid.ColumnHeadersDefaultCellStyle.Font?.Style ?? FontStyle.Regular);
            grid.RowHeadersDefaultCellStyle.Font = Create(
                grid.RowHeadersDefaultCellStyle.Font?.Style ?? FontStyle.Regular);
        }

        if (control is not Chart chart)
            return;

        foreach (var area in chart.ChartAreas)
        {
            foreach (var axis in new[] { area.AxisX, area.AxisX2, area.AxisY, area.AxisY2 })
            {
                axis.LabelStyle.Font = Create(axis.LabelStyle.Font?.Style ?? FontStyle.Regular);
                axis.TitleFont = Create(axis.TitleFont?.Style ?? FontStyle.Regular);
            }
        }
        foreach (var legend in chart.Legends)
            legend.Font = Create(legend.Font?.Style ?? FontStyle.Regular);
        foreach (var title in chart.Titles)
            title.Font = Create(title.Font?.Style ?? FontStyle.Regular);
        foreach (var series in chart.Series)
        {
            series.Font = Create(series.Font?.Style ?? FontStyle.Regular);
            foreach (var point in series.Points)
                point.Font = Create(point.Font?.Style ?? FontStyle.Regular);
        }
    }
}
