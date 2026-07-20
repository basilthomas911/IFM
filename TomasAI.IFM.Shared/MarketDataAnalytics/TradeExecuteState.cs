using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.MarketDataAnalytics
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

    public static class TradeExecuteStateExtensions
    {
        public static string ToStringFast(this TradeExecuteState value) => value switch
        {
            TradeExecuteState.No => nameof(TradeExecuteState.No),
            TradeExecuteState.Wait => nameof(TradeExecuteState.Wait),
            TradeExecuteState.Yes => nameof(TradeExecuteState.Yes),
            TradeExecuteState.Hold => nameof(TradeExecuteState.Hold),
            TradeExecuteState.Enter => nameof(TradeExecuteState.Enter),
            TradeExecuteState.ExitOnTrendReversion => nameof(TradeExecuteState.ExitOnTrendReversion),
            TradeExecuteState.ExitOnEntryLimit => nameof(TradeExecuteState.ExitOnEntryLimit),
            TradeExecuteState.InTrade => nameof(TradeExecuteState.InTrade),
            TradeExecuteState.RangeBound => nameof(TradeExecuteState.RangeBound),
            TradeExecuteState.CloseOnTrendReversion => nameof(TradeExecuteState.CloseOnTrendReversion),
            TradeExecuteState.CloseOnEntryLimit => nameof(TradeExecuteState.CloseOnEntryLimit),
            TradeExecuteState.OpenAtAskPrice => nameof(TradeExecuteState.OpenAtAskPrice),
            TradeExecuteState.OpenAtMidPrice => nameof(TradeExecuteState.OpenAtMidPrice),
            _ => value.ToString()
        };
    }
}
