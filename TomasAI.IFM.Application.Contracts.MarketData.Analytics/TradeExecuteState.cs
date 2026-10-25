using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared
{
    public enum TradeExecuteState
    {
        No,
        Wait,
        Yes,
        Hold,
        Enter,
        ExitOnTrendReversion,
        ExitOnEntryLimit,
        InTrade,
        RangeBound,
        CloseOnTrendReversion,
        CloseOnEntryLimit,
        OpenAtAskPrice,
        OpenAtMidPrice
    }
}
