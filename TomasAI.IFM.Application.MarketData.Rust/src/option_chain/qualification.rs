//! Hot quote qualification after immutable reference/calendar qualification.
//! Decimal prices retain all 96 coefficient bits and all 28 CLR scale digits;
//! no floating conversion or allocation participates in validity decisions.
use std::cmp::Ordering;

/// Explicit CLR decimal representation; never rely on CLR's in-memory ordering.
#[repr(C)]
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq)]
pub struct Decimal {
    /// Low, middle, high coefficient words, then sign/scale flags.
    pub bits: [u32; 4],
}
impl Decimal {
    fn parts(self) -> Option<(bool, u128, u32)> {
        let flags = self.bits[3];
        let scale = (flags >> 16) & 255;
        if flags & 0x7f00ffff != 0 || scale > 28 {
            return None;
        }
        let coefficient = u128::from(self.bits[0])
            | (u128::from(self.bits[1]) << 32)
            | (u128::from(self.bits[2]) << 64);
        Some((flags >> 31 != 0 && coefficient != 0, coefficient, scale))
    }
    /// Compares exact decimals without multiplying a 96-bit coefficient into
    /// an overflowing intermediate. Signed zero compares equal at every scale.
    pub fn compare(self, other: Self) -> Option<Ordering> {
        let (negative, coefficient, scale) = self.parts()?;
        let (other_negative, other_coefficient, other_scale) = other.parts()?;
        if negative != other_negative {
            return Some(if negative {
                Ordering::Less
            } else {
                Ordering::Greater
            });
        }
        fn magnitude(a: u128, scale: u32, b: u128, other_scale: u32) -> Ordering {
            if scale < other_scale {
                return magnitude(b, other_scale, a, scale).reverse();
            }
            let divisor = 10_u128.pow(scale - other_scale);
            (a / divisor).cmp(&b).then_with(|| (a % divisor).cmp(&0))
        }
        let order = magnitude(coefficient, scale, other_coefficient, other_scale);
        Some(if negative { order.reverse() } else { order })
    }
}
/// Frozen quote in exact application units. Times are 100 ns ticks since Unix.
#[repr(C)]
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq)]
pub struct Quote {
    /// Opaque generation low bits.
    pub generation_low: u64,
    /// Opaque generation high bits.
    pub generation_high: u64,
    /// UTC event time.
    pub event: i64,
    /// UTC receive time.
    pub received: i64,
    /// Provider sequence, nonnegative for qualified inputs.
    pub sequence: i64,
    /// Exact bid.
    pub bid: Decimal,
    /// Exact ask.
    pub ask: Decimal,
    /// Bid quantity.
    pub bid_size: Decimal,
    /// Ask quantity.
    pub ask_size: Decimal,
    /// Original event UTC offset in minutes; qualification requires zero.
    pub event_offset: i32,
    /// Original receive UTC offset in minutes; qualification requires zero.
    pub received_offset: i32,
}
/// Complete allocation-free validation input. Reference metadata has already
/// been qualified; identity flags are ordinal string comparisons at the bridge.
#[repr(C)]
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq)]
pub struct Request {
    /// Layout size, 304 bytes.
    pub size: u32,
    /// Underlying ID match=1, option ID match=2, schema 3=4, call=8, metadata call=16.
    pub flags: u32,
    /// Context generation low bits.
    pub generation_low: u64,
    /// Context generation high bits.
    pub generation_high: u64,
    /// API-provided valuation UTC time in Unix ticks.
    pub at: i64,
    /// Qualified quote age limit, 1..5000 ms.
    pub maximum_age: u32,
    /// Qualified source skew limit, 0..2000 ms.
    pub maximum_skew: u32,
    /// Qualified source clock lead, 0..2000 ms.
    pub maximum_lead: u32,
    /// Reserved; zero.
    pub reserved: u32,
    /// Requested strike.
    pub strike: Decimal,
    /// Definition strike, used for schema 3.
    pub definition_strike: Decimal,
    /// Current underlying quote.
    pub underlying: Quote,
    /// Exact option quote being calculated.
    pub option: Quote,
}
/// Exact first failure in the current application validator's order.
#[repr(C)]
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq)]
pub struct Result {
    /// Layout size, 16 bytes.
    pub size: u32,
    /// 0 success, 1 generation, 2 identity/strike, 3 invalid quote, 4 stale, 5 skew.
    pub failure: u32,
    /// Quote index 0 underlying, 1 option; meaningful for failures 3/4 only.
    pub input: u32,
    /// Reserved; zero.
    pub reserved: u32,
}
const MIN_TICKS: i64 = -621_355_968_000_000_000;
const MAX_TICKS: i64 = 2_534_023_007_999_999_999;
/// Performs quote/generation/strike validation. None means malformed ABI input,
/// distinct from a valid financial request whose output contains a failure.
pub fn validate(request: &Request) -> Option<Result> {
    let r = request;
    if r.size != 304
        || r.reserved != 0
        || r.flags > 31
        || !(1..=5000).contains(&r.maximum_age)
        || r.maximum_skew > 2000
        || r.maximum_lead > 2000
        || !(MIN_TICKS..=MAX_TICKS).contains(&r.at)
        || (r.generation_low == 0 && r.generation_high == 0)
    {
        return None;
    }
    r.strike.parts()?;
    r.definition_strike.parts()?;
    for quote in [&r.underlying, &r.option] {
        quote.bid.parts()?;
        quote.ask.parts()?;
        quote.bid_size.parts()?;
        quote.ask_size.parts()?;
        if !(MIN_TICKS..=MAX_TICKS).contains(&quote.event)
            || !(MIN_TICKS..=MAX_TICKS).contains(&quote.received)
        {
            return None;
        }
    }
    let fail = |failure, input| Result {
        size: 16,
        failure,
        input,
        reserved: 0,
    };
    if [&r.underlying, &r.option].iter().any(|quote| {
        quote.generation_low != r.generation_low || quote.generation_high != r.generation_high
    }) {
        return Some(fail(1, 0));
    }
    if r.flags & 3 != 3
        || r.strike.compare(Decimal::default())? != Ordering::Greater
        || r.flags & 4 != 0
            && (r.strike.compare(r.definition_strike)? != Ordering::Equal
                || (r.flags & 8 != 0) != (r.flags & 16 != 0))
    {
        return Some(fail(2, 0));
    }
    for (index, quote) in [&r.underlying, &r.option].into_iter().enumerate() {
        if quote.bid.compare(Decimal::default())? != Ordering::Greater
            || quote.ask.compare(quote.bid)? == Ordering::Less
            || quote.bid_size.compare(Decimal::default())? == Ordering::Less
            || quote.ask_size.compare(Decimal::default())? == Ordering::Less
            || quote.sequence < 0
            || quote.event_offset != 0
            || quote.received_offset != 0
            || i128::from(quote.event) > i128::from(r.at) + i128::from(r.maximum_lead) * 10_000
            || quote.received > r.at
        {
            return Some(fail(3, index as u32));
        }
        if i128::from(r.at) - i128::from(quote.event) > i128::from(r.maximum_age) * 10_000
            || i128::from(r.at) - i128::from(quote.received) > i128::from(r.maximum_age) * 10_000
        {
            return Some(fail(4, index as u32));
        }
    }
    if (i128::from(r.underlying.event) - i128::from(r.option.event)).abs()
        > i128::from(r.maximum_skew) * 10_000
    {
        return Some(fail(5, 0));
    }
    Some(fail(0, 0))
}
