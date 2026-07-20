using TomasAI.IFM.Contracts;
using TomasAI.IFM.Views.Trade.IronCondor;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.ViewModels.Trade.IronCondor;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Views.Trade;

public static class TradeBlotterFactory
{
    public static Control? Create(Control parentControl, IAppRoot appRoot, FundReadModel fund,  FundOrderReadModel fundOrder, FundOrderTradeReadModel fundOrderTrade, DateOnly? valueDate, ICollection<FuturesContractV2ReadModel> baseContracts)
    {
        var blotter = default(Control);
        switch(fundOrderTrade.TradeType)
        {
            case TradeType.ShortIronCondor:
            case TradeType.LongIronCondor:
                var viewModel = new IronCondorViewModel(appRoot, fund, fundOrder, fundOrderTrade, valueDate, baseContracts);
                blotter = new IronCondorView(parentControl, viewModel);
                break;
        }
        return blotter;
    }

}
