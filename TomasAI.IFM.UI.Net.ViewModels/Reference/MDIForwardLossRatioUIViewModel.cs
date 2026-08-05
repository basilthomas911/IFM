using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.UI.Net.ViewModels.Reference
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
