using TomasAI.IFM.UI.Net.Views.Trade;

namespace TomasAI.IFM.UI.Net.Views.MarketData;

internal static class MarketDataInputPalette
{
    internal static void Apply(Control root)
    {
        switch (root)
        {
            case ComboBox or DateTimePicker:
                TradeOrderInputPalette.Apply(root);
                break;
            case TextBoxBase or ListBox or NumericUpDown:
                root.BackColor = Color.Black;
                root.ForeColor = Color.White;
                break;
            case DataGridView grid:
                grid.BackgroundColor = Color.Black;
                grid.EnableHeadersVisualStyles = false;
                foreach (var style in new[] { grid.DefaultCellStyle, grid.RowsDefaultCellStyle,
                    grid.AlternatingRowsDefaultCellStyle, grid.ColumnHeadersDefaultCellStyle,
                    grid.RowHeadersDefaultCellStyle })
                {
                    style.BackColor = Color.Black;
                    style.ForeColor = Color.White;
                    style.SelectionBackColor = SystemColors.Highlight;
                    style.SelectionForeColor = Color.White;
                    style.Font = grid.Font;
                }
                break;
        }

        root.ControlAdded -= ControlAdded;
        root.ControlAdded += ControlAdded;
        foreach (Control child in root.Controls) Apply(child);
    }

    static void ControlAdded(object? sender, ControlEventArgs e)
    {
        if (e.Control is not null) Apply(e.Control);
    }
}
