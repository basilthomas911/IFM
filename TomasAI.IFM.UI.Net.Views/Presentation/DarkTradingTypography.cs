using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace TomasAI.IFM.UI.Net.Views.Presentation;

/// <summary>
/// Applies the application typography before controls are measured or displayed.
/// </summary>
internal static class DarkTradingTypography
{
    internal const string FontFamily = DarkTradingTheme.FontFamily;
    internal const float FontSize = DarkTradingTheme.FontSize;

    internal static void Apply(Control root)
    {
        ApplyToControl(root);
        root.FontChanged -= FontChanged;
        root.FontChanged += FontChanged;
        root.ControlAdded -= ControlAdded;
        root.ControlAdded += ControlAdded;

        foreach (Control child in root.Controls)
            Apply(child);
    }

    static void ControlAdded(object? sender, ControlEventArgs eventArgs)
        => Apply(eventArgs.Control);

    static void ApplyToControl(Control control)
    {
        NormalizeControlFont(control);

        if (control is DateTimePicker dateTimePicker)
            dateTimePicker.CalendarFont = Normalize(dateTimePicker.CalendarFont);

        if (control is Chart chart)
            ApplyToChart(chart);

        if (control is not DataGridView grid)
            return;

        grid.DefaultCellStyle.Font = Normalize(grid.DefaultCellStyle.Font);
        grid.ColumnHeadersDefaultCellStyle.Font = Normalize(grid.ColumnHeadersDefaultCellStyle.Font);
        grid.RowHeadersDefaultCellStyle.Font = Normalize(grid.RowHeadersDefaultCellStyle.Font);
    }

    static void FontChanged(object? sender, EventArgs e)
    {
        if (sender is Control control) NormalizeControlFont(control);
    }

    internal static void NormalizeControlFont(Control control)
    {
        var normalized = Normalize(control.Font);
        if (!ReferenceEquals(normalized, control.Font)) control.Font = normalized;
    }

    static void ApplyToChart(Chart chart)
    {
        foreach (var area in chart.ChartAreas)
        foreach (var axis in new[] { area.AxisX, area.AxisX2, area.AxisY, area.AxisY2 })
        {
            axis.LabelStyle.Font = Normalize(axis.LabelStyle.Font);
            axis.TitleFont = Normalize(axis.TitleFont);
        }

        foreach (var legend in chart.Legends)
            legend.Font = Normalize(legend.Font);
        foreach (var title in chart.Titles)
            title.Font = Normalize(title.Font);
        foreach (var series in chart.Series)
        {
            series.Font = Normalize(series.Font);
            foreach (var point in series.Points)
                point.Font = Normalize(point.Font);
        }
    }

    static Font Normalize(Font? font)
        => font is not null
           && string.Equals(font.Name, FontFamily, StringComparison.OrdinalIgnoreCase)
           && Math.Abs(font.Size - FontSize) < 0.01F
            ? font
            : Create(font?.Style ?? FontStyle.Regular);

    static Font Create(FontStyle style)
        => new(FontFamily, FontSize, style, GraphicsUnit.Point);
}
