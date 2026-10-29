//! Existing configured and discovered option DTOs. Currency, exact strike,
//! provider identity and digest formatting stay in Rust rather than the facade.
use super::Definition;
use crate::{messaging::codec, reference::decimal};
use rmpv::Value;
use sha2::{Digest, Sha256};
/// Exact existing option metadata projection. Missing source values remain absent.
/// `configured` preserves the original basic configured-contract constructor;
/// discovered rows carry appended source metadata but no invented review approval.
///
/// # Errors
/// Invalid maturity, missing currency/strike or a lossy legacy double projection.
pub fn project(
    row: &Definition,
    id: &str,
    fallback: Option<&str>,
    underlying: Option<(&str, u16)>,
    configured: bool,
) -> Result<Value, &'static str> {
    if row.query_maturity_invalid {
        return Err("InvalidMaturity");
    }
    let maturity = row
        .query_maturity
        .or_else(|| {
            timestamp(row.expiration).and_then(|ticks| {
                time::OffsetDateTime::from_unix_timestamp(ticks / 10000000)
                    .ok()
                    .map(|t| t.date())
            })
        })
        .ok_or("MissingMaturity")?;
    if !(1..=9999).contains(&maturity.year()) {
        return Err("InvalidMaturity");
    }
    let strike = row.strike.ok_or("MissingStrike")?;
    let exact = decimal::parse(codec::price(strike).as_str().ok_or("InvalidStrike")?)
        .ok_or("InvalidStrike")?;
    let mapped = decimal::to_double(exact).ok_or("InvalidStrike")?;
    if decimal::from_double(mapped).and_then(|d| d.compare(exact))
        != Some(std::cmp::Ordering::Equal)
    {
        return Err("LossyStrike");
    }
    let currency = if row.currency.trim().is_empty() {
        row.settlement_currency.as_str()
    } else {
        row.currency.as_str()
    };
    let currency = if currency.trim().is_empty() {
        fallback.unwrap_or("")
    } else {
        currency
    };
    if currency.trim().is_empty() {
        return Err("MissingCurrency");
    }
    let mut fields = vec![
        id.into(),
        row.symbol.clone().into(),
        row.asset.clone().into(),
        row.symbol.clone().into(),
        "FOP".into(),
        currency.trim().to_uppercase().into(),
        row.exchange.clone().into(),
        row.multiplier.unwrap_or(1).to_string().into(),
        (maturity.to_julian_day() - 1721426).into(),
        Value::F64(mapped),
        (if row.kind == 2 { "Call" } else { "Put" }).into(),
    ];
    fields.extend((11..45).map(|index| {
        if matches!(index, 12 | 13 | 24 | 34 | 35 | 36 | 37 | 40 | 42) {
            Value::from(0)
        } else {
            Value::Nil
        }
    }));
    if configured {
        return Ok(Value::Array(fields));
    }
    let optional = |v: Option<i128>| v.map(|v| v.to_string()).unwrap_or_default();
    let kind = match row.kind {
        1 => "Future",
        2 => "CallOption",
        3 => "PutOption",
        _ => return Err("UnsupportedContractKind"),
    };
    let digest_source = [
        row.dataset.clone(),
        row.symbol.clone(),
        row.publisher.to_string(),
        row.instrument.to_string(),
        row.underlying.clone(),
        row.underlying_instrument.to_string(),
        kind.into(),
        optional(row.strike.map(i128::from)),
        optional(row.multiplier.map(i128::from)),
        optional(row.tick.map(i128::from)),
        optional(row.expiration.map(i128::from)),
        optional(row.activation.map(i128::from)),
    ]
    .join("|");
    let instant = |ns| timestamp(ns).map_or(Ok(Value::Nil), codec::utc_offset);
    fields[11] = codec::decimal_bits(exact)?;
    fields[14] = row.dataset.clone().into();
    fields[15] = row.publisher.into();
    fields[16] = row.instrument.into();
    fields[17] = row.symbol.clone().into();
    fields[18] = instant(row.activation)?;
    fields[19] = format!("{:x}", Sha256::digest(digest_source.as_bytes())).into();
    fields[20] = format!("databento:{}:{}", row.dataset, row.symbol).into();
    fields[21] = instant(row.expiration)?;
    fields[22] = fields[21].clone();
    fields[25] = row
        .multiplier
        .map_or(Value::Nil, |v| Value::from(v.to_string()));
    fields[26] = "1000000000".into();
    fields[27] = row.tick.map_or(Value::Nil, codec::price);
    fields[29] = "DatabentoContractDetail/v1".into();
    fields[33] = underlying.map_or(Value::Nil, |(id, _)| Value::from(id));
    fields[34] = 1.into();
    fields[35] = (if row.kind == 2 { 1 } else { 2 }).into();
    fields[43] = row.underlying_instrument.into();
    fields[44] = underlying.map_or(Value::Nil, |(_, publisher)| Value::from(publisher));
    Ok(Value::Array(fields))
}
fn timestamp(ns: Option<u64>) -> Option<i64> {
    ns.and_then(|v| i64::try_from(v).ok()).map(|v| v / 100)
}

