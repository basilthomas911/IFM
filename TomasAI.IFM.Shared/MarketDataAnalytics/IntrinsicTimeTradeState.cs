using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.MarketDataAnalytics;

/// <summary>
/// Specifies the possible states of an intrinsic time trade during its lifecycle.
/// </summary>
/// <remarks>Use this enumeration to track and manage the status of a trade as it progresses through different
/// phases, such as being ready to execute, on hold, opened, or closed. The values provide a clear way to represent and
/// evaluate the current state of a trade within trading workflows.</remarks>
public enum IntrinsicTimeTradeState
{
    Ready,
    Hold,
    Opened,
    Closed
}

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
