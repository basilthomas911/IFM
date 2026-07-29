using TomasAI.IFM.Shared.Trade;
using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels
{
    public record FuturesTradeStatusReadModel(
        string TradeStatus,
        TradeExecuteState? TradeExecuteState)
    {
    }
}
