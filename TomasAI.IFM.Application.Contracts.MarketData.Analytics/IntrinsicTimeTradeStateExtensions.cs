using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

public static class IntrinsicTimeTradeStateExtensions
{
    public static string ToStringFast(this IntrinsicTimeTradeState value) => value switch
    {
        IntrinsicTimeTradeState.Ready => nameof(IntrinsicTimeTradeState.Ready),
        IntrinsicTimeTradeState.Hold => nameof(IntrinsicTimeTradeState.Hold),
        IntrinsicTimeTradeState.Opened => nameof(IntrinsicTimeTradeState.Opened),
        IntrinsicTimeTradeState.Closed => nameof(IntrinsicTimeTradeState.Closed),
        _ => value.ToString()
    };
}
