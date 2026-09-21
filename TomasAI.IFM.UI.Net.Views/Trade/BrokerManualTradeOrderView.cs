using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Net.Views.Presentation;

namespace TomasAI.IFM.UI.Net.Views.Trade;

/// <summary>Dark-theme manual order editor for Futures outright and Vertical Spread emulator orders.</summary>
public sealed class BrokerManualTradeOrderView : DarkTradingView, ITradeOrderControl, ITradeExecutionSelectionControl, IFormControl
{
    readonly BrokerManualTradeOrderViewModel _viewModel;
    readonly NumericUpDown _quantity = new() { Name = "quantity", Minimum = 1, Maximum = 100000, Value = 1 };
    readonly NumericUpDown _limit = new()
    {
        Name = "signedNetDebitLimit", Minimum = -1000000, Maximum = 1000000,
        DecimalPlaces = 2, Increment = 0.05m
    };
    readonly Label _account = ValueLabel("Loading emulator account...");
    readonly Label _gate = ValueLabel("Unknown");
    readonly Label _approval = ValueLabel("Not accepted");
    readonly Button _qualification = new()
    {
        Name = "manageBrokerQualification",
        Text = "Manage qualification...",
        AutoSize = true,
        BackColor = Color.FromArgb(45, 45, 48),
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat
    };

    /// <summary>Creates the strategy-specific manual broker editor.</summary>
    public BrokerManualTradeOrderView(BrokerManualTradeOrderViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        Name = viewModel.StrategyKind == TradeStrategyKind.FuturesOutright
            ? "FuturesTradeOrderView"
            : "VerticalSpreadTradeOrderView";
        Dock = DockStyle.Fill;
        BackColor = Color.Black;
        ForeColor = Color.White;
        Font = new Font("Microsoft Sans Serif", 10F);
        Padding = new Padding(12);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = Color.Black,
            ColumnCount = 2,
            RowCount = 0,
            Padding = new Padding(8)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Add(layout, "Strategy", ValueLabel(viewModel.StrategyKind.ToString()));
        Add(layout, "Fund / Order / Trade",
            ValueLabel($"{viewModel.Trade.FundId} / {viewModel.Trade.OrderId} / {viewModel.Trade.TradeId}"));
        Add(layout, "Contracts", ValueLabel(string.Join(Environment.NewLine, viewModel.ContractIds)));
        Add(layout, "Reference", ValueLabel(viewModel.Trade.Reference));
        Add(layout, "Quantity", _quantity);
        Add(layout, "Signed net debit / credit limit", _limit);
        Add(layout, "Broker environment", ValueLabel("Emulator"));
        Add(layout, "Broker account", _account);
        Add(layout, "New-risk gate", _gate);
        Add(layout, "Qualification approval", _approval);
        Add(layout, "Account controls", _qualification);
        _qualification.Click += ManageQualificationClicked;
        Controls.Add(layout);
        AccessibleName = $"{viewModel.StrategyKind} emulator trade order editor";
        _ = RefreshAccountAsync();
    }

    /// <inheritdoc />
    public DateOnly MaturityDate => _viewModel.Trade.MaturityDate;

    /// <inheritdoc />
    public Task RemoveTradeAsync(int fundId, int orderId, int tradeId) => _viewModel.RemoveAsync();

    /// <inheritdoc />
    public Task<Guid> SubmitOrderAsync(DateOnly tradeDate, OrderActionType orderAction,
        ITradeOrderConfirmationService tradeOrderConfirmation)
    {
        if (orderAction != OrderActionType.Open)
            throw new InvalidOperationException(
                "This manual editor creates opening orders. Position workflows create closing orders.");
        return _viewModel.SubmitAsync(decimal.ToInt32(_quantity.Value), _limit.Value,
            tradeOrderConfirmation);
    }

    /// <inheritdoc />
    public Task SetLiveFeedAsync(bool enabled) => Task.CompletedTask;

    /// <inheritdoc />
    public void SetNearestStrikePrices() { }

    /// <inheritdoc />
    public Task OrderActionTypeChangedAsync(OrderActionType orderActionType)
    {
        Enabled = orderActionType == OrderActionType.Open;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void SetExecutionSelection(BrokerOrderType orderType, BrokerAlgorithm algorithm)
        => _viewModel.SetExecutionSelection(orderType, algorithm);

    /// <summary>Starts the view.</summary>
    public void Open() => _ = RefreshAccountAsync();

    /// <summary>Closes the view.</summary>
    public void Close() { }

    /// <inheritdoc />
    void IFormControl.Resize(Control parentControl) => Bounds = parentControl.ClientRectangle;

    async Task RefreshAccountAsync()
    {
        try
        {
            var result = await _viewModel.GetAccountAsync().ConfigureAwait(true);
            if (IsDisposed) return;
            if (!result.Success || result.Value is null)
            {
                _account.Text = "Unavailable";
                _gate.Text = $"Closed ({result.ErrorCode}: {result.ErrorMessage})";
                _approval.Text = "Not accepted";
                return;
            }
            _account.Text = $"{result.Value.Id.AccountAlias} / {result.Value.Environment}";
            _gate.Text = result.Value.Gate.ToString();
            _gate.ForeColor = result.Value.Gate == BrokerAccountOperationalGate.Open
                ? Color.LightGreen
                : Color.Orange;
            _approval.Text = result.Value.ApprovalId == Guid.Empty
                ? $"{result.Value.QualificationStatus}"
                : $"{result.Value.QualificationStatus} / {result.Value.ApprovalId:N}";
        }
        catch (Exception exception)
        {
            if (IsDisposed) return;
            _account.Text = "Unavailable";
            _gate.Text = "Closed";
            _approval.Text = exception.Message;
        }
    }

    async void ManageQualificationClicked(object? sender, EventArgs eventArgs)
    {
        using var dialog = new BrokerAccountQualificationDialog(_viewModel);
        dialog.ShowDialog(this);
        await RefreshAccountAsync().ConfigureAwait(true);
    }

    static Label ValueLabel(string value) => new()
    {
        AutoSize = true,
        MaximumSize = new Size(900, 0),
        BackColor = Color.Black,
        ForeColor = Color.White,
        Text = value,
        Padding = new Padding(4)
    };

    static void Add(TableLayoutPanel panel, string name, Control value)
    {
        var row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            BackColor = Color.Black,
            ForeColor = Color.Silver,
            Text = name,
            Padding = new Padding(4)
        }, 0, row);
        value.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        value.BackColor = Color.Black;
        value.ForeColor = Color.White;
        panel.Controls.Add(value, 1, row);
    }
}
