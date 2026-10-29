//! Full quote-Greeks query projection from one frozen native calculation. No
//! solver, current-book substitution, reference acquisition or managed helper.
use super::{
    codec,
    option_evidence::{Outcome, Quote},
    option_pricing::raw_price,
    option_risk,
};
use crate::option_chain::{
    coalesced::{Calculation, Context as Numeric},
    context::Context,
};
use rmpv::Value;

fn midpoint(quote: crate::option_chain::qualification::Quote) -> Result<Value, &'static str> {
    let bid = raw_price(quote.bid).ok_or("Invalid frozen Greek bid")?;
    let ask = raw_price(quote.ask).ok_or("Invalid frozen Greek ask")?;
    let mut coefficient = (i128::from(bid) + i128::from(ask)) * 5;
    let mut scale = 10;
    while scale > 0 && coefficient % 10 == 0 {
        coefficient /= 10;
        scale -= 1;
    }
    codec::decimal(coefficient, scale)
}
/// Existing quote plus a twenty-four-field transfer representation of
/// OptionGreeksSnapshot, followed by the original physical Guid. The thin C#
/// adapter materializes the public readonly struct without financial arithmetic.
/// Prerequisite failure preserves absent numeric fields and the original reason.
///
/// # Errors
/// Invalid immutable metadata, timestamps or numerical values are explicit.
pub fn read(
    context: &Context,
    numeric: &Numeric,
    calculation: &Calculation,
    current_at: i64,
) -> Result<Option<Value>, &'static str> {
    let Some(outcome) = option_risk::read(context, numeric, calculation, current_at)? else {
        return Ok(None);
    };
    let underlying = context
        .convention()
        .value()
        .as_array()
        .and_then(|v| v.get(9))
        .and_then(Value::as_str)
        .ok_or("Missing Greek underlying identity")?
        .to_owned();
    let option = Quote {
        contract: context.convention().contract().into(),
        quote: calculation.option,
    };
    let failure = outcome
        .value()?
        .as_array()
        .ok_or("Malformed Greek outcome")?[1]
        .clone();
    let prerequisite = matches!(&outcome,Outcome::Failure(f) if f.code!="GreeksCalculationFailed");
    let values = if let Outcome::Success(g) = &outcome {
        Some(g)
    } else {
        None
    };
    let empty_time = codec::utc_offset(-621_355_968_000_000_000)?;
    let number = |f: fn(&crate::pricing::Values) -> f64| {
        values.map_or(Value::Nil, |g| Value::F64(f(&g.values)))
    };
    let snapshot = Value::Array(vec![
        values.is_some().into(),
        false.into(),
        (if prerequisite { 14 } else { 0 }).into(),
        (if prerequisite { 0 } else { 1 }).into(),
        underlying.into(),
        if prerequisite {
            Value::Nil
        } else {
            midpoint(calculation.underlying)?
        },
        if prerequisite {
            Value::Nil
        } else {
            midpoint(calculation.option)?
        },
        if prerequisite {
            Value::Nil
        } else {
            Value::F64(context.annual_rate())
        },
        values.map_or(Value::Nil, |g| Value::F64(g.time)),
        number(|v| v.volatility),
        number(|v| v.price),
        number(|v| v.delta),
        number(|v| v.gamma),
        number(|v| v.vega),
        number(|v| v.theta),
        number(|v| v.rho),
        0.into(),
        (if prerequisite {
            0
        } else {
            calculation.underlying.sequence
        })
        .into(),
        (if prerequisite {
            0
        } else {
            calculation.option.sequence
        })
        .into(),
        if prerequisite {
            empty_time.clone()
        } else {
            codec::utc_offset(calculation.underlying.event)?
        },
        if prerequisite {
            empty_time
        } else {
            codec::utc_offset(calculation.option.event)?
        },
        codec::utc_offset(calculation.at)?,
        failure,
        values.map_or(Value::Nil, |g| g.digest.clone().into()),
    ]);
    Ok(Some(Value::Array(vec![
        option.value()?,
        snapshot,
        super::wire::guid(calculation.source_generation.to_le_bytes()).into(),
    ])))
}
