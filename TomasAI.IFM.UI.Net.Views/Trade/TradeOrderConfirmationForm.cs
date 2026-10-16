using TomasAI.IFM.UI.Net.ViewModels.Trade;

namespace TomasAI.IFM.UI.Net.Views.Trade;

/// <summary>WinForms adapter for framework-neutral trade-order confirmation state.</summary>
public partial class TradeOrderConfirmationForm : DarkTradingForm
{
    readonly TradeOrderConfirmationViewModel _viewModel;
    DataGridView? _legGrid;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<bool>? ConfirmRequested { get; set; }
    bool submitting;
    public void CompleteSubmission() { submitting=false; Close(); }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if(submitting && e.CloseReason == CloseReason.UserClosing) e.Cancel=true;
        base.OnFormClosing(e);
    }

    public TradeOrderConfirmationForm(TradeOrderConfirmationViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        ConfigureLegLayout();
        btnContinue.DialogResult = DialogResult.None;
    }

    void ConfigureLegLayout()
    {
        ConfigureSummaryLayout();
        if (_viewModel.Legs.Count == 0) return;
        SuspendLayout();
        Controls.Clear();
        ClientSize = new Size(620, 405);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
            Padding = new Padding(8) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28 + _viewModel.Legs.Count * 27));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        _legGrid = new DataGridView { Name = "OrderLegsGrid", Dock = DockStyle.Fill,
            ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, RowHeadersVisible = false, MultiSelect = false,
            AutoGenerateColumns = false, BackgroundColor = BackColor,
            BorderStyle = BorderStyle.FixedSingle, EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 26, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
        _legGrid.DefaultCellStyle.BackColor = BackColor;
        _legGrid.DefaultCellStyle.ForeColor = Color.White;
        _legGrid.ColumnHeadersDefaultCellStyle.BackColor = BackColor;
        _legGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _legGrid.RowTemplate.Height = 27;
        _legGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractId", HeaderText = "Contract ID",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _legGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Role", HeaderText = "Role", Width = 125 });
        _legGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Side", HeaderText = "Side", Width = 65 });
        _legGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "LegCommission", HeaderText = "Leg Commission", Width = 135 });
        foreach (var leg in _viewModel.Legs)
            _legGrid.Rows.Add(leg.ContractId, leg.Role, leg.Side, $"USD {leg.LegCommission:F2}");
        foreach (DataGridViewColumn column in _legGrid.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
        txtName.Dock = txtAction.Dock = pnlTradeOrderConfirmation.Dock = DockStyle.Fill;
        pnlTradeOrderConfirmation.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, 170);
        pnlTradeOrderConfirmation.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 100);
        foreach (RowStyle row in pnlTradeOrderConfirmation.RowStyles) { row.SizeType = SizeType.Percent; row.Height = 100f / 6; }
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(btnCancel);
        buttons.Controls.Add(btnContinue);
        layout.Controls.Add(txtName, 0, 0);
        layout.Controls.Add(_legGrid, 0, 1);
        layout.Controls.Add(txtAction, 0, 2);
        layout.Controls.Add(pnlTradeOrderConfirmation, 0, 3);
        layout.Controls.Add(buttons, 0, 4);
        Controls.Add(layout);
        ResumeLayout(true);
    }

    void ConfigureSummaryLayout()
    {
        lblCommission.Text = "Total Commission:";
        lblTradeFillType.Text = "Broker Type:";
        pnlTradeOrderConfirmation.CellBorderStyle = TableLayoutPanelCellBorderStyle.None;
        pnlTradeOrderConfirmation.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, 170);
        pnlTradeOrderConfirmation.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 100);
        Label[] labels = [label1, lblOrderPrice, lblOrderAmount, lblCommission, lblTotalAmount, lblTradeFillType];
        Control[] values = [txtOrderType, txtOrderPrice, txtOrderAmount, txtCommission, txtTotalAmount, ddlTradeFillType];
        for (var row = 0; row < labels.Length; row++)
        {
            var label = labels[row];
            pnlTradeOrderConfirmation.Controls.Remove(pnlTradeOrderConfirmation.GetControlFromPosition(0, row));
            pnlTradeOrderConfirmation.Controls.Add(label, 0, row);
            label.Text = label.Text.TrimEnd(':') + ":";
            label.AutoSize = false;
            label.Dock = DockStyle.Fill;
            label.Margin = new Padding(2);
            label.Padding = new Padding(5, 0, 0, 0);
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = Color.LightGray;
            label.BorderStyle = BorderStyle.FixedSingle;
            var value = values[row];
            value.Dock = DockStyle.None;
            value.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            value.Margin = new Padding(2);
            if (value is TextBox textBox) textBox.BorderStyle = BorderStyle.FixedSingle;
        }
    }

    void TradeOrderConfirmationForm_Load(object sender, EventArgs e)
    {
        txtName.Text = $"{_viewModel.TradeOrder.TradeType}";
        txtDescription.Text = _viewModel.TradeOrder.OrderDescription;
        txtAction.Text = $"{_viewModel.TradeOrder.OrderAction} {_viewModel.TradeOrder.OrderQuantity}";
        txtOrderPrice.Text = $"{_viewModel.TradeOrder.OrderPrice:F2}";
        txtOrderType.Text = $"{_viewModel.TradeOrder.OrderType}";
        txtOrderAmount.Text = $"{_viewModel.TradeOrder.OrderAmount:C}";
        txtCommission.Text = $"{_viewModel.TradeOrder.Commission:C}";
        txtTotalAmount.Text = $"{_viewModel.TradeOrder.TotalAmount:C}";
        if (_viewModel.Legs.Count > 0)
        {
            txtOrderAmount.Text = $"USD {_viewModel.TradeOrder.OrderAmount:F2}";
            txtCommission.Text = $"USD {_viewModel.TradeOrder.Commission:F2}";
            txtTotalAmount.Text = $"USD {_viewModel.TradeOrder.TotalAmount:F2}";
        }
        ddlTradeFillType.Items.Clear();
        foreach (var tradeFillType in _viewModel.TradeFillTypes)
            ddlTradeFillType.Items.Add($"{tradeFillType}");
        ddlTradeFillType.SelectedIndex = _viewModel.TradeFillTypes
            .ToList()
            .IndexOf(_viewModel.SelectedTradeFillType);
        btnContinue.Enabled = _viewModel.CanConfirm;
        btnCancel.Select();
    }

    void btnContinue_Click(object sender, EventArgs e)
    {
        if(submitting) return;
        if(ConfirmRequested is null) { DialogResult=DialogResult.OK; Close(); return; }
        submitting=true;
        btnContinue.Enabled=btnCancel.Enabled=ddlTradeFillType.Enabled=false;
        btnContinue.Text="Submitting?";
        ConfirmRequested();
    }
    void btnCancel_Click(object sender, EventArgs e) => Close();

    void ddlTradeFillType_SelectedIndexChanged(object sender, EventArgs e)
    {
        _viewModel.SelectTradeFillType(ddlTradeFillType.SelectedIndex);
        btnContinue.Enabled = _viewModel.CanConfirm;
    }
}
