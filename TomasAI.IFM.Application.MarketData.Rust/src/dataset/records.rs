//! Owned normalization of the pinned SDK's borrowed DBN records.
//!
//! Adapted from `native.rust/DatabentoFeed.Rust/src/live.rs::normalize` without
//! changing that bridge. Integers retain wire precision and wrapping timestamp
//! conversion exactly; decimal/price policy is applied by later hot-state owners.

use databento::dbn::{
    MboMsg, Mbp1Msg, RecordHeader, RecordRef, Schema, StatMsg, TradeMsg, UNDEF_PRICE,
};

/// Provider-neutral observation metadata, matching the current bridge header.
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub struct Header {
    /// Databento instrument identity within the lane's generation.
    pub instrument: u32,
    /// Publisher identifies the source, not the subscription identity.
    pub publisher: u16,
    /// Adapter flags: bit 0 snapshot, bit 1 replay, bit 2 undefined price.
    pub flags: u8,
    /// Event timestamp in nanoseconds since Unix epoch (same i64 cast as baseline).
    pub event_ns: i64,
    /// Provider receive timestamp, using the same representation.
    pub received_ns: i64,
    /// Provider source sequence; never interpreted as an application trade ordinal.
    pub sequence: u32,
    /// Original DBN schema numeric identity.
    pub schema: u16,
}

/// Top-of-book update with exact nanounit prices and native quantity/count units.
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub struct Quote {
    /// Observation metadata.
    pub header: Header,
    /// Bid price in 1e-9 price units; zero when undefined and flagged.
    pub bid: i64,
    /// Ask price in 1e-9 price units; zero when undefined and flagged.
    pub ask: i64,
    /// Bid quantity.
    pub bid_size: u32,
    /// Ask quantity.
    pub ask_size: u32,
    /// Bid order count.
    pub bid_count: u32,
    /// Ask order count.
    pub ask_count: u32,
}

/// Owned execution record, preserving lifecycle/condition bytes for aggregation.
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub struct Trade {
    /// Observation metadata.
    pub header: Header,
    /// Trade price in 1e-9 units.
    pub price: i64,
    /// Trade quantity.
    pub size: u32,
    /// Original provider action character.
    pub action: u8,
    /// Original aggressor-side character.
    pub side: u8,
    /// Original DBN condition flags.
    pub dbn_flags: u8,
    /// Original book depth.
    pub depth: u8,
    /// Receive-event timing delta in nanoseconds.
    pub delta_ns: i32,
}

/// Owned market-by-order update; authoritative consumers must not coalesce it.
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub struct Order {
    /// Observation metadata.
    pub header: Header,
    /// Provider order identity.
    pub order_id: u64,
    /// Price in 1e-9 units.
    pub price: i64,
    /// Order quantity.
    pub size: u32,
    /// Original action character.
    pub action: u8,
    /// Original side character.
    pub side: u8,
    /// Original DBN condition flags.
    pub dbn_flags: u8,
    /// Provider channel identity.
    pub channel: u8,
    /// Receive-event timing delta in nanoseconds.
    pub delta_ns: i32,
}

/// Owned provider session statistic, including corrections and final volume.
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub struct Statistic {
    /// Observation metadata.
    pub header: Header,
    /// Price in 1e-9 units.
    pub price: i64,
    /// Quantity; preserve the provider's undefined sentinel for policy validation.
    pub quantity: i64,
    /// Reference timestamp in nanoseconds, using the baseline signed cast.
    pub reference_ns: i64,
    /// Original statistic type.
    pub kind: u16,
    /// Provider channel identity.
    pub channel: u16,
    /// Original update action.
    pub action: u8,
    /// Original statistic flags.
    pub flags: u8,
}

/// Native owned observation; contains no pointer into the SDK decode buffer.
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum Observation {
    /// Replaceable latest top-of-book view, after authoritative processing.
    Quote(Quote),
    /// Execution input; preserve authoritative aggregate contributions.
    Trade(Trade),
    /// Market-by-order input.
    Order(Order),
    /// Provider statistic input.
    Statistic(Statistic),
}

fn header(source: &RecordHeader, received: u64, sequence: u32, schema: Schema) -> Header {
    Header {
        instrument: source.instrument_id,
        publisher: source.publisher_id,
        flags: 0,
        event_ns: source.ts_event as i64,
        received_ns: received as i64,
        sequence,
        schema: schema as u16,
    }
}

fn price(value: i64, flags: &mut u8) -> i64 {
    if value == UNDEF_PRICE {
        *flags |= 4;
        0
    } else {
        value
    }
}

/// Copies a supported market record while its SDK borrow is valid. Control,
/// mapping, errors and definitions return None and must be handled by the caller;
/// None is not permission to silently ignore an unknown processing exception.
pub fn normalize(
    source: RecordRef<'_>,
    statistics_replay: bool,
    trade_replay: bool,
) -> Option<Observation> {
    if let Some(message) = source.get::<Mbp1Msg>() {
        let mut header = header(&message.hd, message.ts_recv, message.sequence, Schema::Mbp1);
        if message.flags.is_snapshot() {
            header.flags |= 1;
        }
        let level = &message.levels[0];
        return Some(Observation::Quote(Quote {
            bid: price(level.bid_px, &mut header.flags),
            ask: price(level.ask_px, &mut header.flags),
            header,
            bid_size: level.bid_sz,
            ask_size: level.ask_sz,
            bid_count: level.bid_ct,
            ask_count: level.ask_ct,
        }));
    }
    if let Some(message) = source.get::<TradeMsg>() {
        let mut header = header(
            &message.hd,
            message.ts_recv,
            message.sequence,
            Schema::Trades,
        );
        if message.flags.is_snapshot() {
            header.flags |= 1;
        }
        if trade_replay {
            header.flags |= 2;
        }
        return Some(Observation::Trade(Trade {
            price: price(message.price, &mut header.flags),
            header,
            size: message.size,
            action: message.action as u8,
            side: message.side as u8,
            dbn_flags: message.flags.raw(),
            depth: message.depth,
            delta_ns: message.ts_in_delta,
        }));
    }
    if let Some(message) = source.get::<MboMsg>() {
        let mut header = header(&message.hd, message.ts_recv, message.sequence, Schema::Mbo);
        if message.flags.is_snapshot() {
            header.flags |= 1;
        }
        return Some(Observation::Order(Order {
            price: price(message.price, &mut header.flags),
            header,
            order_id: message.order_id,
            size: message.size,
            action: message.action as u8,
            side: message.side as u8,
            dbn_flags: message.flags.raw(),
            channel: message.channel_id,
            delta_ns: message.ts_in_delta,
        }));
    }
    if let Some(message) = source.get::<StatMsg>() {
        let mut header = header(
            &message.hd,
            message.ts_recv,
            message.sequence,
            Schema::Statistics,
        );
        if statistics_replay {
            header.flags |= 2;
        }
        return Some(Observation::Statistic(Statistic {
            price: price(message.price, &mut header.flags),
            header,
            quantity: message.quantity,
            reference_ns: message.ts_ref as i64,
            kind: message.stat_type,
            channel: message.channel_id,
            action: message.update_action,
            flags: message.stat_flags,
        }));
    }
    None
}
