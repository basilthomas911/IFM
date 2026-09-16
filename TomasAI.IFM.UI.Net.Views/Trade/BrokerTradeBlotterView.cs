using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.Contracts;

namespace TomasAI.IFM.UI.Net.Views.Trade;

/// <summary>Read-only strategy-specific blotter shell for Futures and Vertical Spread trades.</summary>
public sealed class BrokerTradeBlotterView : UserControl, IFormControl
{
    private readonly string[] _header;
    private readonly BrokerExecutionEvidenceControl _evidence;

    /// <summary>Creates a dark read-only blotter for the selected trade identity.</summary>
    public BrokerTradeBlotterView(IAppRoot appRoot, FundReadModel fund, FundOrderReadModel order,
        FundOrderTradeReadModel trade, int portfolioId, bool historicalReadOnly)
    {
        ArgumentNullException.ThrowIfNull(appRoot);
        var tradeOrderId = new TradeOrderId(portfolioId, fund.FundId, order.OrderId);
        Name = trade.TradeType == TradeType.FuturesOutright ? "FuturesView" : "VerticalSpreadView";
        Dock = DockStyle.Fill;
        BackColor = Color.Black;
        ForeColor = Color.White;
        Font = new Font("Microsoft Sans Serif", 10F);
        _header =
        [
            $"Strategy: {(trade.TradeType == TradeType.FuturesOutright ? "Futures" : "Vertical Spread")}",
            $"Portfolio / Fund / Order / Trade: {portfolioId} / {fund.FundId} / {order.OrderId} / {trade.TradeId}",
            $"State: {trade.TradeState}",
            $"Contracts: {string.Join(", ", trade.GetContractIds())}",
            $"Reference: {trade.Reference}",
            historicalReadOnly ? "Mode: Historical read-only" : "Mode: Current trade"
        ];
        var header = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = Color.Black,
            ForeColor = Color.White,
            Font = Font,
            Padding = new Padding(16),
            TextAlign = ContentAlignment.TopLeft,
            Text = string.Join(Environment.NewLine + Environment.NewLine, _header)
        };
        _evidence = new BrokerExecutionEvidenceControl(appRoot, tradeOrderId);
        Controls.Add(_evidence);
        Controls.Add(header);
    }

    /// <summary>Opens the blotter.</summary>
    public void Open() => _ = _evidence.RefreshAsync();

    /// <summary>Refreshes account, broker-order, and execution evidence for the selected Trade Order.</summary>
    public Task RefreshAsync() => _evidence.RefreshAsync();

    /// <summary>Closes the blotter.</summary>
    public void Close() { }

    /// <inheritdoc />
    void IFormControl.Resize(Control parentControl) => Bounds = parentControl.ClientRectangle;

}
