//! Exact complete raw snapshot query DTOs. All source prices become CLR decimal
//! units here; the C# adapter performs no arithmetic or market-value selection.
use super::{codec, wire::guid};
use crate::dataset::live::RawPrices;
use rmpv::Value;
/// Existing raw trade/quote transfer followed by the exact physical generation.
/// Price-publication tick suppression must never suppress a raw execution read.
///
/// # Errors
/// Invalid date, canonical identity or out-of-range timestamp.
pub fn value(contract: &str, date: i32, raw: RawPrices) -> Result<Value, &'static str> {
    if contract.trim().is_empty()
        || contract.len() > 1024
        || !(0..=3652058).contains(&date)
        || raw.generation == 0
    {
        return Err("Invalid native raw price identity");
    }
    let trade = raw
        .trade
        .map(|t| -> Result<Value, &'static str> {
            Ok(Value::Array(vec![
                contract.into(),
                date.into(),
                codec::price(t.value.price),
                t.value.size.into(),
                t.sequence.into(),
                codec::utc_offset(t.event_ticks)?,
                codec::utc_offset(t.receive_ticks)?,
            ]))
        })
        .transpose()?
        .unwrap_or(Value::Nil);
    let quote = raw
        .quote
        .map(|q| -> Result<Value, &'static str> {
            Ok(Value::Array(vec![
                contract.into(),
                date.into(),
                q.value.bid.map_or(Value::Nil, codec::price),
                q.value.bid_size.into(),
                q.value.bid_count.into(),
                q.value.ask.map_or(Value::Nil, codec::price),
                q.value.ask_size.into(),
                q.value.ask_count.into(),
                q.sequence.into(),
                codec::utc_offset(q.event_ticks)?,
                codec::utc_offset(q.receive_ticks)?,
                codec::utc_offset(q.value.local_received_ticks)?,
            ]))
        })
        .transpose()?
        .unwrap_or(Value::Nil);
    Ok(Value::Array(vec![
        trade,
        quote,
        guid(raw.generation.to_le_bytes()).into(),
    ]))
}
