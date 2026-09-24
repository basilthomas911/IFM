using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.Views.Presentation;

namespace TomasAI.IFM.UI.Net.Views.Trade;

/// <summary>Development-only visual preview of the iron-condor Broker Order/Fills tab.</summary>
public sealed class BrokerOrderFillsPreviewControl : DarkTradingView
{
    private static readonly Color Surface = Color.FromArgb(25, 27, 31);
    private static readonly Color Short = Color.FromArgb(110, 24, 30);
    private static readonly Color Long = Color.FromArgb(20, 54, 105);
    private static readonly Color Yellow = Color.FromArgb(245, 216, 77);
    private static readonly Color Green = Color.FromArgb(66, 199, 119);
    private static readonly Color Red = Color.FromArgb(237, 104, 104);
    private readonly TreeView _orderTree;
    private readonly Label _detail;

    /// <summary>Creates sample content for the selected canonical trade without submitting broker commands.</summary>
    public BrokerOrderFillsPreviewControl(
        int portfolioId,
        PortfolioFundEditorModel fund,
        PortfolioFundOrderEditorModel order,
        PortfolioFundOrderTradeEditorModel trade)
    {
        ArgumentNullException.ThrowIfNull(fund);
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(trade);
        Name = "brokerOrderFillsPreview";
        AccessibleName = "Broker Order and Fills development preview";
        Dock = DockStyle.Fill;
        BackColor = Color.Black;
        ForeColor = Color.White;
        Font = new Font("Microsoft Sans Serif", 9F);

        var shell = new TableLayoutPanel
        {
            Name = "brokerOrderFillsPreviewLayout", Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 2, BackColor = Color.Black,
            Padding = new Padding(5, 4, 5, 4)
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var identity = Label("brokerPreviewIdentity",
            $"PREVIEW DATA — Portfolio {portfolioId}  ·  Fund {fund.FundId}: {fund.Name}  ·  " +
            $"Fund Order {order.OrderId}  ·  Trade {trade.TradeId}  ·  {trade.BaseContractId}",
            Color.White);
        identity.BackColor = Surface;
        identity.Padding = new Padding(9, 5, 5, 0);
        shell.Controls.Add(identity, 0, 0);

        var vertical = new SplitContainer
        {
            Name = "brokerOrderFillsVerticalSplit", Dock = DockStyle.Fill,
            Size = new Size(900, 400),
            Orientation = Orientation.Horizontal, BackColor = Color.FromArgb(90, 95, 105),
            SplitterWidth = 5, Panel1MinSize = 140, Panel2MinSize = 105
        };
        vertical.HandleCreated += (_, _) =>
        {
            var available = vertical.Height - vertical.SplitterWidth;
            if (available >= vertical.Panel1MinSize + vertical.Panel2MinSize)
                vertical.SplitterDistance = Math.Clamp(
                    (int)(available * 0.68), vertical.Panel1MinSize,
                    available - vertical.Panel2MinSize);
        };
        shell.Controls.Add(vertical, 0, 1);
        Controls.Add(shell);

        var orderPanel = new Panel { Name = "brokerPreviewOrderPane", Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Black };
        vertical.Panel1.Controls.Add(orderPanel);
        var orderContent = new TableLayoutPanel
        {
            Name = "brokerPreviewOrderContent", Dock = DockStyle.Top, AutoSize = true,
            ColumnCount = 1, BackColor = Color.Black, Padding = new Padding(4)
        };
        orderContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        orderPanel.Controls.Add(orderContent);
        Add(orderContent, Section("BROKER ORDER — OPEN  ·  Iron Condor  ·  16 Delta / 50-point wings  ·  01 Oct 2026"), 30);
        Add(orderContent, Legs(), 142);
        Add(orderContent, Row("brokerPreviewOrderFields",
            Field("Contracts", "1", 86),
            ChoiceField("Order type", 105, ["Limit", "Market"], out _),
            ChoiceField("Time in force", 115, ["Day", "GTC"], out _),
            ChoiceField("Action", 95, ["Open", "Close"], out _),
            Field("Directed venue", "CME", 100)), 48);
        Add(orderContent, Row("brokerPreviewPriceFields",
            Field("Net bid", "15.25", 90), Field("Net mid", "16.25", 90),
            Field("Net ask", "17.25", 90), LimitField(),
            Field("Combo tick", "0.25", 100)), 48);
        var algorithmField = ChoiceField("IFM algorithm", 125,
            ["None", "IFM Atomic Combo"], out var algorithm);
        var paceField = ChoiceField("Pace", 125,
            ["Patient", "Normal", "Urgent"], out var pace);
        pace.SelectedItem = "Normal";
        pace.Enabled = false;
        algorithm.SelectedIndexChanged += (_, _) =>
            pace.Enabled = algorithm.SelectedItem?.ToString() != "None";
        Add(orderContent, Row("brokerPreviewExecutionFields",
            algorithmField, paceField,
            Field("Broker route", "Atomic combo / BAG", 190),
            Field("SMART", "No", 75), Field("Worst approved limit", "14.00", 150)), 48);
        Add(orderContent, Section("FUND ECONOMICS AND LIMITS  ·  sample estimates, not Risk Manager approval"), 27);
        Add(orderContent, Row("brokerPreviewEconomics",
            Field("Est. credit", "$800", 105), Field("Max profit", "$800", 110),
            Field("Max loss", "$1,700", 105), Field("Buying power", "$1,700", 120),
            Field("Est. fees", "$24", 95), Field("Fund capacity", "$25,000", 120)), 39);
        Add(orderContent, Row("brokerPreviewRiskFields",
            Field("Put width", "50", 90), Field("Call width", "50", 90),
            Field("Net delta", "0.00", 95), Field("Net vega", "−0.12", 95),
            Field("Fund risk limit", "$2,000", 130), Field("Validation", "PREVIEW ONLY", 150)), 39);
        Add(orderContent, Row("brokerPreviewFundLimits",
            Field("Available funds", "$25,000", 120), Field("Reserved capital", "$1,700", 120),
            Field("Risk margin", "$1,700", 115), Field("Max return", "47.1%", 105),
            Field("Minimum profit", "$200", 120), Field("Put spread", "$362.50", 105),
            Field("Call spread", "$400.00", 105)), 39);
        Add(orderContent, Row("brokerPreviewAuthority",
            Field("Value source", "Sample / not authoritative", 210),
            Field("As of", "Illustrative only", 135),
            Field("Route check", "Preview / unqualified", 175),
            Field("Risk approval", "Not requested", 145)), 39);
        var actions = Row("brokerPreviewActions",
            PreviewButton("Preview Order"), PreviewButton("Place Order"),
            PreviewButton("Update Limit"), PreviewButton("Cancel Unfilled"));
        Add(orderContent, actions, 43);

        var fills = new SplitContainer
        {
            Name = "brokerPreviewOrderTreeSplit", Dock = DockStyle.Fill,
            Size = new Size(900, 170),
            Orientation = Orientation.Vertical, BackColor = Color.FromArgb(90, 95, 105),
            SplitterWidth = 5, Panel1MinSize = 170, Panel2MinSize = 190
        };
        fills.HandleCreated += (_, _) =>
        {
            var available = fills.Width - fills.SplitterWidth;
            if (available >= fills.Panel1MinSize + fills.Panel2MinSize)
                fills.SplitterDistance = Math.Clamp(
                    (int)(available * 0.40), fills.Panel1MinSize,
                    available - fills.Panel2MinSize);
        };
        vertical.Panel2.Controls.Add(fills);
        var treePanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Color.Black };
        treePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        treePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        treePanel.Controls.Add(Section(
            $"ORDERS / FILLS  ·  sample value date {trade.RequestedTradeDate:dd MMM yyyy}"), 0, 0);
        _orderTree = new TreeView
        {
            Name = "brokerPreviewOrderTree", Dock = DockStyle.Fill,
            BackColor = Surface, ForeColor = Color.White, BorderStyle = BorderStyle.None,
            Font = Font, HideSelection = false, FullRowSelect = true, DrawMode = TreeViewDrawMode.OwnerDrawText
        };
        _orderTree.DrawNode += DrawTreeNode;
        _orderTree.AfterSelect += (_, args) => ShowDetail(args.Node);
        treePanel.Controls.Add(_orderTree, 0, 1);
        fills.Panel1.Controls.Add(treePanel);
        var detailPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Color.Black };
        detailPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        detailPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detailPanel.Controls.Add(Section("SELECTED ORDER / FILL DETAILS"), 0, 0);
        _detail = Label("brokerPreviewSelectedDetail", "", Color.White);
        _detail.BackColor = Surface;
        _detail.Padding = new Padding(12);
        _detail.TextAlign = ContentAlignment.TopLeft;
        detailPanel.Controls.Add(_detail, 0, 1);
        fills.Panel2.Controls.Add(detailPanel);
        SeedTree(portfolioId, fund.FundId, order.OrderId, trade.TradeId);
    }

    private static void Add(TableLayoutPanel layout, Control control, int height)
    {
        var row = layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        control.Dock = DockStyle.Fill;
        layout.Controls.Add(control, 0, row);
    }

    private static Label Label(string name, string text, Color color) => new()
    {
        Name = name, Text = text, ForeColor = color, BackColor = Color.Black,
        Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft
    };

    private static Label Section(string text)
    {
        var label = Label("", text, Color.White);
        label.BackColor = Color.FromArgb(36, 39, 44);
        label.Padding = new Padding(7, 0, 4, 0);
        return label;
    }

    private static FlowLayoutPanel Row(string name, params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            Name = name, Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true,
            BackColor = Surface, Padding = new Padding(3, 3, 3, 0)
        };
        row.Controls.AddRange(controls);
        foreach (Control control in controls)
            control.Tag = control.Width;
        row.Resize += (_, _) =>
        {
            var baseline = controls.Sum(control => (int)control.Tag! + control.Margin.Horizontal);
            var extra = Math.Max(0, row.ClientSize.Width - row.Padding.Horizontal - baseline)
                / Math.Max(1, controls.Length);
            foreach (Control control in controls)
                control.Width = (int)control.Tag! + extra;
        };
        return row;
    }

    private static Control Field(string caption, string value, int width)
    {
        var panel = new TableLayoutPanel
        {
            Name = "brokerPreview" + caption.Replace(" ", ""),
            Width = width, Height = 32, RowCount = 2, Margin = new Padding(4, 0, 4, 0),
            BackColor = Surface
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 14));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(Label("", caption, Color.Silver), 0, 0);
        panel.Controls.Add(Label("", value, Color.White), 0, 1);
        return panel;
    }

    private static Control ChoiceField(string caption, int width, string[] options,
        out ComboBox selector)
    {
        var panel = new TableLayoutPanel
        {
            Name = "brokerPreview" + caption.Replace(" ", ""),
            Width = width, Height = 42, RowCount = 2, Margin = new Padding(4, 0, 4, 0),
            BackColor = Surface
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 14));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(Label("", caption, Color.Silver), 0, 0);
        selector = new ComboBox
        {
            Name = panel.Name + "Selector", Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
            BackColor = Color.Black, ForeColor = Color.White, Font = panel.Font
        };
        selector.Items.AddRange(options);
        selector.SelectedIndex = 0;
        panel.Controls.Add(selector, 0, 1);
        return panel;
    }

    private static Control LimitField()
    {
        var panel = new TableLayoutPanel
        {
            Name = "brokerPreviewNetLimit", Width = 126, Height = 42,
            RowCount = 2, Margin = new Padding(4, 0, 4, 0), BackColor = Surface
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 14));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(Label("", "Net limit", Color.Silver), 0, 0);
        panel.Controls.Add(new NumericUpDown
        {
            Name = "brokerPreviewNetLimitTicks", Dock = DockStyle.Fill,
            DecimalPlaces = 2, Increment = 0.25m, Minimum = -1000,
            Maximum = 1000, Value = 16m, BackColor = Color.Black,
            ForeColor = Color.White, BorderStyle = BorderStyle.None
        }, 0, 1);
        return panel;
    }

    private static Button PreviewButton(string text) => new()
    {
        Name = "brokerPreview" + text.Replace(" ", ""),
        Text = text, Width = 130, Height = 29, Enabled = false,
        BackColor = Color.FromArgb(56, 59, 65), ForeColor = Color.Silver,
        Margin = new Padding(5, 2, 5, 0)
    };

    private static DataGridView Legs()
    {
        var grid = new DataGridView
        {
            Name = "brokerPreviewLegGrid", Dock = DockStyle.Fill, ReadOnly = true,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = Color.Black, GridColor = Color.FromArgb(64, 68, 75),
            BorderStyle = BorderStyle.None, ColumnHeadersHeight = 25, RowTemplate = { Height = 27 },
            EnableHeadersVisualStyles = false
        };
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 48, 54);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        foreach (var name in new[] { "Leg", "Side", "Role", "Contract", "Strike", "Qty", "Bid", "Ask", "Mid", "Delta", "Age", "Selected" })
            grid.Columns.Add(name, name);
        var rows = new[]
        {
            new[] { "1", "BUY", "+LP", "ESZ26 P7700", "7700", "1", "14.25", "14.75", "14.50", "−0.10", "120 ms", "14.75" },
            new[] { "2", "SELL", "−SP", "ESZ26 P7750", "7750", "1", "22.00", "22.50", "22.25", "−0.16", "120 ms", "22.00" },
            new[] { "3", "SELL", "−SC", "ESZ26 C7900", "7900", "1", "19.50", "20.00", "19.75", "+0.16", "120 ms", "19.50" },
            new[] { "4", "BUY", "+LC", "ESZ26 C7950", "7950", "1", "11.00", "11.50", "11.25", "+0.10", "120 ms", "11.50" }
        };
        for (var i = 0; i < rows.Length; i++)
        {
            var index = grid.Rows.Add(rows[i]);
            var color = i is 1 or 2 ? Short : Long;
            grid.Rows[index].DefaultCellStyle.BackColor = color;
            grid.Rows[index].DefaultCellStyle.SelectionBackColor = color;
            grid.Rows[index].DefaultCellStyle.ForeColor = Color.White;
            grid.Rows[index].DefaultCellStyle.SelectionForeColor = Color.White;
        }
        grid.ClearSelection();
        return grid;
    }

    private void SeedTree(int portfolioId, int fundId, int orderId, int tradeId)
    {
        var working = new TreeNode("ORD-042  Iron Condor  2/5  Working  ●") { Name = "brokerPreviewWorkingOrder", Tag = "Working" };
        working.Nodes.Add(new TreeNode("Fill 001  ·  1 combo @ 16.00  ·  10:16:03") { Name = "brokerPreviewFill001", Tag = "Fill 001" });
        working.Nodes.Add(new TreeNode("Fill 002  ·  1 combo @ 15.75  ·  10:18:21") { Name = "brokerPreviewFill002", Tag = "Fill 002" });
        _orderTree.Nodes.Add(working);
        var filled = new TreeNode("ORD-041  Put Spread  3/3  Filled  ●") { Tag = "Filled" };
        filled.Nodes.Add(new TreeNode("Fill 001  ·  3 combos @ 8.25  ·  09:45:10") { Tag = "Fill 001" });
        _orderTree.Nodes.Add(filled);
        _orderTree.Nodes.Add(new TreeNode("ORD-040  Iron Condor  0/1  Cancelled  ●") { Tag = "Cancelled" });
        working.Expand();
        _orderTree.SelectedNode = working;
        _detail.Tag = $"Portfolio {portfolioId} / Fund {fundId} / Fund Order {orderId} / Trade {tradeId}";
        ShowDetail(working);
    }

    private void ShowDetail(TreeNode? node)
    {
        if (node is null) return;
        var identity = _detail.Tag?.ToString() ?? "";
        if (node.Parent is not null)
        {
            _detail.Text = $"{identity}\n\n{node.Parent.Text}\n{node.Text}\n" +
                "Four balanced leg executions · Net price and fees shown as sample data\n" +
                "Execution venue CME · Posting: sample pending\n" +
                "Order mutation is unavailable when a fill is selected.";
            return;
        }
        var facts = node.Tag?.ToString() switch
        {
            "Filled" => "Requested 3 · Filled 3 · Remaining 0 whole combos\n" +
                "Limit credit 8.25 · Average fill credit 8.25\nFees $18.75 · Filled 09:45:10 ET",
            "Cancelled" => "Requested 1 · Filled 0 · Remaining 0 whole combos\n" +
                "Limit credit 14.50 · Cancel acknowledged 09:39:18 ET\nFees $0.00",
            _ => "Requested 5 · Filled 2 · Remaining 3 whole combos\n" +
                "Limit credit 16.00 · Average fill credit 15.875\nFees $12.50 · Last update 10:43:12 ET"
        };
        _detail.Text = $"{identity}\n\n{node.Text}\n{facts}\n" +
            "Directed CME combo/BAG · No SMART · Open · Day\n" +
            "Update Limit / Cancel Unfilled: preview-only; no broker action";
    }

    private void DrawTreeNode(object? sender, DrawTreeNodeEventArgs args)
    {
        if (args.Node is null) return;
        var bounds = args.Bounds;
        var selected = (args.State & TreeNodeStates.Selected) != 0;
        using var background = new SolidBrush(selected ? Color.FromArgb(52, 65, 85) : Surface);
        args.Graphics.FillRectangle(background, new Rectangle(bounds.X, bounds.Y, Math.Max(bounds.Width + 18, 1), bounds.Height));
        var text = args.Node.Text.TrimEnd(' ', '●');
        TextRenderer.DrawText(args.Graphics, text, _orderTree.Font, bounds, Color.White,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        if (args.Node.Parent is not null) return;
        var color = args.Node.Tag?.ToString() switch
        {
            "Filled" => Green, "Cancelled" => Red, _ => Yellow
        };
        var size = TextRenderer.MeasureText(text, _orderTree.Font);
        using var dot = new SolidBrush(color);
        args.Graphics.FillEllipse(dot, bounds.X + size.Width + 3, bounds.Y + (bounds.Height - 10) / 2, 10, 10);
    }
}
