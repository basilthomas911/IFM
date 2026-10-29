//! Exact port of `Framework.MarketData.DataBento/TickAggregation/
//! DatabentoTradeNormalizer.cs`. Values below preserve published wire enum IDs;
//! this module neither drops a trade nor decides its aggregate eligibility.

/// Provider-neutral trade lifecycle action, preserving the C# byte contract.
#[repr(u8)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum TradeAction {
    /// Absent/unsupported provider action.
    Unknown = 0,
    /// A new trade observation.
    New = 1,
    /// Modification of an existing trade.
    Change = 2,
    /// Cancellation of an existing trade.
    Cancel = 3,
    /// Explicit correction (not emitted by the current provider mapping).
    Correct = 4,
    /// Provider cleared its trade state.
    Clear = 5,
    /// Explicit no-action observation.
    None = 6,
}

/// Provider-neutral aggressor side, preserving the C# byte contract.
#[repr(u8)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum TradeSide {
    /// Unsupported provider side.
    Unknown = 0,
    /// Buyer aggressed.
    Buy = 1,
    /// Seller aggressed.
    Sell = 2,
    /// Provider explicitly supplied no side.
    Unspecified = 3,
}

/// Maps all provider action bytes; unsupported values remain explicitly unknown.
pub fn action(value: u8) -> TradeAction {
    match value {
        b'A' | b'F' | b'T' => TradeAction::New,
        b'M' => TradeAction::Change,
        b'C' => TradeAction::Cancel,
        b'R' => TradeAction::Clear,
        b'N' => TradeAction::None,
        _ => TradeAction::Unknown,
    }
}

/// Maps all provider side bytes without guessing an unsupported aggressor side.
pub fn side(value: u8) -> TradeSide {
    match value {
        b'B' => TradeSide::Buy,
        b'A' => TradeSide::Sell,
        b'N' => TradeSide::Unspecified,
        _ => TradeSide::Unknown,
    }
}

/// Converts adapter-header and original DBN flags to the published neutral u16
/// flags. Unknown source bits are ignored exactly as in the current C# function.
/// Snapshot flags from either source are merged, never interpreted as live data.
pub fn conditions(header: u8, dbn: u8) -> u16 {
    let mut flags = 0;
    if dbn & (1 << 7) != 0 {
        flags |= 1;
    }
    if dbn & (1 << 6) != 0 {
        flags |= 1 << 1;
    }
    if header & 1 != 0 || dbn & (1 << 5) != 0 {
        flags |= 1 << 2;
    }
    if header & (1 << 1) != 0 {
        flags |= 1 << 3;
    }
    if dbn & (1 << 4) != 0 {
        flags |= 1 << 4;
    }
    if dbn & (1 << 3) != 0 {
        flags |= 1 << 5;
    }
    if dbn & (1 << 2) != 0 {
        flags |= 1 << 6;
    }
    if dbn & (1 << 1) != 0 {
        flags |= 1 << 7;
    }
    if header & (1 << 2) != 0 {
        flags |= 1 << 8;
    }
    flags
}
