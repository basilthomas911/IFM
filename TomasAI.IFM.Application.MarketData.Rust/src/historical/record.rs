//! Exact historical wire mapping copied from the frozen bridge's
//! `historical_record` function (baseline 8f5c89f93). No financial aggregation or
//! provider state lives here. The original 31-byte symbol fallback is preserved.
use databento::dbn::{OhlcvMsg, RecordRef, TradeMsg};
/// Blittable historical row. Layout and source units match the frozen bridge;
/// this new library owns the buffer and ABI identity independently.
#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct Record {
    /// Complete DTO size, 120 bytes.
    pub size: u32,
    /// New library ABI identity, one.
    pub abi: u32,
    /// Two for OHLCV, three for individual trades.
    pub kind: u32,
    /// Existing provider-neutral historical schema number.
    pub schema: u32,
    /// Provider instrument identifier.
    pub instrument: u32,
    /// Provider publisher identifier.
    pub publisher: u16,
    /// Existing normalized condition flags (zero in the baseline mapper).
    pub conditions: u16,
    /// Original exchange UTC nanoseconds since Unix epoch.
    pub event: i64,
    /// Source trade sequence or one-based accepted OHLCV row ordinal.
    pub sequence: i64,
    /// Open in fixed 1e-9 price units.
    pub open: i64,
    /// High in fixed 1e-9 price units.
    pub high: i64,
    /// Low in fixed 1e-9 price units.
    pub low: i64,
    /// Close or individual trade in fixed 1e-9 price units.
    pub price: i64,
    /// Original OHLCV volume or trade size.
    pub quantity: u64,
    /// Provider action byte.
    pub action: u8,
    /// Provider side byte.
    pub side: u8,
    /// Reserved zero bytes, validated by the managed reader.
    pub reserved: [u8; 6],
    /// Baseline UTF-8 fallback symbol, at most 31 bytes plus zero termination.
    pub symbol: [u8; 32],
}
/// Copies one supported decoder record. Unknown provider records remain skipped
/// as in the baseline; the caller advances the OHLCV ordinal only for accepted rows.
pub fn normalize(source: RecordRef<'_>, schema: u32, symbol: &str, ordinal: u64) -> Option<Record> {
    let mut row = Record {
        size: 120,
        abi: 1,
        schema,
        ..Default::default()
    };
    if let Some(v) = source.get::<OhlcvMsg>() {
        row.kind = 2;
        row.instrument = v.hd.instrument_id;
        row.publisher = v.hd.publisher_id;
        row.event = v.hd.ts_event as i64;
        row.sequence = ordinal as i64 + 1;
        row.open = v.open;
        row.high = v.high;
        row.low = v.low;
        row.price = v.close;
        row.quantity = v.volume;
    } else {
        let v = source.get::<TradeMsg>()?;
        row.kind = 3;
        row.instrument = v.hd.instrument_id;
        row.publisher = v.hd.publisher_id;
        row.event = v.hd.ts_event as i64;
        row.sequence = i64::from(v.sequence);
        row.price = v.price;
        row.quantity = u64::from(v.size);
        row.action = v.action as u8;
        row.side = v.side as u8;
    }
    let length = symbol.len().min(31);
    row.symbol[..length].copy_from_slice(&symbol.as_bytes()[..length]);
    Some(row)
}
