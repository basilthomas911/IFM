namespace TomasAI.IFM.UI.Net.Views.Trade;

internal static class TradeOrderInputPalette
{
    internal static void Apply(Control root)
    {
        if (root is ComboBox combo)
        {
            combo.BackColor = Color.Black;
            combo.ForeColor = Color.White;
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.DrawItem -= DrawBlackComboBoxItem;
            combo.DrawItem += DrawBlackComboBoxItem;
        }
        else if (root is DateTimePicker picker)
        {
            picker.BackColor = Color.Black;
            picker.ForeColor = Color.White;
            picker.CalendarMonthBackground = Color.Black;
            picker.CalendarForeColor = Color.White;
            picker.CalendarTitleBackColor = Color.Black;
            picker.CalendarTitleForeColor = Color.White;
            picker.CalendarTrailingForeColor = Color.Gray;
        }

        root.ControlAdded -= ControlAdded;
        root.ControlAdded += ControlAdded;
        foreach (Control child in root.Controls)
            Apply(child);
    }

    static void ControlAdded(object? sender, ControlEventArgs e)
    {
        if (e.Control is not null) Apply(e.Control);
    }

    internal static void DrawBlackComboBoxItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox combo || e.Bounds.Width <= 0 || e.Bounds.Height <= 0)
            return;

        var selected = combo.Enabled && (e.State & DrawItemState.Selected) != 0;
        using var background = new SolidBrush(selected ? SystemColors.Highlight : Color.Black);
        e.Graphics.FillRectangle(background, e.Bounds);
        var text = e.Index >= 0 && e.Index < combo.Items.Count
            ? combo.GetItemText(combo.Items[e.Index]) : combo.Text;
        TextRenderer.DrawText(e.Graphics, text, combo.Font, e.Bounds,
            combo.Enabled ? Color.White : Color.Gray,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if (combo.Enabled && (e.State & DrawItemState.Focus) != 0)
            e.DrawFocusRectangle();
    }
}
