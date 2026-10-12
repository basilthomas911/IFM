using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared;

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
