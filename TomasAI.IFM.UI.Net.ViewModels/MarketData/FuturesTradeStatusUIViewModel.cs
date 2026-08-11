using TomasAI.IFM.Domain.Trade.Shared;
using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.ViewModels.MarketData
{
    public class FuturesTradeStatusUIViewModel
    {
        readonly FuturesTradeStatusReadModel _futuresTradeStatus;
        public FuturesTradeStatusUIViewModel(FuturesTradeStatusReadModel futuresTradeStatus)
        { 
            _futuresTradeStatus = futuresTradeStatus;   
        }

        public string TradeStatus
            => _futuresTradeStatus.TradeStatus;

        public bool TradeStatusEnabled
            => _futuresTradeStatus.TradeExecuteState switch {
                TradeExecuteState.Enter => true,
                TradeExecuteState.ExitOnTrendReversion => true,
                TradeExecuteState.ExitOnEntryLimit => true,
                _ => false
            };

        public PresentationColorRole TradeStatusForeColor
            => _futuresTradeStatus.TradeExecuteState switch  {
                null => PresentationColorRole.LightText,
                TradeExecuteState.Enter => PresentationColorRole.DarkText,
                TradeExecuteState.ExitOnTrendReversion => PresentationColorRole.DarkText,
                TradeExecuteState.ExitOnEntryLimit => PresentationColorRole.DarkText,
                TradeExecuteState.Hold => PresentationColorRole.DarkText,
                TradeExecuteState.No => PresentationColorRole.DarkText,
                TradeExecuteState.InTrade => PresentationColorRole.LightText,
                TradeExecuteState.RangeBound => PresentationColorRole.DarkText,
                _ => PresentationColorRole.LightText
            };

        public PresentationColorRole TradeStatusBackColor
            => _futuresTradeStatus.TradeExecuteState switch  {
                null => PresentationColorRole.DarkSurface,
                TradeExecuteState.Enter => PresentationColorRole.Positive,
                TradeExecuteState.ExitOnTrendReversion => PresentationColorRole.Negative,
                TradeExecuteState.ExitOnEntryLimit => PresentationColorRole.Negative,
                TradeExecuteState.Hold => PresentationColorRole.Caution,
                TradeExecuteState.No => PresentationColorRole.Caution,
                TradeExecuteState.InTrade => PresentationColorRole.DarkSurface,
                TradeExecuteState.RangeBound => PresentationColorRole.Caution,
                _ => PresentationColorRole.DarkSurface
            };

    }
}
