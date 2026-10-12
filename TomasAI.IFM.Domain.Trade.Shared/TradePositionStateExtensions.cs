using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>
/// Provides high-performance string conversion extension methods for <see cref="TradePositionState"/>.
/// </summary>
public static class TradePositionStateExtensions
{
    /// <summary>
    /// Converts the specified <see cref="TradePositionState"/> value to its string representation
    /// without allocating memory for known values.
    /// </summary>
    /// <param name="value">The <see cref="TradePositionState"/> value to convert.</param>
    /// <returns>The name of the enum member as a string.</returns>
    public static string ToStringFast(this TradePositionState value) => value switch
    {
        TradePositionState.Unknown => nameof(TradePositionState.Unknown),
        TradePositionState.Opened => nameof(TradePositionState.Opened),
        TradePositionState.Closed => nameof(TradePositionState.Closed),
        _ => value.ToString()
    };
}
