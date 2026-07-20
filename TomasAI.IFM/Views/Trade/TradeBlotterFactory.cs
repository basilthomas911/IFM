using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Configuration;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Views.MarketData;
using TomasAI.IFM.Views.Trade;
using TomasAI.IFM.Views.Trade.IronCondor;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.ViewModels.Trade.IronCondor;

namespace TomasAI.IFM.Views.Trade
{
    public static class TradeBlotterFactory
    {
        public static Control Create(Control parentControl, IAppRoot appRoot, FundReadModel fund,  FundOrderReadModel fundOrder, FundOrderTradeReadModel fundOrderTrade, DateTime? valueDate, ICollection<FuturesContractViewModel> baseContracts)
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
}
