namespace TomasAI.IFM.UI.Net.Views.MarketData;

/// <summary>Applies the final Market Data font before hosting and to dynamically added controls.</summary>
internal static class MarketDataTypography
{
    internal static void Apply(Control root)
    {
        root.SuspendLayout();
        try
        {
            root.Font = Normalize(root.Font);
            if (root is DateTimePicker picker)
                picker.CalendarFont = Normalize(picker.CalendarFont);
            root.ControlAdded -= ControlAdded;
            root.ControlAdded += ControlAdded;
            foreach (Control child in root.Controls) Apply(child);
        }
        finally { root.ResumeLayout(true); }
    }

    static void ControlAdded(object? sender, ControlEventArgs e)
    {
        if (e.Control is not null) Apply(e.Control);
    }

    static Font Normalize(Font font)
        => font.Name == "Microsoft Sans Serif" && Math.Abs(font.SizeInPoints - 10F) < 0.01F
            ? font
            : new Font("Microsoft Sans Serif", 10F, font.Style, GraphicsUnit.Point);
}
