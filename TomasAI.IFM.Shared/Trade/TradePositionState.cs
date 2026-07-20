using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade;

/// <summary>
/// Represents the lifecycle state of an option trade position.
/// </summary>
/// <remarks>This enumeration is used to track whether a trade position has been opened or closed.
/// State transitions are driven by domain events such as <c>OptionTradePositionOpenedEvent</c>
/// and <c>OptionTradePositionClosedEvent</c>. The <see cref="Unknown"/> value serves as the
/// default uninitialized state before any position action has occurred.</remarks>
public enum TradePositionState
{
    /// <summary>
    /// The trade position state has not been set. This is the default value before any
    /// position open or close action has been applied.
    /// </summary>
    Unknown,

    /// <summary>
    /// The trade position has been opened. All trade positions associated with the option trade
    /// are actively held and eligible for intra-day processing, leg data changes, and P&amp;L tracking.
    /// </summary>
    Opened,

    /// <summary>
    /// The trade position has been closed. All trade positions associated with the option trade
    /// have been finalized and are no longer actively traded.
    /// </summary>
    Closed
}

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
