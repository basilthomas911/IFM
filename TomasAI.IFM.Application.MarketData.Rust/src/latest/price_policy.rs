//! Existing IMarketDataApi mark selection expressed natively. Source freshness
//! uses API time; it never opens a subscription or retains a resource handle.
use crate::{
    dataset::live::RawPrices,
    messaging::codec,
    option_chain::qualification::Decimal,
    reference::{decimal, decimal_arithmetic as math},
};
fn exact(n: i64) -> Option<Decimal> {
    decimal::parse(codec::price(n).as_str()?)
}
fn half(value: Decimal) -> Option<Decimal> {
    let mut coefficient = u128::from(value.bits[0])
        | (u128::from(value.bits[1]) << 32)
        | (u128::from(value.bits[2]) << 64);
    let mut scale = (value.bits[3] >> 16) & 255;
    if value.bits[3] & 0x8000_0000 != 0 || scale > 27 {
        return None;
    }
    coefficient = coefficient.checked_mul(5)?;
    scale += 1;
    while scale != 0 && coefficient.is_multiple_of(10) {
        coefficient /= 10;
        scale -= 1;
    }
    if coefficient >= 1u128 << 96 {
        return None;
    }
    Some(Decimal {
        bits: [
            coefficient as u32,
            (coefficient >> 32) as u32,
            (coefficient >> 64) as u32,
            scale << 16,
        ],
    })
}
/// Futures use a fresh raw trade before the current quote; options use a fresh
/// positive two-sided quote and preserve the existing crossed-quote failure.
///
/// # Errors
/// The existing invalid-option-quote code, or impossible exact decimal arithmetic.
pub fn calculate(
    raw: RawPrices,
    options: bool,
    now: i64,
    maximum_age: i64,
) -> Result<Option<Decimal>, &'static str> {
    if maximum_age <= 0 {
        return Err("InvalidPriceFreshnessPolicy");
    }
    let fresh = |at| {
        now.checked_sub(at)
            .is_some_and(|age| age >= 0 && age <= maximum_age)
    };
    if !options && let Some(trade) = raw.trade.filter(|t| fresh(t.event_ticks)) {
        return exact(trade.value.price)
            .map(Some)
            .ok_or("InvalidNativePrice");
    }
    let Some(quote) = raw.quote.filter(|q| fresh(q.event_ticks)) else {
        return Ok(None);
    };
    let (Some(bid), Some(ask)) = (quote.value.bid, quote.value.ask) else {
        return Ok(None);
    };
    if bid <= 0 || ask <= 0 {
        return Ok(None);
    }
    if bid > ask {
        return if options {
            Err("InvalidFuturesOptionQuote")
        } else {
            Ok(None)
        };
    }
    let (bid, ask) = (
        exact(bid).ok_or("InvalidNativePrice")?,
        exact(ask).ok_or("InvalidNativePrice")?,
    );
    math::subtract(ask, bid)
        .and_then(half)
        .and_then(|difference| math::add(bid, difference))
        .map(Some)
        .ok_or("InvalidNativePrice")
}
