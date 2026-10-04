using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.UI.Net.Services.Subscriptions;
using TomasAI.IFM.UI.Net.Contracts;
using System.Globalization;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.Views.Presentation;

namespace TomasAI.IFM.UI.Net.Views.Trade;

/// <summary>Shared development-only visual preview of iron-condor broker trade and fill evidence.</summary>
public class BrokerOrderFillsPreviewControl : DarkTradingView
{
    private const int FieldRowHeight = 24;
    private const int FieldSeparatorHeight = 2;
    private const string BrokerFieldRowName = "Broker Trade field row";
    private static readonly Color Surface = Color.FromArgb(25, 27, 31);
    private static readonly Color Short = Color.FromArgb(110, 24, 30);
    private static readonly Color Long = Color.FromArgb(20, 54, 105);
    private static readonly Color Yellow = Color.FromArgb(245, 216, 77);
    private static readonly Color Green = Color.FromArgb(66, 199, 119);
    private static readonly Color Red = Color.FromArgb(237, 104, 104);
    protected readonly TreeView _orderTree;
    protected readonly PropertyGrid _detail;
    protected readonly NumericUpDown _updateOrderPrice;
    public decimal UpdateOrderPrice => _updateOrderPrice.Value;
    private readonly SplitContainer _vertical;
    private readonly Button _placeOrder;
    private readonly Button _updateLimit;
    private readonly Button _cancelUnfilled;
    public event EventHandler? ManageQualificationRequested;
    public void EnableQualificationManagement()
    {
        var button = PreviewButton("Manage qualification...");
        button.Name = "manageBrokerQualification";
        button.Width = 180;
        button.Enabled = true;
        button.Click += (_, _) => ManageQualificationRequested?.Invoke(this, EventArgs.Empty);
        Controls.Find("brokerPreviewActions", true).Single().Controls.Add(button);
    }

    public event EventHandler? PlaceOrderRequested;
    public event EventHandler? UpdateLimitRequested;
    public event EventHandler? CancelUnfilledRequested;


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
            Name = "brokerOrderFillsPreviewLayout",
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Black,
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

        _vertical = new SplitContainer
        {
            Name = "brokerOrderFillsVerticalSplit",
            Dock = DockStyle.Fill,
            Size = new Size(900, 400),
            Orientation = Orientation.Horizontal,
            BackColor = Color.FromArgb(90, 95, 105),
            SplitterWidth = 5,
            Panel1MinSize = 140,
            Panel2MinSize = 105
        };
        _vertical.HandleCreated += (_, _) =>
        {
            if (_vertical.Panel1Collapsed || _vertical.Panel2Collapsed)
                return;
            var available = _vertical.Height - _vertical.SplitterWidth;
            if (available >= _vertical.Panel1MinSize + _vertical.Panel2MinSize)
                _vertical.SplitterDistance = Math.Clamp(
                    (int)(available * 0.68), _vertical.Panel1MinSize,
                    available - _vertical.Panel2MinSize);
        };
        shell.Controls.Add(_vertical, 0, 1);
        Controls.Add(shell);

        var orderPanel = new Panel { Name = "brokerPreviewOrderPane", Dock = DockStyle.Fill, AutoScroll = false, BackColor = Color.Black };
        _vertical.Panel1.Controls.Add(orderPanel);
        var orderContent = new TableLayoutPanel
        {
            Name = "brokerPreviewOrderContent",
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            BackColor = Color.Black,
            Padding = new Padding(4)
        };
        orderContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        orderPanel.Controls.Add(orderContent);
        Add(orderContent, Section("BROKER ORDER — OPEN  ·  Iron Condor  ·  16 Delta / 50-point wings  ·  01 Oct 2026"), 30);
        Add(orderContent, Legs(), 142);
        Add(orderContent, FieldRow("brokerPreviewOrderFields",
            Field("Contracts", "1", 86),
            ChoiceField("Order type", 105, ["Limit", "Market"], out _),
            ChoiceField("Time in force", 115, ["Day", "GTC"], out _),
            ChoiceField("Action", 95, ["Open", "Close"], out _),
            Field("Directed venue", "CME", 100)), 56);
        Add(orderContent, FieldRow("brokerPreviewPriceFields",
            Field("Net bid", "15.25", 90), Field("Net mid", "16.25", 90),
            Field("Net ask", "17.25", 90), LimitField(),
            Field("Combo tick", "0.25", 100)), 56);
        var algorithmField = ChoiceField("IFM algorithm", 125,
            ["None", "IFM Atomic Combo"], out var algorithm);
        var paceField = ChoiceField("Pace", 125,
            ["Patient", "Normal", "Urgent"], out var pace);
        pace.SelectedItem = "Normal";
        pace.Enabled = false;
        algorithm.SelectedIndexChanged += (_, _) =>
            pace.Enabled = algorithm.SelectedItem?.ToString() != "None";
        Add(orderContent, FieldRow("brokerPreviewExecutionFields",
            algorithmField, paceField,
            Field("Broker route", "Atomic combo / BAG", 190),
            Field("SMART", "No", 75), Field("Worst approved limit", "14.00", 150)), 56);
        Add(orderContent, Section("FUND ECONOMICS AND LIMITS  ·  sample estimates, not Risk Manager approval"), 27);
        Add(orderContent, FieldRow("brokerPreviewEconomics",
            Field("Est. credit", "$800", 105), Field("Max profit", "$800", 110),
            Field("Max loss", "$1,700", 105), Field("Buying power", "$1,700", 120),
            Field("Est. fees", "$24", 95), Field("Fund capacity", "$25,000", 120)), 56);
        Add(orderContent, FieldRow("brokerPreviewRiskFields",
            Field("Put width", "50", 90), Field("Call width", "50", 90),
            Field("Net delta", "0.00", 95), Field("Net vega", "−0.12", 95),
            Field("Fund risk limit", "$2,000", 130), Field("Validation", "PREVIEW ONLY", 150)), 56);
        Add(orderContent, FieldRow("brokerPreviewFundLimits",
            Field("Available funds", "$25,000", 120), Field("Reserved capital", "$1,700", 120),
            Field("Risk margin", "$1,700", 115), Field("Max return", "47.1%", 105),
            Field("Minimum profit", "$200", 120), Field("Put spread", "$362.50", 105),
            Field("Call spread", "$400.00", 105)), 56);
        Add(orderContent, FieldRow("brokerPreviewAuthority",
            Field("Value source", "Sample / not authoritative", 210),
            Field("As of", "Illustrative only", 135),
            Field("Route check", "Preview / unqualified", 175),
            Field("Risk approval", "Not requested", 145)), 56);
        _placeOrder = PreviewButton("Place Order");
        _updateLimit = PreviewButton("Update Limit");
        _updateLimit.Text = "Update Order Price";
        _updateLimit.Width = 155;
        _updateOrderPrice = new NumericUpDown
        {
            Name = "brokerPreviewUpdateOrderPrice", Width = 110, Height = 29,
            DecimalPlaces = 2, Increment = trade.TradeType == TradeType.FuturesOutright ? 0.25m : 0.05m,
            Minimum = -1000000, Maximum = 1000000, BackColor = Color.Black,
            ForeColor = Color.White, Margin = new Padding(5, 2, 5, 0), Enabled = false
        };
        _cancelUnfilled = PreviewButton("Cancel Unfilled");
        _placeOrder.Click += (_, _) => PlaceOrderRequested?.Invoke(this, EventArgs.Empty);
        _updateLimit.Click += (_, _) => UpdateLimitRequested?.Invoke(this, EventArgs.Empty);
        _cancelUnfilled.Click += (_, _) => CancelUnfilledRequested?.Invoke(this, EventArgs.Empty);
        var actions = ActionBar("brokerPreviewActions", _placeOrder);
        orderPanel.Controls.Add(actions);
        actions.BringToFront();
        NormalizeBrokerTradeFieldRows(orderContent);
        orderContent.HandleCreated += (_, _) => NormalizeBrokerTradeFieldRows(orderContent);

