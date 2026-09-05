using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.Views.Portfolio;

static class PortfolioUiStyle
{
    // Matches the established Trade Order form surface exactly.
    public static readonly Color Surface = Color.FromArgb(64, 64, 64);
    public static readonly Color MenuSurface = Color.Black;
    public static readonly Color Border = Color.Gray;
    public const int BorderWidth = 3;
    public static readonly Color DataSurface = Color.Black;
    public static readonly Color Foreground = Color.White;
    public static readonly Font BodyFont = new("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point);

    public static void Apply(Form form)
    {
        form.BackColor = Surface;
        form.ForeColor = Foreground;
        form.Font = BodyFont;
        form.StartPosition = FormStartPosition.CenterParent;
    }

    public static DataGridView Grid(string accessibleName) => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AutoGenerateColumns = true,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        BackgroundColor = DataSurface,
        ForeColor = Foreground,
        GridColor = Surface,
        BorderStyle = BorderStyle.FixedSingle,
        AccessibleName = accessibleName,
        RowHeadersVisible = false,
        EnableHeadersVisualStyles = false,
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Surface, ForeColor = Foreground, Font = BodyFont },
        DefaultCellStyle = new DataGridViewCellStyle { BackColor = DataSurface, ForeColor = Foreground, SelectionBackColor = Color.DarkSlateBlue, SelectionForeColor = Foreground },
    };

    public static Button Button(string text, string accessibleName) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Color.Black,
        Font = BodyFont,
        AccessibleName = accessibleName,
        UseVisualStyleBackColor = true,
        Margin = new Padding(4),
    };

    public static Label Caption(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Foreground,
        TextAlign = ContentAlignment.MiddleRight,
        Dock = DockStyle.Fill,
        Padding = new Padding(3, 7, 3, 0),
    };

    public static Label MenuTitle(string text) => new()
    {
        Name = "portfolioMenuTitle",
        AccessibleName = "Portfolio menu title",
        Text = text,
        AutoSize = true,
        BackColor = MenuSurface,
        ForeColor = Foreground,
        Font = new Font(BodyFont, FontStyle.Bold),
        Margin = new Padding(2, 4, 20, 0),
        Padding = new Padding(0, 7, 0, 0),
    };

    public static TextBox TextBox(string accessibleName, bool readOnly = false) => new()
    {
        AccessibleName = accessibleName,
        BackColor = DataSurface,
        ForeColor = Foreground,
        BorderStyle = BorderStyle.FixedSingle,
        ReadOnly = readOnly,
        Dock = DockStyle.Fill,
    };

    public static ComboBox Combo(string accessibleName) => new()
    {
        AccessibleName = accessibleName,
        BackColor = Surface,
        ForeColor = Foreground,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Dock = DockStyle.Fill,
    };

    public static ComboBox StrategyTimeFrameCombo(string accessibleName)
    {
        var combo = Combo(accessibleName);
        combo.Items.AddRange(TradeStrategyTimeFrames.Allowed.Cast<object>().ToArray());
        return combo;
    }

    public static void SelectStrategyTimeFrame(ComboBox combo, string? name)
    {
        combo.SelectedIndex = -1;
        if (TradeStrategyTimeFrames.TryParseName(name, out var value)) combo.SelectedItem = value;
    }

    public static string SelectedStrategyTimeFrameName(ComboBox combo) =>
        combo.SelectedItem is TimeFrameType value && TradeStrategyTimeFrames.IsAllowed(value)
            ? value.ToString() : string.Empty;
}
