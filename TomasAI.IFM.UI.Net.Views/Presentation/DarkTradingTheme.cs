using System.Windows.Forms.DataVisualization.Charting;

namespace TomasAI.IFM.UI.Net.Views.Presentation;

public static class DarkTradingTheme
{
    public const string FontFamily = "Microsoft Sans Serif";
    public const float FontSize = 10F;
    public const int FrameWidth = 3;
    public const int ButtonGap = 4;
    public static readonly Color Background = Color.Black;
    public static readonly Color Foreground = Color.White;
    public static readonly Color Border = Color.Gray;
    public static readonly Color DisabledText = Color.Gray;
    public static readonly Color CommandSurface = Color.FromArgb(45, 45, 48);
    public static readonly Color HoverSurface = Color.FromArgb(62, 62, 64);
    public static readonly Color PressedSurface = Color.FromArgb(31, 31, 32);
    public static Color Selection => SystemColors.Highlight;
    public static Color ButtonTextColor(bool enabled) => enabled ? Foreground : DisabledText;
    public static Font CreateFont(FontStyle style = FontStyle.Regular)
        => new(FontFamily, FontSize, style, GraphicsUnit.Point);

    public static void Apply(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);
        DarkTradingTypography.Apply(root);
        ApplyPalette(root);
    }

    static void ApplyPalette(Control root)
    {
        if (root is Button button)
        {
            button.UseVisualStyleBackColor = false;
            if (IsNeutral(button.BackColor) || button.BackColor == CommandSurface)
            {
                button.BackColor = CommandSurface;
            }
            button.EnabledChanged -= ButtonStateChanged;
            button.EnabledChanged += ButtonStateChanged;
            button.ForeColorChanged -= ButtonStateChanged;
            button.ForeColorChanged += ButtonStateChanged;
            ButtonStateChanged(button, EventArgs.Empty);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.MouseOverBackColor = HoverSurface;
            button.FlatAppearance.MouseDownBackColor = PressedSurface;
            button.Margin = new Padding(ButtonGap / 2);
            button.Paint -= PaintDisabledButton;
            button.Paint += PaintDisabledButton;
        }
        else if (root is Form or UserControl or Panel or GroupBox or Label or TabPage
                 or CheckBox or RadioButton or TextBoxBase or NumericUpDown or ListBox or ListView or TreeView)
        {
            // Keep colored status indicators; normalize the neutral editor surfaces.
            if (IsNeutral(root.BackColor))
            {
                root.BackColor = IsFrame(root) || IsSeparator(root) ? Border : Background;
                if (IsNeutral(root.ForeColor)) root.ForeColor = Foreground;
            }
        }

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

        switch (root)
        {
            case LinkLabel link:
                link.LinkColor = Foreground;
                link.ActiveLinkColor = Selection;
                link.VisitedLinkColor = Foreground;
                link.DisabledLinkColor = DisabledText;
                break;
            case Label label:
                label.Paint -= PaintDisabledLabel;
                label.Paint += PaintDisabledLabel;
                break;
            case DataGridView grid:
                ApplyGrid(grid);
                grid.ColumnAdded -= GridColumnAdded;
                grid.ColumnAdded += GridColumnAdded;
                break;
            case ListView list:
                DarkTradingListHeader.Apply(list);
                list.OwnerDraw = true;
                list.HideSelection = false;
                list.DrawColumnHeader -= DrawListHeader;
                list.DrawColumnHeader += DrawListHeader;
                list.DrawItem -= DrawListItem;
                list.DrawItem += DrawListItem;
                list.DrawSubItem -= DrawListSubItem;
                list.DrawSubItem += DrawListSubItem;
                break;
            case ToolStrip strip:
                strip.BackColor = Background;
                strip.ForeColor = Foreground;
                strip.Renderer = new App.DashboardMenuRenderer();
                foreach (ToolStripItem item in strip.Items) ApplyItem(item);
                strip.ItemAdded -= StripItemAdded;
                strip.ItemAdded += StripItemAdded;
                strip.EnabledChanged -= StripStateChanged;
                strip.EnabledChanged += StripStateChanged;
                break;
            case PropertyGrid property:
                property.BackColor = Background;
                property.ForeColor = Foreground;
                property.ViewBackColor = Background;
                property.ViewForeColor = Foreground;
                property.HelpBackColor = Background;
                property.HelpForeColor = Foreground;
                property.CategoryForeColor = Foreground;
                property.CategorySplitterColor = Border;
                property.LineColor = CommandSurface;
                property.SelectedItemWithFocusBackColor = Selection;
                property.SelectedItemWithFocusForeColor = Foreground;
                break;
            case SplitContainer split:
                split.BackColor = Background;
                split.Paint -= PaintSplitter;
                split.Paint += PaintSplitter;
                break;
            case MonthCalendar calendar:
                calendar.BackColor = Background;
                calendar.ForeColor = Foreground;
                calendar.TitleBackColor = Background;
                calendar.TitleForeColor = Foreground;
                calendar.TrailingForeColor = DisabledText;
                break;
            case Chart chart:
                ApplyChart(chart);
                break;
        }

        if (root.ContextMenuStrip is { } menu) Apply(menu);
        root.ContextMenuStripChanged -= ContextMenuChanged;
        root.ContextMenuStripChanged += ContextMenuChanged;

        root.ControlAdded -= ControlAdded;
        root.ControlAdded += ControlAdded;
        foreach (Control child in root.Controls)
            ApplyPalette(child);
    }

    internal static bool IsFrame(Control control) => control is Panel
        && control.Parent is Form && control.Dock == DockStyle.Fill
        && control.Padding.All == FrameWidth;

    static void PaintSplitter(object? sender, PaintEventArgs e)
    {
        if (sender is not SplitContainer split) return;
        var bounds = split.SplitterRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        using var background = new SolidBrush(Background);
        e.Graphics.FillRectangle(background, bounds);
        using var pen = new Pen(Border);
        if (split.Orientation == Orientation.Vertical)
            e.Graphics.DrawLine(pen, bounds.Left + bounds.Width / 2, bounds.Top,
                bounds.Left + bounds.Width / 2, bounds.Bottom - 1);
        else
            e.Graphics.DrawLine(pen, bounds.Left, bounds.Top + bounds.Height / 2,
                bounds.Right - 1, bounds.Top + bounds.Height / 2);
    }

    static bool IsSeparator(Control control) => control is Panel
        && (control.Height <= FrameWidth || control.Width <= FrameWidth)
        && control.BackColor.ToArgb() == Border.ToArgb();

    static void ContextMenuChanged(object? sender, EventArgs e)
    {
        if (sender is Control { ContextMenuStrip: { } menu }) Apply(menu);
    }

    static void ApplyGrid(DataGridView grid)
    {
        grid.BackgroundColor = Background;
        grid.GridColor = Border;
        grid.EnableHeadersVisualStyles = false;
        foreach (var style in new[] { grid.DefaultCellStyle, grid.RowsDefaultCellStyle,
                     grid.AlternatingRowsDefaultCellStyle }) ApplyCellStyle(style, false);
        ApplyCellStyle(grid.ColumnHeadersDefaultCellStyle, true);
        ApplyCellStyle(grid.RowHeadersDefaultCellStyle, true);
        foreach (DataGridViewColumn column in grid.Columns) ApplyCellStyle(column.DefaultCellStyle, false);
    }

    static void ApplyCellStyle(DataGridViewCellStyle style, bool header)
    {
        if (style.BackColor.IsEmpty || IsNeutral(style.BackColor) || style.BackColor == CommandSurface)
            style.BackColor = header ? CommandSurface : Background;
        if (style.ForeColor.IsEmpty || IsNeutral(style.ForeColor)) style.ForeColor = Foreground;
        style.SelectionBackColor = Selection;
        style.SelectionForeColor = Foreground;
        if (style.Font is null || style.Font.Name != FontFamily || Math.Abs(style.Font.Size - FontSize) > 0.01F)
            style.Font = CreateFont(style.Font?.Style ?? FontStyle.Regular);
    }

    static void GridColumnAdded(object? sender, DataGridViewColumnEventArgs e)
        => ApplyCellStyle(e.Column.DefaultCellStyle, false);

    static void DrawListHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        using var background = new SolidBrush(CommandSurface);
        e.Graphics.FillRectangle(background, e.Bounds);
        ControlPaint.DrawBorder(e.Graphics, e.Bounds, Border, ButtonBorderStyle.Solid);
        var bounds = Rectangle.Inflate(e.Bounds, -4, 0);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text, e.Font, bounds, Foreground,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    static void DrawListItem(object? sender, DrawListViewItemEventArgs e)
    {
        if (sender is ListView { View: View.Details }) return;
        e.DrawDefault = true;
    }

    static void DrawListSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        if (e.Item is null || e.SubItem is null || sender is not ListView list) return;
        using var background = new SolidBrush(e.Item.Selected ? Selection : e.SubItem.BackColor);
        e.Graphics.FillRectangle(background, e.Bounds);
        var alignment = e.Header?.TextAlign switch
        {
            HorizontalAlignment.Right => TextFormatFlags.Right,
            HorizontalAlignment.Center => TextFormatFlags.HorizontalCenter,
            _ => TextFormatFlags.Left
        };
        TextRenderer.DrawText(e.Graphics, e.SubItem.Text, list.Font, Rectangle.Inflate(e.Bounds, -3, 0),
            !list.Enabled ? DisabledText : e.Item.Selected ? Foreground : e.SubItem.ForeColor,
            alignment | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if (e.Item.Focused && list.Focused) ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds);
    }

    static void StripItemAdded(object? sender, ToolStripItemEventArgs e) => ApplyItem(e.Item);

    internal static bool IsCommandItem(ToolStripItem item) => item is ToolStripButton or ToolStripDropDownItem;

    internal static bool IsCommandEnabled(ToolStripItem item)
        => item.Enabled && (item.Owner?.Enabled ?? true)
           && (item.Owner is not ToolStripDropDown { OwnerItem: { } parent } || IsCommandEnabled(parent));

    static void StripStateChanged(object? sender, EventArgs e)
    {
        if (sender is not ToolStrip strip) return;
        foreach (ToolStripItem item in strip.Items)
            if (IsCommandItem(item)) CommandItemStateChanged(item, EventArgs.Empty);
    }

    static void ButtonStateChanged(object? sender, EventArgs e)
    {
        if (sender is not Button button) return;
        var color = ButtonTextColor(button.Enabled);
        if (button.ForeColor != color) button.ForeColor = color;
        button.Invalidate();
    }

    static void CommandItemStateChanged(object? sender, EventArgs e)
    {
        if (sender is not ToolStripItem item) return;
        var color = ButtonTextColor(IsCommandEnabled(item));
        if (item.ForeColor != color) item.ForeColor = color;
        item.Invalidate();
        if (item is ToolStripDropDownItem dropDown)
            foreach (ToolStripItem child in dropDown.DropDownItems)
                if (IsCommandItem(child)) CommandItemStateChanged(child, EventArgs.Empty);
    }

    static void ApplyItem(ToolStripItem? item)
    {
        if (item is null) return;
        if (item.Font.Name != FontFamily || Math.Abs(item.Font.Size - FontSize) > 0.01F)
            item.Font = CreateFont(item.Font.Style);
        if (item.Name is not ("marketDataFeedButton" or "marketDataFeedHealthIndicator") && IsNeutral(item.BackColor))
        {
            item.BackColor = Background;
            if (IsNeutral(item.ForeColor)) item.ForeColor = Foreground;
        }
        if (item is ToolStripControlHost host) Apply(host.Control);
        if (IsCommandItem(item))
        {
            item.EnabledChanged -= CommandItemStateChanged;
            item.EnabledChanged += CommandItemStateChanged;
            item.ForeColorChanged -= CommandItemStateChanged;
            item.ForeColorChanged += CommandItemStateChanged;
            item.OwnerChanged -= CommandItemStateChanged;
            item.OwnerChanged += CommandItemStateChanged;
            CommandItemStateChanged(item, EventArgs.Empty);
        }
        if (item is ToolStripDropDownItem dropDown)
        {
            dropDown.DropDownOpening -= DropDownOpening;
            dropDown.DropDownOpening += DropDownOpening;
            Apply(dropDown.DropDown);
        }
    }

    static void DropDownOpening(object? sender, EventArgs e)
    {
        if (sender is ToolStripDropDownItem item) Apply(item.DropDown);
    }

    static void ApplyChart(Chart chart)
    {
        chart.BackColor = Background;
        foreach (var area in chart.ChartAreas)
        {
            area.BackColor = Background;
            foreach (var axis in new[] { area.AxisX, area.AxisY, area.AxisX2, area.AxisY2 })
            {
                axis.LabelStyle.ForeColor = Foreground;
                axis.TitleForeColor = Foreground;
                axis.LineColor = Border;
                axis.MajorGrid.LineColor = CommandSurface;
                axis.MinorGrid.LineColor = CommandSurface;
            }
        }
        foreach (var legend in chart.Legends) { legend.BackColor = Background; legend.ForeColor = Foreground; }
        foreach (var title in chart.Titles) title.ForeColor = Foreground;
    }

    static bool IsNeutral(Color color) =>
        color.A == 0 || (color.R == color.G && color.G == color.B);

    static void PaintDisabledButton(object? sender, PaintEventArgs e)
    {
        if (sender is not Button { Enabled: false } button || button.Image is not null || button.ImageList is not null) return;

        // Native flat buttons use dark disabled text, which disappears on this palette.
        var bounds = Rectangle.Inflate(button.ClientRectangle, -2, -2);
        using var background = new SolidBrush(button.BackColor);
        e.Graphics.FillRectangle(background, bounds);
        TextRenderer.DrawText(e.Graphics, button.Text, button.Font, bounds, Color.Gray,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }

    static void PaintDisabledLabel(object? sender, PaintEventArgs e)
    {
        if (sender is not Label { Enabled: false, Image: null } label || !IsNeutral(label.BackColor)) return;
        using var background = new SolidBrush(label.BackColor);
        e.Graphics.FillRectangle(background, label.ClientRectangle);
        var flags = TextFormatFlags.WordBreak;
        if (label.TextAlign is ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight)
            flags |= TextFormatFlags.VerticalCenter;
        else if (label.TextAlign is ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight)
            flags |= TextFormatFlags.Bottom;
        if (label.TextAlign is ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter)
            flags |= TextFormatFlags.HorizontalCenter;
        else if (label.TextAlign is ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight)
            flags |= TextFormatFlags.Right;
        var bounds = new Rectangle(label.Padding.Left, label.Padding.Top,
            Math.Max(0, label.ClientSize.Width - label.Padding.Horizontal),
            Math.Max(0, label.ClientSize.Height - label.Padding.Vertical));
        TextRenderer.DrawText(e.Graphics, label.Text, label.Font, bounds, DisabledText, flags);
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