        var fills = new SplitContainer
        {
            Name = "brokerPreviewOrderTreeSplit",
            Dock = DockStyle.Fill,
            Size = new Size(900, 170),
            Orientation = Orientation.Vertical,
            BackColor = Color.FromArgb(90, 95, 105),
            SplitterWidth = 5,
            Panel1MinSize = 170,
            Panel2MinSize = 190
        };
        void FitOrderTreeSplit()
        {
            var available = fills.Width - fills.SplitterWidth;
            if (available >= fills.Panel1MinSize + fills.Panel2MinSize)
                fills.SplitterDistance = Math.Clamp(
                    (int)(available * 0.50), fills.Panel1MinSize,
                    available - fills.Panel2MinSize);
        }
        fills.HandleCreated += (_, _) => FitOrderTreeSplit();
        fills.SizeChanged += (_, _) => FitOrderTreeSplit();
        var fillsLayout = new TableLayoutPanel
        {
            Name = "brokerPreviewFillsLayout", Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 2, BackColor = Color.Black,
            Margin = Padding.Empty, Padding = Padding.Empty
        };
        fillsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fillsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        fillsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        fills.Margin = Padding.Empty;
        fillsLayout.Controls.Add(fills, 0, 0);
        _vertical.Panel2.Controls.Add(fillsLayout);
        var fillActions = ActionBar("brokerPreviewFillActions", _cancelUnfilled, _updateLimit);
        fillActions.Controls.Add(_updateOrderPrice);
        fillActions.Controls.SetChildIndex(_updateOrderPrice, 1);
        fillActions.Dock = DockStyle.Fill;
        fillActions.Margin = Padding.Empty;
        fillsLayout.Controls.Add(fillActions, 0, 1);
        var treePanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Color.Black };
        treePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        treePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        treePanel.Controls.Add(Section(
            $"ORDERS / FILLS  ·  sample value date {trade.RequestedTradeDate:dd MMM yyyy}"), 0, 0);
        _orderTree = new TreeView
        {
            Name = "brokerPreviewOrderTree",
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Color.White,
            BorderStyle = BorderStyle.None,
            Font = Font,
            HideSelection = false,
            FullRowSelect = true,
            DrawMode = TreeViewDrawMode.OwnerDrawText
        };
        _orderTree.DrawNode += DrawTreeNode;
        _orderTree.AfterSelect += (_, args) => ShowDetail(args.Node);
        treePanel.Controls.Add(_orderTree, 0, 1);
        fills.Panel1.Controls.Add(treePanel);
        var detailPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Color.Black };
        detailPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        detailPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detailPanel.Controls.Add(Section("SELECTED ORDER / FILL DETAILS"), 0, 0);
        _detail = new PropertyGrid
        {
            Name = "brokerPreviewSelectedDetail", Dock = DockStyle.Fill, Margin = Padding.Empty,
            ToolbarVisible = false, HelpVisible = false, PropertySort = PropertySort.NoSort,
            BackColor = Color.Black, ForeColor = Color.White,
            ViewBackColor = Color.Black, ViewForeColor = Color.White,
            CategoryForeColor = Color.White, LineColor = Color.Black
        };
        _detail.HandleCreated += (_, _) => _detail.BeginInvoke((Action)(() =>
        {
            // WinForms exposes no public property for its name/value divider.
            var grid = _detail.Controls.Cast<Control>().FirstOrDefault(x => x.GetType().Name == "PropertyGridView");
            grid?.GetType().GetMethod("MoveSplitterTo",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(grid, [Math.Max(1, grid.ClientSize.Width / 3)]);
        }));
        detailPanel.Controls.Add(_detail, 0, 1);
        fills.Panel2.Controls.Add(detailPanel);
        SeedTree(portfolioId, fund.FundId, order.OrderId, trade.TradeId);
    }

    /// <summary>Displays only the broker trade proposal pane and gives it the complete tab workspace.</summary>
    protected void ShowBrokerTradePane()
    {
        Name = "brokerTradePreview";
        AccessibleName = "Broker Trade development preview";
        _vertical.Panel2Collapsed = true;
    }

    /// <summary>Displays only order and fill evidence and gives it the complete tab workspace.</summary>
    protected void ShowOrderFillsPane()
    {
        Name = "orderFillsPreview";
        AccessibleName = "Order Fills development preview";
        _vertical.Panel1Collapsed = true;
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
        Name = name,
        Text = text,
        ForeColor = color,
        BackColor = Color.Black,
        Dock = DockStyle.Fill,
        AutoEllipsis = true,
        Margin = new Padding(0),
        TextAlign = ContentAlignment.MiddleLeft
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
            Name = name,
            Dock = DockStyle.Fill,
            WrapContents = false,
            AutoScroll = false,
            BackColor = Surface,
            Padding = new Padding(3, 3, 3, 0)
        };
        row.Controls.AddRange(controls);
        foreach (Control control in controls)
            control.MinimumSize = new Size(control.Width, control.MinimumSize.Height);
        row.Resize += (_, _) =>
        {
            var baseline = controls.Sum(control => control.MinimumSize.Width + control.Margin.Horizontal);
            var extra = Math.Max(0, row.ClientSize.Width - row.Padding.Horizontal - baseline)
                / Math.Max(1, controls.Length);
            foreach (Control control in controls)
                control.Width = control.MinimumSize.Width + extra;
        };
        return row;
    }

    private static Panel FieldRow(string name, params BrokerField[] fields)
    {
        var row = new Panel
        {
            Name = name,
            AccessibleName = BrokerFieldRowName,
            Dock = DockStyle.Fill,
            Height = (FieldRowHeight * 2) + FieldSeparatorHeight + 6,
            BackColor = Surface,
            Padding = new Padding(3)
        };
        foreach (var field in fields)
        {
            field.Caption.MinimumSize = new Size(field.Width, 0);
            field.Editor.MinimumSize = new Size(field.Width, 0);
            row.Controls.Add(field.Caption);
        }
        foreach (var field in fields)
            row.Controls.Add(field.Editor);
        row.Resize += (_, _) => LayoutFieldRow(row);
        LayoutFieldRow(row);
        return row;
    }

    private static BrokerField Field(string caption, string value, int width)
    {
        var fieldName = "brokerPreview" + caption.Replace(" ", "");
        return new BrokerField(width,
            Label(fieldName + "Label", caption, Color.Silver),
            Label(fieldName + "Value", value, Color.White));
    }

    private static BrokerField ChoiceField(string caption, int width, string[] options,
        out ComboBox selector)
    {
        var fieldName = "brokerPreview" + caption.Replace(" ", "");
        var label = Label(fieldName + "Label", caption, Color.Silver);
        selector = new ComboBox
        {
            Name = fieldName + "Selector",
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Black,
            ForeColor = Color.White,
            Font = new Font(label.Font.FontFamily, 10F),
            Margin = new Padding(0)
        };
        selector.Items.AddRange(options);
        selector.SelectedIndex = 0;
        selector.DrawItem += DrawChoiceItem;
        return new BrokerField(width, label, selector);
    }

    private static BrokerField LimitField()
    {
        const string fieldName = "brokerPreviewNetLimit";
        var label = Label(fieldName + "Label", "Net limit", Color.Silver);
        var editor = new TextBox
        {
            Name = "brokerPreviewNetLimitTicks",
            Text = "16.00",
            ReadOnly = true,
            Multiline = true,
            BackColor = Color.Black,
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = HorizontalAlignment.Right,
            Font = new Font(label.Font.FontFamily, 10F),
            Margin = new Padding(0)
        };
        return new BrokerField(126, label, editor);
    }

    private static void NormalizeBrokerTradeFieldRows(TableLayoutPanel content)
    {
        var rows = content.Controls.Cast<Control>()
            .OfType<Panel>()
            .Where(row => row.AccessibleName == BrokerFieldRowName)
            .ToArray();
        if (rows.Length == 0)
            return;
        foreach (var row in rows)
        {
            LayoutFieldRow(row);
            var position = content.GetPositionFromControl(row);
            if (position.Row >= 0)
            {
                content.RowStyles[position.Row].SizeType = SizeType.Absolute;
                content.RowStyles[position.Row].Height = row.Height;
            }
        }
    }

    private static void LayoutFieldRow(Panel row)
    {
        var captions = row.Controls.Cast<Control>()
            .Where(control => control.Name.EndsWith("Label", StringComparison.Ordinal))
            .ToArray();
        if (captions.Length == 0)
            return;
        var editors = row.Controls.Cast<Control>()
            .Where(control => !control.Name.EndsWith("Label", StringComparison.Ordinal))
            .ToArray();
        var scale = row.DeviceDpi / 96F;
        var separatorHeight = (int)Math.Ceiling(FieldSeparatorHeight * scale);
        var rowHeight = (int)Math.Ceiling(FieldRowHeight * scale);
        var availableWidth = Math.Max(0, row.ClientSize.Width - row.Padding.Horizontal);
        var gap = (int)Math.Ceiling(8 * scale);
        var widthBudget = Math.Max(0, availableWidth - (gap * (captions.Length - 1)));
        var requestedWidth = captions.Sum(caption => caption.MinimumSize.Width);
        var x = row.Padding.Left;
        for (var index = 0; index < captions.Length; index++)
        {
            var width = index == captions.Length - 1
                ? Math.Max(1, row.ClientSize.Width - row.Padding.Right - x)
                : Math.Max(1, requestedWidth == 0
                    ? widthBudget / captions.Length
                    : (int)Math.Round(widthBudget * (captions[index].MinimumSize.Width / (double)requestedWidth)));
            var caption = captions[index];
            var editor = editors[index];
            caption.Dock = DockStyle.None;
            editor.Dock = DockStyle.None;
            caption.SetBounds(x, row.Padding.Top, width, rowHeight);
            if (editor is ComboBox comboBox)
                comboBox.ItemHeight = Math.Max(1, rowHeight - 6);
            editor.SetBounds(x, row.Padding.Top + rowHeight + separatorHeight, width, rowHeight);
            x += width + gap;
        }
        row.Height = row.Padding.Vertical + (rowHeight * 2) + separatorHeight;
    }

    private sealed record BrokerField(int Width, Label Caption, Control Editor);

    private static void DrawChoiceItem(object? sender, DrawItemEventArgs args)
    {
        if (sender is not ComboBox selector)
            return;
        args.DrawBackground();
        if (args.Index >= 0)
        {
            var text = selector.GetItemText(selector.Items[args.Index]);
            TextRenderer.DrawText(args.Graphics, text, selector.Font, args.Bounds,
                selector.Enabled ? selector.ForeColor : Color.Gray,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
        args.DrawFocusRectangle();
    }

    private static FlowLayoutPanel ActionBar(string name, params Button[] buttons)
    {
        var bar = new FlowLayoutPanel { Name = name, Dock = DockStyle.Bottom, Height = 43,
            FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
            BackColor = Color.Black, Padding = new Padding(4) };
        foreach (var button in buttons)
        {
            button.BackColor = DarkTradingTheme.CommandSurface;
            DarkTradingTheme.Apply(button);
        }
        bar.Controls.AddRange(buttons);
        return bar;
    }

    /// <summary>Only working trades can expose order mutations; completed and new trades cannot.</summary>
    public void SetTradeState(TradeState state, bool readOnly = false)
    {
        var waiting = state is TradeState.OrderSubmitted or TradeState.OrderPlaced or TradeState.OrderPartiallyFilled;
        _updateOrderPrice.Enabled = _updateLimit.Enabled = _cancelUnfilled.Enabled = waiting && !readOnly;
        if (state != TradeState.NewTrade || readOnly) _placeOrder.Enabled = false;
    }

    public void SetPlaceOrderEnabled(bool enabled) => _placeOrder.Enabled = enabled;

    private static Button PreviewButton(string text) => new()
    {
        Name = "brokerPreview" + text.Replace(" ", ""),
        Text = text,
        Width = 130,
        Height = 29,
        Enabled = false,
        BackColor = Color.FromArgb(56, 59, 65),
        ForeColor = Color.Silver,
        Margin = new Padding(5, 2, 5, 0)
    };

    private static DataGridView Legs()
    {
        var grid = new DataGridView
        {
            Name = "brokerPreviewLegGrid",
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = Color.Black,
            GridColor = Color.FromArgb(64, 68, 75),
            BorderStyle = BorderStyle.None,
            ColumnHeadersHeight = 25,
            RowTemplate = { Height = 27 },
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
        if (node.Tag is not string) { _detail.SelectedObject = node.Tag; return; }
        var status = node.Tag?.ToString() ?? "";
        _detail.SelectedObject = new
        {
            Account = _detail.Tag?.ToString() ?? "",
            Order = node.Parent?.Text ?? node.Text,
            Selection = node.Text,
            Status = status,
            Requested = node.Parent is not null ? (int?)null : status == "Filled" ? 3 : status == "Cancelled" ? 1 : 5,
            Filled = node.Parent is not null ? (int?)null : status == "Filled" ? 3 : status == "Cancelled" ? 0 : 2,
            Remaining = node.Parent is not null ? (int?)null : status is "Filled" or "Cancelled" ? 0 : 3,
            LimitPrice = status == "Filled" ? (decimal?)8.25m : status == "Cancelled" ? 14.50m : node.Parent is null ? 16m : null,
            AverageFillPrice = node.Parent is not null ? (decimal?)null : status == "Filled" ? 8.25m : status == "Cancelled" ? null : 15.875m,
            Fees = node.Parent is not null ? (decimal?)null : status == "Filled" ? 18.75m : status == "Cancelled" ? 0m : 12.50m,
            LastUpdate = node.Parent is not null ? "See selected fill" : status == "Filled" ? "09:45:10 ET" : status == "Cancelled" ? "09:39:18 ET" : "10:43:12 ET",
            Venue = "CME",
            Route = "Combo / BAG",
            Action = "Open",
            TimeInForce = "Day",
            DataSource = "Sample preview data"
        };
    }

    private void DrawTreeNode(object? sender, DrawTreeNodeEventArgs args)
    {
        if (args.Node is null) return;
        var bounds = args.Bounds;
        var selected = (args.State & TreeNodeStates.Selected) != 0;
        using var background = new SolidBrush(selected ? Color.FromArgb(52, 65, 85) : _orderTree.BackColor);
        args.Graphics.FillRectangle(background, new Rectangle(bounds.X, bounds.Y, Math.Max(bounds.Width + 18, 1), bounds.Height));
        var text = args.Node.Text.TrimEnd(' ', '●');
        TextRenderer.DrawText(args.Graphics, text, _orderTree.Font, bounds, Color.White,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        if (args.Node.Parent is not null) return;
        var color = args.Node.Tag?.ToString() switch
        {
            "Filled" => Green,
            "Cancelled" => Red,
            _ => Yellow
        };
        var size = TextRenderer.MeasureText(text, _orderTree.Font);
        using var dot = new SolidBrush(color);
        args.Graphics.FillEllipse(dot, bounds.X + size.Width + 3, bounds.Y + (bounds.Height - 10) / 2, 10, 10);
    }
}

/// <summary>Development-only view of the proposed iron-condor broker trade.</summary>
public sealed class BrokerTradePreviewControl : BrokerOrderFillsPreviewControl
{
    private readonly NumericUpDown _quantity = new() { Name = "brokerPreviewContractsValue", Minimum = 1, Maximum = 100000, Increment = 1, DecimalPlaces = 0, Value = 1, BackColor = Color.Black, ForeColor = Color.White };
    private readonly NumericUpDown _legQuantity = new() { Name = "brokerPreviewLegQuantity", Minimum = 1, Maximum = 100000, Increment = 1, DecimalPlaces = 0, Value = 1, BackColor = Color.Black, ForeColor = Color.White };
    private readonly Dictionary<string, int> _quantities = new(StringComparer.Ordinal);
    private IReadOnlyList<BrokerTradeLegData> _legs = [];
    private BrokerTradeInitializationResult? _data;
    private bool _binding;
    private bool _bindingExecution;
    private decimal? _editedOrderPrice;
    public decimal? SelectedOrderPrice => decimal.TryParse(Controls.Find("brokerPreviewNetLimitTicks", true).Single().Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) ? price : null;
    private bool _canEditOrderType;
    public event EventHandler? OrderTypeChanged;
    private ComboBox ExecutionSelector(string caption) => Descendants(this).OfType<ComboBox>().First(x => x.Name == "brokerPreview" + caption.Replace(" ", "") + "Selector");
    public string SelectedAlgorithm => ExecutionSelector("IFM algorithm").SelectedItem?.ToString() ?? "None";
    public string SelectedTimeInForce => ExecutionSelector("Time in force").SelectedItem?.ToString() ?? "Day";
    public string SelectedPace => ExecutionSelector("Pace").SelectedItem?.ToString() ?? "Normal";
    public string SelectedAction => ExecutionSelector("Action").SelectedItem?.ToString() ?? "Open";
    public event EventHandler? ActionChanged;
    public string SelectedOrderType => Descendants(this).OfType<ComboBox>().First(x => x.Name == "brokerPreviewOrdertypeSelector").SelectedItem?.ToString() ?? "Limit";
    private (string OrderType, string Algorithm, string Action, string Route, string Venue, string Tick)? _execution;
    public event EventHandler? QuantitiesChanged;
    public IReadOnlyList<BrokerTradeLegData> SelectedLegs => _legs;
    public int Quantity => decimal.ToInt32(_quantity.Value);
    public IReadOnlyDictionary<string, int> LegQuantities => _quantities;

    /// <summary>Creates a view-only broker trade preview for the selected canonical trade.</summary>
    public BrokerTradePreviewControl(int portfolioId, PortfolioFundEditorModel fund,
        PortfolioFundOrderEditorModel order, PortfolioFundOrderTradeEditorModel trade)
        : base(portfolioId, fund, order, trade)
    {
        ShowBrokerTradePane();
        var shell = (TableLayoutPanel)Controls.Find("brokerOrderFillsPreviewLayout", true).Single();
        var status = shell.GetControlFromPosition(0, 0);
        if (status is not null) { shell.Controls.Remove(status); status.Dispose(); }
        shell.RowStyles[0].SizeType = SizeType.Absolute;
        shell.RowStyles[0].Height = 0;
        var content = (TableLayoutPanel)Controls.Find("brokerPreviewOrderContent", true).Single();
        var heading = content.GetControlFromPosition(0, 0);
        if (heading is not null) { content.Controls.Remove(heading); heading.Dispose(); }
        content.RowStyles[0].SizeType = SizeType.Absolute;
        content.RowStyles[0].Height = 0;
        var orderPrice = Controls.Find("brokerPreviewNetLimitTicks", true).Single();
        TradeOrderInputPalette.ApplyBlackBackgrounds(this, includeButtons: false, excludedControl: orderPrice);
        orderPrice.BackColor = Color.Yellow;
        orderPrice.ForeColor = Color.Black;
        ((TextBox)orderPrice).TextAlign = HorizontalAlignment.Left;
        ((TextBox)orderPrice).ReadOnly = trade.TradeState != TradeState.NewTrade;
        orderPrice.TextChanged += (_, _) =>
        {
            if (!_binding) _editedOrderPrice = SelectedOrderPrice;
        };
        orderPrice.Validated += (_, _) => { if (_editedOrderPrice.HasValue) Render(); };
        AddCaptionColons(this);
        ClearBrokerTradeValues(this);
        StyleBrokerTradeFields(this);
        Controls.Find("brokerPreviewNetLimitLabel", true).Single().Text = "OrderPrice:";
        _canEditOrderType = trade.TradeState == TradeState.NewTrade;
        var orderType = (ComboBox)Controls.Find("brokerPreviewOrdertypeSelector", true).Single();
        orderType.Enabled = _canEditOrderType;
        orderType.SelectedIndexChanged += (_, _) =>
        {
            if (!_bindingExecution && _canEditOrderType) OrderTypeChanged?.Invoke(this, EventArgs.Empty);
        };
        var algorithmSelector = ExecutionSelector("IFM algorithm");
        algorithmSelector.Items.Clear(); algorithmSelector.Items.AddRange(["None", "Adaptive"]);
        algorithmSelector.SelectedItem = "None";
        ExecutionSelector("Time in force").SelectedItem = "Day";
        ExecutionSelector("Pace").SelectedItem = "Normal";
        if (trade.TradeState == TradeState.NewTrade)
        {
            ExecutionSelector("Action").Items.Clear();
            ExecutionSelector("Action").Items.Add("Open");
        }
        ExecutionSelector("Action").SelectedItem = "Open";
        foreach (var caption in new[] { "IFM algorithm", "Time in force", "Pace" })
            ExecutionSelector(caption).SelectedIndexChanged += (_, _) =>
            {
                if (_bindingExecution || !_canEditOrderType) return;
                ExecutionSelector("Pace").Enabled = SelectedAlgorithm != "None";
                OrderTypeChanged?.Invoke(this, EventArgs.Empty);
            };
        ExecutionSelector("Action").SelectedIndexChanged += (_, _) =>
        {
            if (!_bindingExecution && _canEditOrderType) ActionChanged?.Invoke(this, EventArgs.Empty);
        };
        var old = Descendants(this).First(x => x.Name == "brokerPreviewContractsValue");
        var parent = old.Parent!;
        var oldIndex = parent.Controls.GetChildIndex(old);
        _quantity.AutoSize = false;
        _quantity.Font = new Font(old.Font.FontFamily, 10F);
        _quantity.BorderStyle = BorderStyle.FixedSingle;
        _quantity.Bounds = old.Bounds;
        _quantity.Anchor = old.Anchor;
        parent.Controls.Remove(old);
        old.Dispose();
        parent.Controls.Add(_quantity);
        parent.Controls.SetChildIndex(_quantity, oldIndex);
        _quantity.Enabled = trade.TradeState == TomasAI.IFM.Domain.Trade.Shared.TradeState.NewTrade;
        var grid = Descendants(this).OfType<DataGridView>().First(x => x.Name == "brokerPreviewLegGrid");
        grid.Controls.Add(_legQuantity);
        _legQuantity.Enabled = false;
        _legQuantity.Visible = false;
        void PositionLegQuantity()
        {
            var row = grid.CurrentRow;
            var rectangle = row is null ? Rectangle.Empty : grid.GetCellDisplayRectangle(5, row.Index, true);
            _legQuantity.Visible = row is not null && rectangle.Width > 0 && rectangle.Height > 0;
            if (_legQuantity.Visible) { _legQuantity.Bounds = rectangle; _legQuantity.BringToFront(); }
        }
        grid.Scroll += (_, _) => PositionLegQuantity();
        grid.Resize += (_, _) => PositionLegQuantity();
        grid.SelectionChanged += (_, _) =>
        {
            if (_binding) return;
            _binding = true;
            var id = grid.CurrentRow?.Cells[3].Value?.ToString();
            _legQuantity.Enabled = _canEditOrderType && id is not null && _quantities.ContainsKey(id);
            if (_legQuantity.Enabled) _legQuantity.Value = _quantities[id!];
            PositionLegQuantity();
            _binding = false;
        };
        _quantity.ValueChanged += (_, _) =>
        {
            if (_binding) return;
            foreach (var leg in _legs) _quantities[leg.ContractId] = Quantity;
            Render();
            QuantitiesChanged?.Invoke(this, EventArgs.Empty);
        };
        _legQuantity.ValueChanged += (_, _) =>
        {
            if (_binding) return;
            var id = grid.CurrentRow?.Cells[3].Value?.ToString();
            if (id is null) return;
            var quantity = decimal.ToInt32(_legQuantity.Value);
            if (trade.TradeType is TradeType.FuturesOutright or TradeType.ShortIronCondor or TradeType.LongIronCondor
                or TradeType.CallCreditSpread or TradeType.CallDebitSpread
                or TradeType.PutCreditSpread or TradeType.PutDebitSpread)
            {
                _binding = true;
                try
                {
                    _quantity.Value = quantity;
                    foreach (var leg in _legs) _quantities[leg.ContractId] = quantity;
                }
                finally { _binding = false; }
            }
            else _quantities[id] = quantity;
            Render();
            QuantitiesChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    public void SetOrderTypeEditable(bool editable)
    {
        _canEditOrderType = editable;
        _quantity.Enabled = editable;
        _legQuantity.Enabled = editable && _legs.Count > 0;
        ((TextBox)Controls.Find("brokerPreviewNetLimitTicks", true).Single()).ReadOnly = !editable;
        foreach (var caption in new[] { "Order type", "IFM algorithm", "Time in force", "Action", "Pace" })
            ExecutionSelector(caption).Enabled = editable && (caption != "Pace" || SelectedAlgorithm != "None");
    }

    /// <summary>Commits typed spinner text and captures one quantity for the selected strategy.</summary>
    public void CommitDraftEdits()
    {
        ValidateChildren();
        var quantity = _legQuantity.ContainsFocus ? decimal.ToInt32(_legQuantity.Value) : Quantity;
        _binding = true;
        try
        {
            _quantity.Value = quantity;
            foreach (var leg in _legs) _quantities[leg.ContractId] = quantity;
        }
        finally { _binding = false; }
        Render();
    }

    public void BindInitialization(BrokerTradeInitializationResult data)
    {
        _data = data;
        Render();
    }

    public void BindSelectedLegs(IReadOnlyList<BrokerTradeLegData> legs)
    {
        if (!_legs.Select(x => (x.ContractId, x.Sign)).SequenceEqual(legs.Select(x => (x.ContractId, x.Sign))))
            _editedOrderPrice = null;
        _legs = legs;
        foreach (var removed in _quantities.Keys.Except(legs.Select(x => x.ContractId)).ToArray()) _quantities.Remove(removed);
        foreach (var leg in legs) _quantities.TryAdd(leg.ContractId, Quantity);
        Render();
    }

    private void Render()
    {
        _binding = true;
        try
        {
            var grid = Descendants(this).OfType<DataGridView>().First(x => x.Name == "brokerPreviewLegGrid");
            var selectedId = grid.CurrentRow?.Cells[3].Value?.ToString();
            grid.Rows.Clear();
            Descendants(this).OfType<TextBox>().First(x => x.Name == "brokerPreviewNetLimitTicks").Clear();
            foreach (var value in Descendants(this).OfType<Label>().Where(x => x.Name.EndsWith("Value", StringComparison.Ordinal))) value.Text = "Unavailable";
            foreach (var leg in _legs)
            {
                decimal? mid = leg.Bid is { } b && leg.Ask is { } a ? (b + a) / 2 : null;
                var index = grid.Rows.Add(grid.Rows.Count + 1, leg.Sign > 0 ? "BUY" : leg.Sign < 0 ? "SELL" : "", leg.Role,
                    leg.ContractId, leg.Strike, _quantities[leg.ContractId], leg.Bid, leg.Ask, mid, leg.Delta,
                    leg.QuoteAtUtc is { } at ? $"{Math.Max(0, (DateTimeOffset.UtcNow - at).TotalSeconds):N0}s" : "Unavailable", mid);
                grid.Rows[index].DefaultCellStyle.ForeColor = Color.White;
                grid.Rows[index].DefaultCellStyle.BackColor = Color.Black;
                if (leg.ContractId == selectedId) grid.CurrentCell = grid.Rows[index].Cells[0];
            }
            _legQuantity.Enabled = _canEditOrderType && grid.CurrentRow is not null;
            if (grid.CurrentRow?.Cells[3].Value is string id)
            {
                _legQuantity.Value = _quantities[id];
                _legQuantity.Bounds = grid.GetCellDisplayRectangle(5, grid.CurrentRow.Index, true);
                _legQuantity.Visible = _legQuantity.Bounds.Width > 0 && _legQuantity.Bounds.Height > 0;
            }
            else _legQuantity.Visible = false;
            SetField("Value source", _legs.Any(x => x.Frozen) ? "Frozen quotes / stored fund" : "Market Selection / stored fund");
            var asOf = _legs.Where(x => x.QuoteAtUtc.HasValue).Select(x => x.QuoteAtUtc!.Value).ToArray();
            SetField("As of", asOf.Length > 0 ? asOf.Min().ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "Quote timestamp unavailable");
            SetField("Risk approval", _data?.Order?.RiskAuthorization is null ? "Not requested" : "Stored approval - requires revalidation");
            SetField("Available funds", _data?.Balances?.Value?.AvailableCash);
            SetField("Fund capacity", _data?.RiskEnvelope?.AvailableCapital);
            SetField("Fund risk limit", _data?.RiskEnvelope?.MaximumRiskPerTrade);
            SetField("Minimum profit", _data?.Trade?.TradeLimit?.MinProfitTarget);
            SetField("Risk margin", _data?.Trade?.TradeLimit?.RiskMargin);
            SetField("Buying power", "Broker margin assessment required");
            SetField("Est. fees", "Fee schedule unavailable");
            SetField("Worst approved limit", "Not approved for this selection");
            var usage = _data?.CapacityUsage?.Value?.Scopes.Where(x => x.ScopeKind == CapacityScopeKind.Fund && x.ScopeKey == FinancialScopeKeys.Fund(_data!.FundId) && x.Unit == CapacityUnit.Usd && x.Measure == CapacityMeasure.LossCharge).ToArray();
            if (usage is { Length: > 0 }) SetField("Reserved capital", usage.Sum(x => x.Held + x.Working));
            else SetField("Reserved capital", "Unavailable");
            if (_legs.Count == 1 && _legs[0].IsFuture)
            {
                var future = _legs[0];
                var referencePrice = _data?.UnderlyingPrice?.ClosePrice;
                SetField("Value source", "Stored EOD reference - no bid/ask");
                if (_data?.UnderlyingPrice is { } eod) SetField("As of", $"EOD {eod.ValueDate:yyyy-MM-dd}");
                SetField("Validation", "Reference price only - market quote required");
                if (referencePrice is > 0)
                {
                    SetField("Net mid", ((decimal)referencePrice.Value).ToString("N2"));
                    Descendants(this).OfType<TextBox>().First(x => x.Name == "brokerPreviewNetLimitTicks").Text = (_editedOrderPrice ?? future.Sign * (decimal)referencePrice.Value).ToString("0.########", CultureInfo.InvariantCulture);
                    grid.Rows[0].Cells[11].Value = referencePrice.Value;
                }
                // An operator-entered limit remains valid even when no stored reference price exists.
                if (_editedOrderPrice is { } enteredPrice)
                    Descendants(this).OfType<TextBox>().First(x => x.Name == "brokerPreviewNetLimitTicks").Text = enteredPrice.ToString("0.########", CultureInfo.InvariantCulture);
                if (future.Multiplier.HasValue) SetField("Net delta", future.Sign * _quantities[future.ContractId] * future.Multiplier.Value);
                SetField("Net vega", 0m);
                SetField("Max profit", "Price dependent"); SetField("Max loss", "Price dependent");
                return;
            }
            var valid = _legs.Count > 0 && _legs.All(x => x.Sign != 0 && x.Bid.HasValue && x.Ask.HasValue);
            var balanced = _legs.Select(x => _quantities[x.ContractId]).Distinct().Count() <= 1;
            SetField("Validation", !valid ? "Select priced strategy legs" : !balanced ? "Unequal leg quantities - strategy changed" : _data?.Errors.Length > 0 ? "Stored data incomplete" : "Preview - approval required");
            if (!valid) return;
            // Quotes remain per strategy unit; economics and exposures include actual leg quantities.
            var netBid = _legs.Sum(x => x.Sign * (x.Sign > 0 ? x.Bid!.Value : x.Ask!.Value) * _quantities[x.ContractId]) / Quantity;
            var netAsk = _legs.Sum(x => x.Sign * (x.Sign > 0 ? x.Ask!.Value : x.Bid!.Value) * _quantities[x.ContractId]) / Quantity;
            SetField("Net bid", netBid); SetField("Net ask", netAsk); SetField("Net mid", (netBid + netAsk) / 2);
            var limit = Descendants(this).OfType<TextBox>().First(x => x.Name == "brokerPreviewNetLimitTicks");
            var tick = 0.05m;
            limit.Text = (_editedOrderPrice ?? Math.Round((netBid + netAsk) / 2 / tick, MidpointRounding.AwayFromZero) * tick).ToString("0.########", CultureInfo.InvariantCulture);
            if (_legs.All(x => x.Delta.HasValue && x.Multiplier.HasValue)) SetField("Net delta", _legs.Sum(x => x.Sign * x.Delta!.Value * _quantities[x.ContractId] * (double)x.Multiplier!.Value).ToString("0.####"));
            if (_legs.All(x => x.Vega.HasValue && x.Multiplier.HasValue)) SetField("Net vega", _legs.Sum(x => x.Sign * x.Vega!.Value * _quantities[x.ContractId] * (double)x.Multiplier!.Value).ToString("0.####"));
            foreach (var call in new[] { true, false })
            {
                var pair = _legs.Where(x => !x.IsFuture && x.IsCall == call).ToArray();
                if (pair.Length == 2 && pair.All(x => x.Strike.HasValue) && pair.Sum(x => x.Sign) == 0)
                    SetField(call ? "Call width" : "Put width", Math.Abs(pair[0].Strike!.Value - pair[1].Strike!.Value));
                if (pair.Length > 0 && pair.All(x => x.Multiplier.HasValue))
                    SetField(call ? "Call spread" : "Put spread", -pair.Sum(x => x.Sign * (x.Bid!.Value + x.Ask!.Value) / 2 * _quantities[x.ContractId] * x.Multiplier!.Value));
            }
            if (_legs.Any(x => x.IsFuture) || _legs.Any(x => !x.Multiplier.HasValue || !x.Strike.HasValue)) return;
            var cost = balanced && _legs.Select(x => x.Multiplier).Distinct().Count() == 1 && SelectedOrderPrice is { } price
                ? price * Quantity * _legs[0].Multiplier!.Value
                : _legs.Sum(x => x.Sign * (x.Bid!.Value + x.Ask!.Value) / 2 * _quantities[x.ContractId] * x.Multiplier!.Value);
            SetField("Est. credit", -cost);
            var profits = _legs.Select(x => x.Strike!.Value).Append(0m).Distinct().Select(spot =>
                _legs.Sum(x => x.Sign * _quantities[x.ContractId] * x.Multiplier!.Value * Math.Max(x.IsCall ? spot - x.Strike!.Value : x.Strike!.Value - spot, 0m)) - cost).ToArray();
            var slope = _legs.Where(x => x.IsCall).Sum(x => x.Sign * _quantities[x.ContractId] * x.Multiplier!.Value);
            decimal? maxProfit = slope > 0 ? null : profits.Max();
            decimal? maxLoss = slope < 0 ? null : Math.Max(0, -profits.Min());
            SetField("Max profit", maxProfit is null ? "Unlimited" : maxProfit.Value.ToString("N2"));
            SetField("Max loss", maxLoss is null ? "Unlimited" : maxLoss.Value.ToString("N2"));
            SetField("Max return", maxLoss is > 0 && maxProfit.HasValue ? (maxProfit.Value / maxLoss.Value).ToString("P2") : "Unavailable");
        }
        finally
        {
            _binding = false;
            if (_execution is { } e) SetExecutionFields(e.OrderType, e.Algorithm, e.Action, e.Route, e.Venue, e.Tick);
        }
    }

    internal void SetExecutionFields(string orderType, string algorithm, string action, string route, string venue, string tick)
    {
        _bindingExecution = true;
        try
        {
        _execution = (orderType, algorithm, action, route, venue, tick);
        foreach (var pair in new[] { ("Order type", orderType), ("IFM algorithm", algorithm), ("Action", action) })
        {
            var selector = Descendants(this).OfType<ComboBox>().First(x => x.Name == "brokerPreview" + pair.Item1.Replace(" ", "") + "Selector");
            if (!selector.Items.Contains(pair.Item2)) selector.Items.Add(pair.Item2);
            selector.SelectedItem = pair.Item2;
            selector.Enabled = _canEditOrderType;
        }
        SetField("Broker route", route); SetField("Directed venue", venue); SetField("Combo tick", tick);
        SetField("SMART", "Not configured"); SetField("Route check", _data?.BrokerAccount is { } account ? $"{account.QualificationStatus} / {account.Gate}" : "Account qualification unavailable");
        ExecutionSelector("Time in force").Enabled = _canEditOrderType;
        ExecutionSelector("Pace").Enabled = _canEditOrderType && algorithm != "None";
        }
        finally { _bindingExecution = false; }
    }

    private void SetField(string caption, decimal? value) => SetField(caption, value?.ToString("N2") ?? "Unavailable");

    private void SetField(string caption, string value)
    {
        var name = "brokerPreview" + caption.Replace(" ", "") + "Value";
        var control = Descendants(this).FirstOrDefault(x => x.Name == name);
        if (control is not null) control.Text = value;
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void ClearBrokerTradeValues(Control root)
    {
        foreach (var control in Descendants(root))
        {
            if (control is ComboBox combo) combo.SelectedIndex = -1;
            else if (control is TextBoxBase text) text.Clear();
            else if (control is DataGridView grid) grid.Rows.Clear();
            else if (control is TreeView tree) tree.Nodes.Clear();
            else if (control is Label label)
            {
                if (label.Name.EndsWith("Value", StringComparison.Ordinal)
                    || label.Name is "brokerPreviewIdentity" or "brokerPreviewSelectedDetail") label.Text = "";
                else if (label.Name.Length == 0)
                    label.Text = label.Text.StartsWith("BROKER ORDER", StringComparison.Ordinal)
                        ? "Broker Trade:" : "Fund Economics and Limits:";
            }
        }
    }

    private static void StyleBrokerTradeFields(Control root)
    {
        if (root is Button button)
        {
            button.BackColor = DarkTradingTheme.CommandSurface;
            DarkTradingTheme.Apply(button);
        }
        if (root is Panel row && row.AccessibleName == "Broker Trade field row")
        {
            foreach (Control editor in row.Controls)
            {
                if (editor.Name.EndsWith("Label", StringComparison.Ordinal)) continue;
                switch (editor)
                {
                    case Label value: value.BorderStyle = BorderStyle.FixedSingle; break;
                    case TextBoxBase text: text.BorderStyle = BorderStyle.FixedSingle; break;
                    case ComboBox combo: combo.FlatStyle = FlatStyle.Standard; break;
                }
            }
        }
        foreach (Control child in root.Controls) StyleBrokerTradeFields(child);
    }

    private static void AddCaptionColons(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Label label && !string.IsNullOrWhiteSpace(label.Text)
                && (label.Name.EndsWith("Label", StringComparison.Ordinal) || label.Name.Length == 0))
                label.Text = label.Text.TrimEnd().TrimEnd(':') + ":";
            AddCaptionColons(child);
        }
    }
}

/// <summary>Shows broker order/fill evidence and sends permitted working-order mutations.</summary>
public sealed class OrderFillsPreviewControl : BrokerOrderFillsPreviewControl
{
    private readonly IAppRoot? _appRoot;
    private readonly bool _readOnly;
    private readonly System.Windows.Forms.Timer _refresh = new() { Interval = 1000 };
    private TradeOrderId[] _orderIds;
    private readonly Dictionary<string, BrokerOrderDefinition> _orders = new();
    private readonly Label _executionStatus = new()
    {
        Name = "orderExecutionNotificationStatus", Dock = DockStyle.Fill,
        BackColor = Color.Black, ForeColor = Color.White, AutoEllipsis = true,
        TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(5, 0, 5, 0),
        Text = "Waiting for order execution notifications"
    };
    private readonly Dictionary<OrderExecutionId, OrderExecutionDefinition> _executions = new();
    private readonly Dictionary<OrderExecutionId, OrderExecutionChangedEvent> _executionEvents = new();
    private readonly Dictionary<string, BrokerOrderChangedEvent> _brokerEvents = new();
    private IUiEventSubscription? _notifications;
    private bool _loading;
    private bool _mutating;
    private (BrokerOrderId Id, Guid Operation)? _awaitingMutation;

    public OrderFillsPreviewControl(int portfolioId, PortfolioFundEditorModel fund,
        PortfolioFundOrderEditorModel order, PortfolioFundOrderTradeEditorModel trade, bool readOnly = false,
        IAppRoot? appRoot = null)
        : base(portfolioId, fund, order, trade)
    {
        _appRoot = appRoot; _readOnly = readOnly;
        _orderIds = [new(portfolioId, fund.FundId, order.OrderId)];
        ShowOrderFillsPane();
        var fillsLayout = (TableLayoutPanel)Controls.Find("brokerPreviewFillsLayout", true).Single();
        fillsLayout.RowCount = 3;
        fillsLayout.RowStyles.Insert(1, new RowStyle(SizeType.Absolute, 27));
        var fillActions = fillsLayout.GetControlFromPosition(0, 1)!;
        fillsLayout.SetRow(fillActions, 2);
        fillsLayout.Controls.Add(_executionStatus, 0, 1);
        var shell = (TableLayoutPanel)Controls.Find("brokerOrderFillsPreviewLayout", true).Single();
        var status = shell.GetControlFromPosition(0, 0);
        if (status is not null) { shell.Controls.Remove(status); status.Dispose(); }
        shell.RowStyles[0].SizeType = SizeType.Absolute;
        shell.RowStyles[0].Height = 0;
        TradeOrderInputPalette.ApplyBlackBackgrounds(this, includeButtons: true);
        Controls.Find("brokerPreviewOrderTree", true).Single().BackColor = Color.Black;
        Controls.Find("brokerPreviewOrderTreeSplit", true).Single().BackColor = Color.Black;
        Controls.Find("brokerOrderFillsVerticalSplit", true).Single().BackColor = Color.Black;
        SetTradeState(trade.TradeState, readOnly);
        if (_appRoot is null) return;
        _notifications = _appRoot.Services.OrderExecutionNotifications.CreateSubscription(
            value => DispatchNotification(() => ReceiveExecution(value)),
            value => DispatchNotification(() => ReceiveBroker(value)));
        HandleCreated += (_, _) => UiExceptionReporter.Observe(StartNotificationsAsync(), nameof(StartNotificationsAsync), this);
        _orderTree.Nodes.Clear(); _detail.SelectedObject = null;
        SetTradeState(TradeState.NewTrade, true);
        _orderTree.AfterSelect += (_, _) => BindSelectedOrder();
        UpdateLimitRequested += (_, _) => UiExceptionReporter.Observe(MutateAsync(false), nameof(MutateAsync), this);
        CancelUnfilledRequested += (_, _) => UiExceptionReporter.Observe(MutateAsync(true), nameof(MutateAsync), this);
        _refresh.Tick += (_, _) => UiExceptionReporter.Observe(RefreshAsync(), nameof(RefreshAsync), this);
        VisibleChanged += (_, _) =>
        {
            if (Visible) { _refresh.Start(); UiExceptionReporter.Observe(StartNotificationsAsync(), nameof(StartNotificationsAsync), this); UiExceptionReporter.Observe(RefreshAsync(), nameof(RefreshAsync), this); }
            else _refresh.Stop();
        };
    }

    public void BindSubmittedOrders(IReadOnlyList<TradeOrderDefinition> orders)
    {
        _orderIds = orders.Select(x => x.Id).Distinct().ToArray();
        _orders.Clear(); _executions.Clear(); _executionEvents.Clear(); _brokerEvents.Clear();
        _executionStatus.Text = "Order submitted; waiting for execution notifications";
        _executionStatus.ForeColor = Color.Yellow;
        UiExceptionReporter.Observe(RefreshAsync(), nameof(RefreshAsync), this);
    }

    public async Task RefreshAsync()
    {
        if (_appRoot is null || _loading || IsDisposed) return;
        _loading = true;
        try
        {
            var evidence = new List<(BrokerOrderDefinition Order, OrderExecutionDefinition? Execution)>();
            foreach (var id in _orderIds.Where(x => x.IsValid))
            {
                var result = await _appRoot.Services.BrokerOrders.ListAsync(id);
                if (!result.Success) throw new InvalidOperationException(result.ErrorMessage);
                foreach (var order in result.Value ?? [])
                {
                    var execution = await _appRoot.Services.OrderExecutions.GetAsync(id, order.Id.Execution.ExecutionAttemptId);
                    evidence.Add((order, execution.Success ? execution.Value : null));
                }
            }
            if (IsDisposed) return;
            foreach (var (order, execution) in evidence)
            {
                var key = order.Id.Format();
                if (!_orders.TryGetValue(key, out var current) || order.Revision >= current.Revision)
                    _orders[key] = order;
                if (execution is not null)
                {
                    if (!_executionEvents.TryGetValue(execution.Id, out var notification)
                        || execution.Status == notification.OrderExecutionDefinition.Status && execution.Fills.SequenceEqual(notification.OrderExecutionDefinition.Fills)
                        || execution.Fills.Length > notification.OrderExecutionDefinition.Fills.Length
                        || execution.Status is OrderExecutionStatus.Filled or OrderExecutionStatus.Cancelled or OrderExecutionStatus.Rejected
                            && notification.OrderExecutionDefinition.Status is not (OrderExecutionStatus.Filled or OrderExecutionStatus.Cancelled or OrderExecutionStatus.Rejected))
                        _executions[execution.Id] = execution;
                }
            }
            RenderEvidence();
        }
        catch (Exception exception)
        {
            if (!IsDisposed) { SetTradeState(TradeState.NewTrade, true); _detail.SelectedObject = new { Error = exception.Message }; }
            UiExceptionReporter.Report(exception, nameof(OrderFillsPreviewControl), nameof(RefreshAsync), this);
        }
        finally { _loading = false; }
    }

    private void RenderEvidence()
    {
        var selectedName = _orderTree.SelectedNode?.Name;
        var editingId = SelectedOrder()?.Id;
        var editedPrice = _updateOrderPrice.Value;
        _orderTree.BeginUpdate();
        try
        {
            _orderTree.Nodes.Clear();
            foreach (var order in _orders.Values)
            {
                _executions.TryGetValue(order.Id.Execution, out var execution);
                var name = order.Id.Format();
                var node = new TreeNode(OrderTreeCaption(order))
                {
                    Name = name, ForeColor = StatusColor(order, execution),
                    Tag = new { OrderId = name, Status = DisplayStatus(order, execution), BrokerStatus = order.Status, ExecutionStatus = execution?.Status, Price = order.CurrentSignedNetDebitLimit,
                        order.BrokerRevision, order.Order.TimeInForce, order.Order.BrokerAlgorithm, order.Order.AlgorithmPace,
                        Requested = execution?.OrderQuantity, Filled = execution?.CumulativeFilledQuantity,
                        Remaining = execution is null ? (int?)null : Math.Max(0, execution.OrderQuantity - execution.CumulativeFilledQuantity),
                        order.DispatchCategory, order.DispatchDetail, order.ChangedAtUtc }
                };
                foreach (var fill in execution?.Fills.Where(x => x.ComponentId == order.Id.ComponentId) ?? [])
                    node.Nodes.Add(new TreeNode($"{fill.ContractId}  {fill.SignedQuantity} @ {fill.Price}")
                    { Name = fill.ExecutionFillId.ToString(), Tag = fill });
                _orderTree.Nodes.Add(node); node.Expand();
            }
            _orderTree.SelectedNode = _orderTree.Nodes.Cast<TreeNode>().SelectMany(x => new[] { x }.Concat(x.Nodes.Cast<TreeNode>()))
                .FirstOrDefault(x => x.Name == selectedName) ?? _orderTree.Nodes.Cast<TreeNode>().FirstOrDefault();
            BindSelectedOrder();
            if (editingId == SelectedOrder()?.Id)
                _updateOrderPrice.Value = Math.Clamp(editedPrice, _updateOrderPrice.Minimum, _updateOrderPrice.Maximum);
            if (_orderTree.Nodes.Count == 0) _detail.SelectedObject = new { Status = "No broker orders submitted" };
        }
        finally { _orderTree.EndUpdate(); }
    }

    private Task StartNotificationsAsync() => _notifications?.StartAsync().AsTask() ?? Task.CompletedTask;

    public async ValueTask StopNotificationsAsync()
    {
        _refresh.Stop();
        if (_notifications is not null) await _notifications.StopAsync();
    }

    private void DispatchNotification(Action apply)
    {
        if (IsDisposed || Disposing || !IsHandleCreated) return;
        try
        {
            BeginInvoke((Action)(() => { if (!IsDisposed && !Disposing) apply(); }));
        }
        catch (InvalidOperationException) when (IsDisposed || Disposing || !IsHandleCreated) { }
    }

    private void ReceiveExecution(OrderExecutionChangedEvent value)
    {
        if (!_orderIds.Contains(value.OrderExecutionDefinition.TradeOrderId)) return;
        if (_executionEvents.TryGetValue(value.EntityId, out var previous)
            && (value.EventId > 0 && previous.EventId > 0 ? value.EventId <= previous.EventId : value.ReceivedOn <= previous.ReceivedOn)) return;
        _executionEvents[value.EntityId] = value;
        _executions[value.EntityId] = value.OrderExecutionDefinition;
        RenderEvidence();
    }

    private void ReceiveBroker(BrokerOrderChangedEvent value)
    {
        if (!_orderIds.Contains(value.EntityId.Execution.TradeOrder)) return;
        var key = value.EntityId.Format();
        if (_orders.TryGetValue(key, out var previous) && value.BrokerOrderDefinition.Revision < previous.Revision) return;
        _brokerEvents[key] = value;
        _orders[key] = value.BrokerOrderDefinition;
        RenderEvidence();
    }

    private static string DisplayStatus(BrokerOrderDefinition order, OrderExecutionDefinition? execution) =>
        execution?.Status is OrderExecutionStatus.Filled or OrderExecutionStatus.Cancelled or OrderExecutionStatus.Rejected
            ? execution.Status.ToString() : order.Status.ToString();

    private static Color StatusColor(BrokerOrderDefinition order, OrderExecutionDefinition? execution) =>
        execution?.Status == OrderExecutionStatus.Filled || order.Status == BrokerOrderStatus.Filled ? Color.LimeGreen :
        execution?.Status is OrderExecutionStatus.Cancelled or OrderExecutionStatus.Rejected || order.Status is BrokerOrderStatus.Cancelled or BrokerOrderStatus.Rejected ? Color.Red :
        order.Status is BrokerOrderStatus.Working or BrokerOrderStatus.PartiallyFilled or BrokerOrderStatus.UpdatePending or BrokerOrderStatus.CancelPending ? Color.Yellow : Color.White;

    private void UpdateStatusLine(BrokerOrderDefinition? order)
    {
        if (order is null) return;
        _executions.TryGetValue(order.Id.Execution, out var execution);
        _executionEvents.TryGetValue(order.Id.Execution, out var executionEvent);
        _brokerEvents.TryGetValue(order.Id.Format(), out var brokerEvent);
        var latest = executionEvent is not null
            ? $"{executionEvent.Subject.Verb} #{executionEvent.EventId} received {executionEvent.ReceivedOn:HH:mm:ss.fff} UTC"
            : brokerEvent is not null ? $"{brokerEvent.Subject.Verb} #{brokerEvent.EventId} received {brokerEvent.ReceivedOn:HH:mm:ss.fff} UTC" : "Stored order state; waiting for notifications";
        _executionStatus.Text = $"{DisplayStatus(order, execution)} | Execution: {execution?.Status.ToString() ?? "Pending"} | Filled {execution?.CumulativeFilledQuantity ?? 0}/{execution?.OrderQuantity ?? 0} | Price {order.CurrentSignedNetDebitLimit:0.########} | {latest}";
        _executionStatus.ForeColor = StatusColor(order, execution);
    }

    private static string OrderTreeCaption(BrokerOrderDefinition order)
    {
        var component = order.Order.Components.Single(x => x.ComponentId == order.Id.ComponentId);
        string Leg(TradeLegDefinition leg) => leg.AssetFamily == TradeAssetFamily.FuturesOption
            ? leg.Strike?.ToString("0.########", CultureInfo.InvariantCulture) ?? ""
            : $"{(leg.SignedQuantity > 0 ? "buy" : "sell")} {leg.ContractId}";
        var calls = component.Legs.Where(x => x.PutCall == 1).OrderByDescending(x => x.SignedQuantity).Select(Leg);
        var puts = component.Legs.Where(x => x.PutCall == 2).OrderBy(x => x.SignedQuantity).Select(Leg);
        var groups = new[] { string.Join("-", calls), string.Join("-", puts) }.Where(x => x.Length > 0);
        var legs = component.Legs.All(x => x.AssetFamily == TradeAssetFamily.FuturesOption)
            ? string.Join(":", groups) : string.Join("-", component.Legs.Select(Leg));
        var type = component.StrategyKind switch
        {
            TradeStrategyKind.IronCondor => component.Legs.Any(x => x.PutCall == 1 && x.SignedQuantity > 0 && x.Strike == component.Legs.Where(y => y.PutCall == 1).Max(y => y.Strike))
                ? "ShortIronCondor" : "LongIronCondor",
            TradeStrategyKind.VerticalSpread => "Vertical Spread",
            TradeStrategyKind.FuturesOutright => "Futures",
            _ => component.StrategyKind.ToString()
        };
        return $"{type} = {legs} @ {order.CurrentSignedNetDebitLimit.ToString("0.########", CultureInfo.InvariantCulture)}";
    }

    private BrokerOrderDefinition? SelectedOrder()
    {
        var node = _orderTree.SelectedNode;
        if (node?.Parent is not null) node = node.Parent;
        return node is not null && _orders.TryGetValue(node.Name, out var order) ? order : null;
    }

    private void BindSelectedOrder()
    {
        var order = SelectedOrder();
        var selectedOwner = order ?? (_orderTree.SelectedNode?.Parent is { } parent && _orders.TryGetValue(parent.Name, out var owner) ? owner : null);
        UpdateStatusLine(selectedOwner);
        if (order is not null && _awaitingMutation is { } pending && pending.Id == order.Id && order.OperationId == pending.Operation)
            _awaitingMutation = null;
        var awaiting = order is not null && _awaitingMutation?.Id == order.Id;
        var terminalExecution = order is not null && _executions.TryGetValue(order.Id.Execution, out var execution)
            && execution.Status is OrderExecutionStatus.Filled or OrderExecutionStatus.Cancelled or OrderExecutionStatus.Rejected;
        var working = !terminalExecution && order?.Status is BrokerOrderStatus.Working or BrokerOrderStatus.PartiallyFilled;
        SetTradeState(working ? TradeState.OrderPlaced : TradeState.NewTrade, _readOnly || _mutating || awaiting || !working);
        if (order is null) return;
        var component = order.Order.Components.Single(x => x.ComponentId == order.Id.ComponentId);
        if (component.TickIncrement is not > 0 || component.MinimumSignedNetDebitLimit is null || component.MaximumSignedNetDebitLimit is null)
        { SetTradeState(TradeState.NewTrade, true); return; }
        _updateOrderPrice.Increment = component.TickIncrement.Value;
        _updateOrderPrice.DecimalPlaces = Math.Min(8, (decimal.GetBits(component.TickIncrement.Value)[3] >> 16) & 0xff);
        _updateOrderPrice.Minimum = -1000000m; _updateOrderPrice.Maximum = 1000000m;
        _updateOrderPrice.Value = Math.Clamp(order.CurrentSignedNetDebitLimit, _updateOrderPrice.Minimum, _updateOrderPrice.Maximum);
        _updateOrderPrice.Minimum = component.MinimumSignedNetDebitLimit.Value;
        _updateOrderPrice.Maximum = component.MaximumSignedNetDebitLimit.Value;
    }

    private async Task MutateAsync(bool cancel)
    {
        if (_appRoot is null || _readOnly || _mutating || SelectedOrder() is not { } order) return;
        var price = UpdateOrderPrice;
        _mutating = true; BindSelectedOrder();
        try
        {
            var operation = Guid.NewGuid();
            _awaitingMutation = (order.Id, operation);
            var result = cancel
                ? await _appRoot.Services.BrokerOrderCommands.CancelAsync(order.Id, operation)
                : await _appRoot.Services.BrokerOrderCommands.UpdatePriceAsync(order.Id, price, operation);
            if (!result.Success) throw new InvalidOperationException($"Broker order request failed ({result.ErrorCode}): {result.ErrorMessage}");
            await RefreshAsync();
        }
        catch (Exception exception) { _awaitingMutation = null; MessageBox.Show(this, exception.Message, "Broker order", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { _mutating = false; BindSelectedOrder(); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refresh.Stop(); _refresh.Dispose();
            if (_notifications is not null)
                UiExceptionReporter.Observe(_notifications.DisposeAsync().AsTask(), "StopOrderExecutionNotifications", this);
        }
        base.Dispose(disposing);
    }
}
