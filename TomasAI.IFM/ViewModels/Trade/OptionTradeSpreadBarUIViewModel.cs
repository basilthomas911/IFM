using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.ViewModels.Trade
{
    public class OptionTradeSpreadBarUIViewModel
    {
        readonly OptionTradeSpreadBarDataViewModel _optionTradeSpreadBarData;
        readonly IronCondorMDILimitViewModel _ironCondorMDILimit;

        public OptionTradeSpreadBarUIViewModel(OptionTradeSpreadBarDataViewModel optionTradeSpreadBarData, IronCondorMDILimitViewModel ironCondorMDILimit) 
        {
            _optionTradeSpreadBarData = optionTradeSpreadBarData;
            _ironCondorMDILimit = ironCondorMDILimit;
        }
        public DateTime BarDate => _optionTradeSpreadBarData.BarDate;
        public double LossLimit => _optionTradeSpreadBarData.LossLimit;
        public double WinLimit => _optionTradeSpreadBarData.WinLimit;
        public decimal ForwardSpread => _optionTradeSpreadBarData.ForwardSpread;
        public decimal NetSpread => _optionTradeSpreadBarData.NetSpread;
        public double MDIWarningLimit => _ironCondorMDILimit?.WarningLimit ?? 0;
        public double MDIMaxLimit => _ironCondorMDILimit?.MaxLimit ?? 0;
    }
}
