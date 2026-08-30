using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.Portfolio;

/// <summary>Portfolio -> Fund -> planned-composition navigation, intentionally separate from legacy TradeDb blotters.</summary>
public sealed class PortfolioCompositionViewModel(IPortfolioQueryApi queries) : ObservableObject
{
    readonly IPortfolioQueryApi _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    PortfolioReadModel? _portfolio;
    FundMandateReadModel? _fund;
    FundOrderProjectionReadModel[] _orders = [];
    FundOrderProjectionReadModel? _selectedOrder;
    FundOrderTradeProjectionReadModel[] _trades = [];

    public PortfolioReadModel? Portfolio { get => _portfolio; private set => SetProperty(ref _portfolio, value); }
    public FundMandateReadModel? Fund { get => _fund; private set => SetProperty(ref _fund, value); }
    public FundOrderProjectionReadModel[] Orders { get => _orders; private set => SetProperty(ref _orders, value); }
    public FundOrderProjectionReadModel? SelectedOrder { get => _selectedOrder; private set => SetProperty(ref _selectedOrder, value); }
    public FundOrderTradeProjectionReadModel[] Trades { get => _trades; private set => SetProperty(ref _trades, value); }
    public string Semantics => "Planned composition only; not a broker order, fill, live trade, or position.";

    public void SelectPortfolio(PortfolioReadModel portfolio)
    {
        Portfolio = portfolio ?? throw new ArgumentNullException(nameof(portfolio));
        Fund = null; Orders = []; SelectedOrder = null; Trades = [];
    }

    public async Task SelectFundAsync(FundMandateReadModel fund, DateOnly month, CancellationToken cancellationToken = default)
    {
        if (Portfolio is null || fund.PortfolioId != Portfolio.PortfolioId) throw new InvalidOperationException("Fund must belong to the selected Portfolio.");
        Fund = fund; SelectedOrder = null; Trades = [];
        var result = await _queries.GetOrdersAsync(fund.PortfolioId, fund.FundId, month, 200, cancellationToken: cancellationToken).ConfigureAwait(false);
        Orders = result.Success ? result.Value?.Items ?? [] : [];
    }

    public async Task<bool> SearchOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        if (orderId <= 0) return false;
        var result = await _queries.GetOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Value is null || Portfolio is null || result.Value.PortfolioId != Portfolio.PortfolioId || Fund is not null && result.Value.FundId != Fund.FundId) return false;
        SelectedOrder = result.Value;
        var trades = await _queries.GetOrderTradesAsync(orderId, 200, cancellationToken: cancellationToken).ConfigureAwait(false);
        Trades = trades.Success ? trades.Value?.Items ?? [] : [];
        return true;
    }

    public async Task<bool> SearchTradeAsync(int tradeId, CancellationToken cancellationToken = default)
    {
        if (tradeId <= 0) return false;
        var result = await _queries.GetTradeAsync(tradeId, cancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Value is null || Portfolio is null || result.Value.PortfolioId != Portfolio.PortfolioId || Fund is not null && result.Value.FundId != Fund.FundId) return false;
        return await SearchOrderAsync(result.Value.OrderId, cancellationToken).ConfigureAwait(false);
    }
}
