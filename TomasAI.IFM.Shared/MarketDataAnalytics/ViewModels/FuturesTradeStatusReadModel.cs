using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels
{
    public record FuturesTradeStatusReadModel(
        string TradeStatus,
        TradeExecuteState? TradeExecuteState)
    {
    }
}
