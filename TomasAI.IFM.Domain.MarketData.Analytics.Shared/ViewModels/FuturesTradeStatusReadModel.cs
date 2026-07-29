using TomasAI.IFM.Domain.Trade.Shared;
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
