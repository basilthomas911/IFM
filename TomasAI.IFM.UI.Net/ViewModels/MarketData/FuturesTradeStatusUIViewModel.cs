using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

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

        public Color TradeStatusForeColor
            => _futuresTradeStatus.TradeExecuteState switch  {
                null => Color.White,
                TradeExecuteState.Enter => Color.Black,
                TradeExecuteState.ExitOnTrendReversion => Color.Black,
                TradeExecuteState.ExitOnEntryLimit => Color.Black,
                TradeExecuteState.Hold => Color.Black,
                TradeExecuteState.No => Color.Black,
                TradeExecuteState.InTrade => Color.White,
                TradeExecuteState.RangeBound => Color.Black,
                _ => Color.White
            };

        public Color TradeStatusBackColor
            => _futuresTradeStatus.TradeExecuteState switch  {
                null => Color.Black,
                TradeExecuteState.Enter => Color.LimeGreen,
                TradeExecuteState.ExitOnTrendReversion => Color.Red,
                TradeExecuteState.ExitOnEntryLimit => Color.Red,
                TradeExecuteState.Hold => Color.Yellow,
                TradeExecuteState.No => Color.Yellow,
                TradeExecuteState.InTrade => Color.Black,
                TradeExecuteState.RangeBound => Color.Yellow,
                _ => Color.Black
            };

    }
}
