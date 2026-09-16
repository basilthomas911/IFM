using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Views.Trade.IronCondor;
using TomasAI.IFM.UI.Net.ViewModels.Trade.IronCondor;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.Views.Trade;

public static class TradeBlotterFactory
{
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
    public static Control? Create(Control parentControl, IAppRoot appRoot, FundReadModel fund,  FundOrderReadModel fundOrder, FundOrderTradeReadModel fundOrderTrade, DateOnly? valueDate, ICollection<FuturesContractV3ReadModel> baseContracts, bool historicalReadOnly = false, int portfolioId = 0)
    {
        var blotter = default(Control);
        switch(fundOrderTrade.TradeType)
        {
            case TradeType.ShortIronCondor:
            case TradeType.LongIronCondor:
                var viewModel = new IronCondorViewModel(appRoot, fund, fundOrder, fundOrderTrade, valueDate, baseContracts, historicalReadOnly: historicalReadOnly, portfolioId: portfolioId);
                blotter = new IronCondorView(parentControl, viewModel);
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
