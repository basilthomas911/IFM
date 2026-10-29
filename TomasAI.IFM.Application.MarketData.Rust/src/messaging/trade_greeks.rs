//! Read-only projection of the first durably acknowledged trade calculation.
//! Every field comes from the retained source-time evidence. A later quote,
//! context refresh or calculation can never change this result.
use super::{codec, wire::guid};
use rmpv::Value;
fn array(value: &Value, count: usize) -> Result<&[Value], &'static str> {
    value
        .as_array()
        .filter(|v| v.len() == count)
        .map(Vec::as_slice)
        .ok_or("Malformed frozen trade evidence")
}
fn integer(value: &Value) -> Result<i64, &'static str> {
    value.as_i64().ok_or("Invalid frozen trade integer")
}
/// Existing raw trade, full twenty-four-key Greek transfer, physical identity.
/// Only native-generated, bounded, identity-validated evidence is accepted here.
///
/// # Errors
/// Malformed or conflicting evidence cannot become a usable snapshot.
pub fn read(evidence: &Value, underlying: &str, physical: u128) -> Result<Value, &'static str> {
    if underlying.trim().is_empty() || underlying.len() > 1024 || physical == 0 {
        return Err("Invalid frozen trade query identity");
    }
    let e = array(evidence, 7)?;
    let source = array(&e[0], 12)?;
    if e[6].as_str() != Some("Trade") || source[10].as_str().is_none() {
        return Err("Invalid frozen trade basis");
    }
    let context = if e[1].is_nil() {
        None
    } else {
        Some(array(&e[1], 10)?)
    };
    let success = !e[4].is_nil();
    let g = if success {
        Some(array(&e[4], 9)?)
    } else {
        None
    };
    if success && (context.is_none() || e[2].is_nil() || !e[5].is_nil())
        || !success && e[5].is_nil()
    {
        return Err("Invalid frozen trade outcome");
    }
    if let Some(context) = context {
        let convention = array(&context[0], 29)?;
        if convention[9].as_str() != Some(underlying)
            || convention[1] != source[3]
            || context[4] != source[10]
        {
            return Err("Frozen trade context identity mismatch");
        }
    }
    let trade = Value::Array(vec![
        source[3].clone(),
        source[4].clone(),
        source[5].clone(),
        source[6].clone(),
        source[7].clone(),
        codec::utc_offset(integer(&source[8])? / 100)?,
        codec::utc_offset(integer(&source[9])? / 100)?,
    ]);
    let empty = codec::utc_offset(-621_355_968_000_000_000)?;
    let quote = if success {
        Some(array(&e[2], 9)?)
    } else {
        None
    };
    let midpoint = if let Some(q) = quote {
        // Native provider prices are exact nanounits. Reconstruct with checked
        // decimal parsing; never pass financial values through generic JSON.
        let bid = crate::reference::decimal::parse(q[1].as_str().ok_or("Invalid frozen bid")?)
            .and_then(super::option_pricing::raw_price)
            .ok_or("Invalid frozen bid")?;
        let ask = crate::reference::decimal::parse(q[2].as_str().ok_or("Invalid frozen ask")?)
            .and_then(super::option_pricing::raw_price)
            .ok_or("Invalid frozen ask")?;
        let mut coefficient = (i128::from(bid) + i128::from(ask)) * 5;
        let mut scale = 10;
        while scale > 0 && coefficient % 10 == 0 {
            coefficient /= 10;
            scale -= 1;
        }
        codec::decimal(coefficient, scale)?
    } else {
        Value::Nil
    };
    let greek = |index: usize| g.map_or(Value::Nil, |g| g[index].clone());
    let rate = if success {
        array(&context.ok_or("Missing frozen context")?[2], 8)?[2].clone()
    } else {
        Value::Nil
    };
    let snapshot = Value::Array(vec![
        success.into(),
        false.into(),
        (if success { 0 } else { 14 }).into(),
        2.into(),
        underlying.into(),
        midpoint,
        if context.is_some() {
            source[5].clone()
        } else {
            Value::Nil
        },
        rate,
        greek(7),
        greek(0),
        greek(6),
        greek(1),
        greek(2),
        greek(4),
        greek(3),
        greek(5),
        0.into(),
        quote.map_or(0.into(), |q| q[7].clone()),
        if context.is_some() {
            source[7].clone()
        } else {
            0.into()
        },
        quote.map_or(empty.clone(), |q| q[5].clone()),
        if context.is_some() {
            codec::utc_offset(integer(&source[8])? / 100)?
        } else {
            empty
        },
        e[3].clone(),
        e[5].clone(),
        greek(8),
    ]);
    Ok(Value::Array(vec![
        trade,
        snapshot,
        guid(physical.to_le_bytes()).into(),
    ]))
}
