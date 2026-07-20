using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.MarketDataAnalytics;

namespace TomasAI.IFM.ViewModels.Reference
{
    public class MDIForwardLossRatioUIViewModel
    {
        public MDIForwardLossRatioUIViewModel(MDIForwardLossRatioReadModel e) 
        {
            MDI = $"MDI >= {e.MDI}";
            TrendDirection = $"{e.TrendDirection}";
            TradeType = $"{e.TradeType}";
            ForwardLossRatio = $"{e.ForwardLossRatio:F2}";
        }

        public string MDI { get; private set; }
        public string TrendDirection { get; private set; }
        public string TradeType { get; private set; }
        public string ForwardLossRatio { get; private set; }

    }
}