/// Projects and orders a bounded immutable provider query into existing public
/// DTOs. Exact underlying mappings are copied reference data, not source handles.
/// Returns [models, failure code, failed identity] with no partial success.
///
/// # Errors
/// Malformed command/capacity. Individual mapping failures are returned in-band.
pub fn query(
    rows: &[std::sync::Arc<Definition>],
    request: &serde_json::Value,
) -> Result<Value, &'static str> {
    let text = |key: &str, limit: usize| {
        request[key]
            .as_str()
            .filter(|v| v.len() <= limit)
            .ok_or("Invalid option model query string")
    };
    let futures = match request["asset"].as_str() {
        None | Some("options") => false,
        Some("futures") => true,
        _ => return Err("Invalid model query asset"),
    };
    let configured = request["configured"]
        .as_bool()
        .ok_or("Invalid option model query mode")?;
    let fallback = if request["fallback"].is_null() {
        None
    } else {
        Some(text("fallback", 256)?)
    };
    let sort = request["sort"]
        .as_u64()
        .filter(|v| *v <= 2)
        .ok_or("Invalid option model ordering")?;
    if futures && (!configured || sort != 0) {
        return Err("Futures mapping requires one configured result");
    }
    let mappings = request["underlyings"]
        .as_array()
        .filter(|v| v.len() <= 64)
        .ok_or("Invalid option underlying query capacity")?;
    let mut underlyings = std::collections::BTreeMap::new();
    for entry in mappings {
        let fields = entry
            .as_array()
            .filter(|v| v.len() == 3)
            .ok_or("Invalid option underlying mapping")?;
        let provider = fields[0]
            .as_str()
            .filter(|v| !v.is_empty() && v.len() <= 256)
            .ok_or("Invalid option underlying source")?;
        let domain = fields[1]
            .as_str()
            .filter(|v| !v.is_empty() && v.len() <= 256)
            .ok_or("Invalid option underlying identity")?;
        let publisher = fields[2]
            .as_u64()
            .and_then(|v| u16::try_from(v).ok())
            .filter(|v| *v != 0)
            .ok_or("Invalid option underlying publisher")?;
        if let Some(old) = underlyings.insert(provider, (domain, publisher))
            && old != (domain, publisher)
        {
            return Err("Conflicting option underlying mapping");
        }
    }
    let canonical = if configured {
        Some(text("canonical", 1024)?)
    } else {
        None
    };
    let source = if configured {
        Some(text("symbol", 1024)?)
    } else {
        None
    };
    if configured
        && (rows
            .iter()
            .filter(|row| Some(row.symbol.as_str()) == source)
            .count()
            != 1
            || canonical.is_none_or(|id| id.trim().is_empty()))
    {
        return Err("Configured option requires one exact definition");
    }
    let from = request["from"].as_i64();
    let through = request["through"].as_i64();
    if from.is_some() != through.is_some()
        || from
            .zip(through)
            .is_some_and(|(a, b)| a < 0 || b < a || b > 3652058)
    {
        return Err("Invalid option model date range");
    }
    let mut output = Vec::new();
    let mut encoded = 0usize;
    for row in rows {
        if configured && Some(row.symbol.as_str()) != source {
            continue;
        }
        if !configured && !matches!(row.kind, 2 | 3) {
            continue;
        }
        if let Some((from, through)) = from.zip(through) {
            let Some(day) = row
                .query_maturity
                .map(|d| i64::from(d.to_julian_day() - 1721426))
            else {
                continue;
            };
            if day < from || day > through {
                continue;
            }
        }
        let id = canonical.unwrap_or(&row.symbol);
        let model = match if futures {
            future(
                row,
                id,
                fallback,
                request["on_the_run"]
                    .as_bool()
                    .ok_or("Invalid futures flag")?,
                request["rollover"]
                    .as_bool()
                    .ok_or("Invalid futures flag")?,
            )
        } else {
            project(
                row,
                id,
                fallback,
                underlyings.get(row.underlying.as_str()).copied(),
                configured,
            )
        } {
            Ok(model) => model,
            Err(code) => return Ok(Value::Array(vec![Value::Nil, code.into(), id.into()])),
        };
        encoded = encoded
            .checked_add(codec::encode(&model)?.len())
            .filter(|v| *v <= 4 * 1024 * 1024 - 1024)
            .ok_or("Option model encoded capacity exceeded")?;
        if output.len() == 4096 {
            return Err("Option model result capacity exceeded");
        }
        output.push(model);
    }
    if sort != 0 {
        output.sort_by(|a, b| {
            let a = a.as_array().expect("constructed option DTO");
            let b = b.as_array().expect("constructed option DTO");
            (if sort == 2 {
                a[8].as_i64().cmp(&b[8].as_i64())
            } else {
                std::cmp::Ordering::Equal
            })
            .then_with(|| {
                a[9].as_f64()
                    .expect("mapped finite strike")
                    .partial_cmp(&b[9].as_f64().expect("mapped finite strike"))
                    .expect("finite strike")
            })
            .then_with(|| {
                a[10]
                    .as_str()
                    .expect("mapped option type")
                    .encode_utf16()
                    .cmp(b[10].as_str().expect("mapped option type").encode_utf16())
            })
            .then_with(|| {
                a[0].as_str()
                    .expect("mapped contract")
                    .encode_utf16()
                    .cmp(b[0].as_str().expect("mapped contract").encode_utf16())
            })
        });
    }
    Ok(Value::Array(vec![
        Value::Array(output),
        Value::Nil,
        Value::Nil,
    ]))
}

