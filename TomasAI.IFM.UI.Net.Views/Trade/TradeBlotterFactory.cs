using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Views.Trade.IronCondor;
using TomasAI.IFM.UI.Net.ViewModels.Trade.IronCondor;
using TomasAI.IFM.UI.Net.Models.Portfolio;

namespace TomasAI.IFM.UI.Net.Views.Trade;

public static class TradeBlotterFactory
{
    /// <summary>Creates the existing strategy view using the actual execution-created trade.</summary>
    /// <param name="parentControl">The host for the strategy view.</param>
    /// <param name="appRoot">The application service boundary.</param>
    /// <param name="fund">The owning fund.</param>
    /// <param name="fundOrder">The selected setup order.</param>
    /// <param name="setupTrade">The setup row linked to the established trade.</param>
    /// <param name="trade">The persisted financial trade and its execution evidence.</param>
    /// <param name="baseContracts">Available underlying futures contracts.</param>
    /// <returns>The strategy-specific trade view.</returns>
    public static Control CreateEstablished(Control parentControl, IAppRoot appRoot, PortfolioFundEditorModel fund,
        PortfolioFundOrderEditorModel fundOrder, PortfolioFundOrderTradeEditorModel setupTrade,
        EstablishedTradeDefinition trade, ICollection<FuturesContractV3ReadModel> baseContracts,
        BrokerEnvironment brokerEnvironment = BrokerEnvironment.Live)
        => trade.StrategyKind switch
        {
            TradeStrategyKind.IronCondor => new IronCondorTradeView(parentControl,
                new IronCondorViewModel(appRoot, fund, fundOrder, setupTrade,
                    DateOnly.FromDateTime(trade.EstablishedAtUtc), baseContracts,
                    historicalReadOnly: trade.Status == EstablishedTradeStatus.Closed,
                    portfolioId: trade.Id.PortfolioId, establishedTrade: trade, brokerEnvironment: brokerEnvironment)),
            _ => new EstablishedTradeView(trade)
        };

    /// <summary>Creates the strategy-specific trade monitor for a selected trade.</summary>
    /// <param name="parentControl">The control that will host the monitor.</param>
    /// <param name="appRoot">The application service boundary.</param>
    /// <param name="fund">The Fund that owns the selected trade.</param>
    /// <param name="fundOrder">The Fund order containing the selected trade.</param>
    /// <param name="fundOrderTrade">The selected trade.</param>
    /// <param name="valueDate">The optional historical value date.</param>
    /// <param name="baseContracts">The available futures contracts.</param>
    /// <param name="historicalReadOnly">Whether the monitor is restricted to historical display.</param>
    /// <param name="portfolioId">The Portfolio component of the canonical trade identity.</param>
    /// <returns>The supported strategy monitor, or <see langword="null"/> when no monitor is available.</returns>
    public static Control? Create(Control parentControl, IAppRoot appRoot, PortfolioFundEditorModel fund, PortfolioFundOrderEditorModel fundOrder, PortfolioFundOrderTradeEditorModel fundOrderTrade, DateOnly? valueDate, ICollection<FuturesContractV3ReadModel> baseContracts, bool historicalReadOnly = false, int portfolioId = 0)
    {
        var blotter = default(Control);
        switch (fundOrderTrade.TradeType)
        {
            case TradeType.ShortIronCondor:
            case TradeType.LongIronCondor:
                var viewModel = new IronCondorViewModel(appRoot, fund, fundOrder, fundOrderTrade, valueDate, baseContracts, historicalReadOnly: historicalReadOnly, portfolioId: portfolioId);
                var ironCondor = new IronCondorTradeView(parentControl, viewModel);
                blotter = new EsTradeBlotterControl(appRoot, fund, fundOrder, fundOrderTrade,
                    portfolioId, historicalReadOnly, workflowControl: ironCondor)
                {
                    Name = "IronCondorTradeView"
                };
                break;
            case TradeType.FuturesOutright:
            case TradeType.PutCreditSpread:
            case TradeType.PutDebitSpread:
            case TradeType.CallCreditSpread:
            case TradeType.CallDebitSpread:
                blotter = new BrokerTradeBlotterView(
                    appRoot, fund, fundOrder, fundOrderTrade, portfolioId, historicalReadOnly);
                break;
        }
        return blotter;
    }

}
