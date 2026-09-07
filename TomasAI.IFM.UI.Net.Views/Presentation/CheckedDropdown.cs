namespace TomasAI.IFM.UI.Net.Views.Presentation;

public sealed record CheckedDropdownItem(string Value, string DisplayName, bool IsEnabled = true);

/// <summary>Reusable multi-select dropdown. Text is a read-only summary; values retain their stable identity.</summary>
public sealed class CheckedDropdown : UserControl
{
    readonly TextBox display = new() { ReadOnly = true, BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, TabStop = false };
    readonly Button toggle = new() { Text = "▼", Dock = DockStyle.Right, Width = 28, TabStop = true, AccessibleName = "Open selections" };
    readonly TextBox search = new() { Dock = DockStyle.Top, PlaceholderText = "Search values", AccessibleName = "Filter choices" };
    readonly CheckedListBox list = new() { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, HorizontalScrollbar = true, BorderStyle = BorderStyle.None };
    readonly ToolStripDropDown popup = new() { AutoClose = true, AutoSize = false, Padding = Padding.Empty };
    readonly Panel popupBody = new() { Padding = new Padding(4) };
    readonly HashSet<string> selected = new(StringComparer.Ordinal);
    CheckedDropdownItem[] items = [];
    bool updating;

    public event EventHandler? SelectionChanged;
    public string[] SelectedValues => OrderedItems().Where(x => selected.Contains(x.Value)).Select(x => x.Value).ToArray();
    public string DisplayText => display.Text;
    public bool HasUnavailableSelections => OrderedItems().Any(x => selected.Contains(x.Value) && !x.IsEnabled);

    public CheckedDropdown()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        BorderStyle = BorderStyle.FixedSingle;
        Height = 28;
        Padding = new Padding(3, 3, 0, 0);
        Controls.Add(display); Controls.Add(toggle);
        popupBody.Controls.Add(list); popupBody.Controls.Add(search);
        var host = new ToolStripControlHost(popupBody) { AutoSize = false, Margin = Padding.Empty, Padding = Padding.Empty };
        popup.Items.Add(host);
        DarkTradingTheme.Apply(this); DarkTradingTheme.Apply(popupBody);
        popup.BackColor = DarkTradingTheme.Background;
        toggle.Margin = Padding.Empty;
        toggle.Click += (_, _) => ToggleDropdown(); display.Click += (_, _) => ToggleDropdown();
        search.TextChanged += (_, _) => RebuildList();
        list.ItemCheck += (_, e) =>
        {
            if (updating) return;
            var item = ((Choice)list.Items[e.Index]).Item;
            if (e.NewValue == CheckState.Checked && !item.IsEnabled) { e.NewValue = e.CurrentValue; return; }
            if (e.NewValue == CheckState.Checked) selected.Add(item.Value); else selected.Remove(item.Value);
            UpdateDisplay(); // ItemCheck fires before CheckedItems changes.
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        };
        search.KeyDown += (_, e) => { if (e.KeyCode == Keys.Down) { list.Focus(); if (list.Items.Count > 0) list.SelectedIndex = 0; e.Handled = true; } };
        popup.Closed += (_, _) => toggle.Focus();
        EnabledChanged += (_, _) => { display.ForeColor = Enabled ? DarkTradingTheme.Foreground : DarkTradingTheme.DisabledText; if (!Enabled) popup.Close(); };
    }

    public void SetItems(IEnumerable<CheckedDropdownItem> values)
    {
        var next = values.ToArray();
        if (next.Any(x => string.IsNullOrWhiteSpace(x.Value) || string.IsNullOrWhiteSpace(x.DisplayName))
            || next.Select(x => x.Value).Distinct(StringComparer.Ordinal).Count() != next.Length)
            throw new ArgumentException("Dropdown choices must have distinct, nonempty values and labels.", nameof(values));
        items = next; RebuildList(); UpdateDisplay();
    }

    public void SetSelectedValues(IEnumerable<string> values)
    {
        var next = values.ToArray();
        if (next.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Selected values cannot be empty.", nameof(values));
        selected.Clear(); selected.UnionWith(next); RebuildList(); UpdateDisplay();
    }

    IEnumerable<CheckedDropdownItem> OrderedItems() => items.Concat(selected.Except(items.Select(x => x.Value), StringComparer.Ordinal)
        .Order(StringComparer.Ordinal).Select(x => new CheckedDropdownItem(x, x, false)));

    void RebuildList()
    {
        updating = true; list.BeginUpdate();
        try
        {
            list.Items.Clear();
            foreach (var item in OrderedItems().Where(x => x.Value.Contains(search.Text, StringComparison.OrdinalIgnoreCase)
                || x.DisplayName.Contains(search.Text, StringComparison.OrdinalIgnoreCase)))
                list.Items.Add(new Choice(item), selected.Contains(item.Value));
        }
        finally { list.EndUpdate(); updating = false; }
    }

    void UpdateDisplay()
    {
        display.Text = string.Join(", ", OrderedItems().Where(x => selected.Contains(x.Value)).Select(x => x.IsEnabled ? x.DisplayName : $"Unavailable: {x.DisplayName}"));
        display.SelectionStart = 0; display.SelectionLength = 0;
        display.AccessibleName = AccessibleName + " selections";
    }

    void ToggleDropdown()
    {
        if (popup.Visible) { popup.Close(); return; }
        if (!Enabled) return;
        search.Clear();
        list.AccessibleName = AccessibleName + " choices";
        popupBody.Font = Font;
        var area = Screen.FromControl(this).WorkingArea;
        var width = Math.Min(Math.Max(Width, 260), area.Width - 12);
        var height = Math.Min(320, area.Height - 12);
        popup.Size = new(width, height);
        popup.Items[0].Size = new(width - 2, height - 2);
        popupBody.Size = popup.Items[0].Size;
        popup.Show(this, new Point(0, Height)); search.Focus();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData is (Keys.Alt | Keys.Down) or Keys.F4) { ToggleDropdown(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) popup.Dispose();
        base.Dispose(disposing);
    }

    sealed record Choice(CheckedDropdownItem Item)
    {
        public override string ToString() => Item.IsEnabled ? Item.DisplayName : $"Unavailable: {Item.DisplayName} (uncheck to remove)";
    }
}
