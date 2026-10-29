//! Native quote-derived option snapshot and economic metadata. The enclosing
//! dataset owns observation revision and throttling; this mapping never acquires
//! a lease, reads a current book or substitutes new inputs for a cached result.
use super::{codec, option_evidence::Outcome, option_pricing::raw_price, option_risk};
use crate::{
    option_chain::{
        coalesced::{Calculation, Context as Numeric},
        context::Context,
    },
    pricing::midpoint::midpoint,
    reference::decimal,
};
use rmpv::Value;
const DAY: i64 = 864_000_000_000;
fn quantity(value: crate::option_chain::qualification::Decimal) -> Result<i32, &'static str> {
    let [lo, mid, hi, flags] = value.bits;
    if mid != 0 || hi != 0 || flags != 0 {
        return Err("Malformed native observation quantity");
    }
    i32::try_from(lo).map_err(|_| "Native option quantity exceeds current checked Int32 contract")
}
/// Constructs the existing 33-key FuturesOptionTickDataV2ReadModel from one frozen
/// native risk calculation. Superseded context or nonpositive sequence/quote
/// returns None, matching current observation eligibility.
///
/// # Errors
/// Invalid metadata, overflow, nonfinite risk or malformed cached identity
/// requires native recovery. Pricing failures emit explicitly unavailable Greeks.
#[allow(clippy::too_many_arguments)]
pub fn snapshot(
    context: &Context,
    numeric: &Numeric,
    calculation: &Calculation,
    date: time::Date,
    current_at: i64,
    revision: i64,
    pricing_update: bool,
) -> Result<Option<Value>, &'static str> {
    let Some(outcome) = option_risk::read(context, numeric, calculation, current_at)? else {
        return Ok(None);
    };
    let q = calculation.option;
    if q.sequence <= 0 {
        return Ok(None);
    }
    let Some(mark) = raw_price(q.bid)
        .zip(raw_price(q.ask))
        .and_then(|(b, a)| midpoint(b, a))
    else {
        return Ok(None);
    };
    if revision <= 0 || !(1..=9999).contains(&date.year()) {
        return Err("Invalid option observation revision or ValueDate");
    }
    let mut deadline = q
        .event
        .checked_add(50_000_000)
        .ok_or("Option observation validity overflow")?
        .min(numeric.valid_until);
    let has_underlying = matches!(&outcome, Outcome::Success(_))
        || matches!(&outcome,Outcome::Failure(f)if f.code=="GreeksCalculationFailed");
    let underlying = if has_underlying {
        deadline = deadline.min(
            calculation
                .underlying
                .event
                .checked_add(50_000_000)
                .ok_or("Underlying observation validity overflow")?,
        );
        raw_price(calculation.underlying.bid)
            .zip(raw_price(calculation.underlying.ask))
            .and_then(|(b, a)| midpoint(b, a))
            .ok_or("Invalid frozen underlying midpoint")?
    } else {
        0.0
    };
    let (values, failure, digest) = match &outcome {
        Outcome::Success(g) => (Some(&g.values), Value::Nil, Value::from(g.digest.clone())),
        Outcome::Failure(f) => (None, Value::from(f.code.clone()), Value::Nil),
    };
    let mut fields = vec![
        context.convention().contract().into(),
        (date.to_julian_day() - 1_721_426).into(),
        q.sequence.into(),
        q.event.rem_euclid(DAY).into(),
        Value::F64(mark),
        Value::F64(decimal::to_double(q.bid).ok_or("Invalid option bid")?),
        Value::F64(decimal::to_double(q.ask).ok_or("Invalid option ask")?),
        quantity(q.bid_size)?.into(),
        quantity(q.ask_size)?.into(),
        Value::F64(values.map_or(0.0, |g| g.volatility)),
        Value::F64(underlying),
        Value::F64(values.map_or(0.0, |g| g.delta)),
        Value::F64(values.map_or(0.0, |g| g.gamma)),
        Value::F64(values.map_or(0.0, |g| g.vega)),
        Value::F64(values.map_or(0.0, |g| g.theta)),
        Value::F64(values.map_or(0.0, |g| g.rho)),
    ];
    // The logical Guid has two independently retained words. It must not be
    // confused with the physical provider generation used by cached readiness.
    let logical = u128::from(numeric.qualification.generation_low)
        | (u128::from(numeric.qualification.generation_high) << 64);
    fields.extend([
        Value::Nil,
        Value::Nil,
        Value::Nil,
        Value::Nil,
        values.is_some().into(),
        super::wire::guid(logical.to_le_bytes()).into(),
    ]);
    fields.extend([
        revision.into(),
        codec::utc_offset(q.event)?,
        codec::utc_offset(calculation.at)?,
        codec::utc_offset(deadline)?,
        failure,
        digest,
        pricing_update.into(),
        false.into(),
        values.map_or(Value::Nil, |g| Value::F64(g.price)),
        (if has_underlying {
            calculation.underlying.sequence
        } else {
            0
        })
        .into(),
        context.pricing_inputs(q.bid, q.ask)?,
    ]);
    Ok(Some(Value::Array(fields)))
}
