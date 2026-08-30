using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;

namespace TomasAI.IFM.UI.Net.Views.Portfolio;

/// <summary>Read-only planned-composition explorer. It never reads or labels legacy TradeDb state as Portfolio state.</summary>
public sealed class PortfolioCompositionForm : Form
{
    readonly ComboBox _fund = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260, AccessibleName = "Fund selector", BackColor = Color.FromArgb(64, 64, 64), ForeColor = Color.White };
    readonly DateTimePicker _month = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM", ShowUpDown = true, Width = 100, AccessibleName = "Order month" };
    readonly TextBox _identity = new() { Width = 130, AccessibleName = "Integer Order or Trade ID", BackColor = Color.Black, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
    readonly Button _findOrder = PortfolioUiStyle.Button("Find OrderId", "Find planned Order by integer ID");
    readonly Button _findTrade = PortfolioUiStyle.Button("Find TradeId", "Find planned Trade by integer ID");
    readonly Button _refresh = PortfolioUiStyle.Button("Refresh", "Refresh planned compositions");
    readonly Button _close = PortfolioUiStyle.Button("Close", "Close planned compositions");
    readonly DataGridView _orders = PortfolioUiStyle.Grid("Planned FundOrders");
    readonly DataGridView _trades = PortfolioUiStyle.Grid("Planned FundOrderTrades");
    readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 30, ForeColor = Color.White, BackColor = Color.Black };
    readonly IPortfolioQueryApi _queries;
    readonly PortfolioReadModel _portfolio;
    readonly PortfolioCompositionViewModel _viewModel;
    FundMandateReadModel[] _funds = [];

    public PortfolioCompositionForm(IPortfolioQueryApi queries, PortfolioReadModel portfolio)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _portfolio = portfolio ?? throw new ArgumentNullException(nameof(portfolio));
        _viewModel = new(queries);
        _viewModel.SelectPortfolio(portfolio);
        Text = $"Portfolio {portfolio.PortfolioId} - Planned Compositions";
        AccessibleName = "Portfolio planned compositions";
        Width = 1300; Height = 760; PortfolioUiStyle.Apply(this);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(6), BackColor = Color.Black };
        toolbar.Controls.AddRange([
            Caption("Fund"), _fund, Caption("Month"), _month, Caption("ID"), _identity, _findOrder, _findTrade, _refresh, _close,
        ]);
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 330 };
        split.Panel1.Controls.Add(_orders); split.Panel2.Controls.Add(_trades);
        Controls.Add(split); Controls.Add(toolbar); Controls.Add(_status);
        _status.Text = _viewModel.Semantics;
        Shown += async (_, _) => await LoadFundsAsync();
        _fund.SelectedIndexChanged += async (_, _) => await LoadOrdersAsync();
        _orders.SelectionChanged += async (_, _) => await SelectOrderAsync();
        _findOrder.Click += async (_, _) => await FindAsync(isTrade: false);
        _findTrade.Click += async (_, _) => await FindAsync(isTrade: true);
        _refresh.Click += async (_, _) => await LoadOrdersAsync();
        _close.Click += (_, _) => Close();
    }

    async Task LoadFundsAsync()
    {
        var result = await _queries.GetFundsAsync(_portfolio.PortfolioId, null, 200);
        _funds = result.Success ? result.Value?.Items ?? [] : [];
        _fund.DataSource = _funds;
        _fund.DisplayMember = nameof(FundMandateReadModel.FundCode);
        _status.Text = _funds.Length == 0 ? "No Funds are configured for this Portfolio." : _viewModel.Semantics;
    }

    async Task LoadOrdersAsync()
    {
        if (_fund.SelectedItem is not FundMandateReadModel fund) return;
        await _viewModel.SelectFundAsync(fund, new DateOnly(_month.Value.Year, _month.Value.Month, 1));
        _orders.DataSource = _viewModel.Orders;
        _trades.DataSource = Array.Empty<FundOrderTradeProjectionReadModel>();
        _status.Text = $"{_viewModel.Orders.Length} planned composition(s). {_viewModel.Semantics}";
    }

    async Task SelectOrderAsync()
    {
        if (_orders.CurrentRow?.DataBoundItem is not FundOrderProjectionReadModel order) return;
        await _viewModel.SearchOrderAsync(order.OrderId);
        _trades.DataSource = _viewModel.Trades;
        _status.Text = $"OrderId {order.OrderId}: {order.Status}. {_viewModel.Semantics}";
    }

    async Task FindAsync(bool isTrade)
    {
        if (!int.TryParse(_identity.Text, out var id) || id <= 0)
        {
            _status.Text = "Enter a positive integer OrderId or TradeId.";
            return;
        }
        var found = isTrade ? await _viewModel.SearchTradeAsync(id) : await _viewModel.SearchOrderAsync(id);
        _orders.DataSource = _viewModel.SelectedOrder is null ? Array.Empty<FundOrderProjectionReadModel>() : new[] { _viewModel.SelectedOrder };
        _trades.DataSource = _viewModel.Trades;
        _status.Text = found ? $"Found {(isTrade ? "TradeId" : "OrderId")} {id}. {_viewModel.Semantics}" : $"ID {id} was not found in the selected Portfolio/Fund scope.";
    }

    static Label Caption(string text) => new() { Text = text, AutoSize = true, Padding = new Padding(0, 11, 0, 0), ForeColor = Color.White };
}