/// Existing configured futures DTO, independent of current-contract eligibility.
/// Explicit flags are copied configuration; source fields are never guessed.
///
/// # Errors
/// Invalid/missing maturity or missing contract/settlement/fallback currency.
pub fn future(
    row: &Definition,
    id: &str,
    fallback: Option<&str>,
    on_the_run: bool,
    rollover: bool,
) -> Result<Value, &'static str> {
    if row.query_maturity_invalid {
        return Err("InvalidMaturity");
    }
    let maturity = row
        .query_maturity
        .or_else(|| {
            timestamp(row.expiration).and_then(|ticks| {
                time::OffsetDateTime::from_unix_timestamp(ticks / 10000000)
                    .ok()
                    .map(|t| t.date())
            })
        })
        .ok_or("MissingMaturity")?;
    if !(1..=9999).contains(&maturity.year()) {
        return Err("InvalidMaturity");
    }
    let currency = if row.currency.trim().is_empty() {
        row.settlement_currency.as_str()
    } else {
        row.currency.as_str()
    };
    let currency = if currency.trim().is_empty() {
        fallback.unwrap_or("")
    } else {
        currency
    };
    if currency.trim().is_empty() {
        return Err("MissingCurrency");
    }
    let mut fields = vec![
        id.into(),
        row.symbol.clone().into(),
        row.asset.clone().into(),
        row.symbol.clone().into(),
        "FUT".into(),
        currency.trim().to_uppercase().into(),
        row.exchange.clone().into(),
        row.multiplier.unwrap_or(1).to_string().into(),
        (maturity.to_julian_day() - 1721426).into(),
        on_the_run.into(),
        rollover.into(),
    ];
    fields.extend((11..32).map(|key| {
        if matches!(key, 11 | 12 | 23) {
            Value::from(0)
        } else {
            Value::Nil
        }
    }));
    Ok(Value::Array(fields))
}
