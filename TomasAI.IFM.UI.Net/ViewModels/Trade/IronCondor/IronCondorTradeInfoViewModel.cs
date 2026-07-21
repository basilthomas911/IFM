using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.UI.Net.Views.Trade.IronCondor;

namespace TomasAI.IFM.UI.Net.ViewModels.Trade.IronCondor
{
    public class IronCondorTradeInfoReadModel
    {
        private readonly IronCondorViewModel _baseViewModel;

        public IronCondorTradeInfoReadModel(IronCondorViewModel baseViewModel)
        {
            _baseViewModel = baseViewModel;
        }

        public string OrderReference => _baseViewModel.FundOrder.Reference;
        public string TradeReference => _baseViewModel.FundOrderTrade.Reference;
        public TradeAction TradeAction => _baseViewModel.FundOrderTrade.TradeAction;
        public TradeType TradeType => _baseViewModel.FundOrderTrade.TradeType;
    }
}
