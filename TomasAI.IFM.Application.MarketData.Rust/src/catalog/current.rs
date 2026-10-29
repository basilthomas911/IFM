//! Current-future/term-structure selection previously performed by the managed
//! native resolver. This runs over detached definitions, never dataset resources.
use super::Definition;
use rmpv::Value;
use std::{collections::BTreeSet, sync::Arc};

/// Existing result DTOs or one explicit application failure to materialize.
pub struct Outcome {
    /// Existing thirty-two-key FuturesContractV3ReadModel values.
    pub contracts: Vec<Value>,
    /// Stable structural/application failure code, absent on success.
    pub failure: Option<&'static str>,
    /// Affected contract ID, or normalized symbol for a not-found result.
    pub identity: String,
}
impl Outcome {
    /// Three-key copied query transfer. No C# filtering or financial mapping is needed.
    pub fn value(&self) -> Value {
        Value::Array(vec![
            if self.failure.is_some() {
                Value::Nil
            } else {
                Value::Array(self.contracts.clone())
            },
            self.failure.map_or(Value::Nil, Into::into),
            self.identity.clone().into(),
        ])
    }
}
fn failed(code: &'static str, id: &str) -> Outcome {
    Outcome {
        contracts: vec![],
        failure: Some(code),
        identity: id.into(),
    }
}
/// Preserves first-per-(maturity,symbol), UTC maturity fallback, ES quarterly
/// filtering, UTF-16 symbol ordering, currency fallback and OnTheRun/Rollover flags.
///
/// # Errors
/// Invalid query bounds or an unsupported out-of-range expiry are explicit.
pub fn select(
    rows: &[Arc<Definition>],
    symbol: &str,
    date: time::Date,
    count: usize,
    fallback: Option<&str>,
) -> Result<Outcome, &'static str> {
    if symbol.trim().is_empty()
        || symbol.len() > 256
        || count == 0
        || count > 100_000
        || rows.len() > 100_000
        || !(1..=9999).contains(&date.year())
    {
        return Err("Invalid native current-contract request");
    }
    let symbol = symbol.trim().to_uppercase();
    let eligible = date
        .next_day()
        .ok_or("Current-contract ValueDate overflow")?;
    if rows.iter().any(|d| d.query_maturity_invalid) {
        return Ok(failed("InvalidMaturity", &symbol));
    }
    let mut unique = BTreeSet::new();
    let mut selected = Vec::new();
    for row in rows {
        if row.kind != 1 {
            continue;
        }
        let maturity = if let Some(maturity) = row.query_maturity {
            Some(maturity)
        } else if let Some(ns) = row.expiration {
            Some(
                time::OffsetDateTime::from_unix_timestamp(
                    i64::try_from(ns / 1_000_000_000).map_err(|_| "Invalid native expiry")?,
                )
                .map_err(|_| "Invalid native expiry")?
                .date(),
            )
        } else {
            None
        };
        let Some(maturity) = maturity else {
            continue;
        };
        if maturity < eligible
            || symbol == "ES"
                && !matches!(
                    maturity.month(),
                    time::Month::March
                        | time::Month::June
                        | time::Month::September
                        | time::Month::December
                )
        {
            continue;
        }
        if unique.insert((maturity, row.symbol.as_str())) {
            selected.push((maturity, row));
        }
    }
    selected.sort_by(|(a, ar), (b, br)| {
        a.cmp(b)
            .then_with(|| ar.symbol.encode_utf16().cmp(br.symbol.encode_utf16()))
    });
    if selected.len() < count {
        return Ok(failed("NotFound", &symbol));
    }
    // The copied query has an independent output cap. Huge valid count requests
    // never allocate a provider-sized DTO graph on a caller thread.
    if count > 4096 {
        return Err("Native current-contract result capacity exceeded");
    }
    let mut contracts = Vec::with_capacity(count);
    let mut encoded_bytes = 0usize;
    for (index, (maturity, row)) in selected.into_iter().take(count).enumerate() {
        let id = format!(
            "{symbol}{:04}{:02}{:02}",
            maturity.year(),
            maturity.month() as u8,
            maturity.day()
        );
        let currency = if !row.currency.trim().is_empty() {
            Some(row.currency.as_str())
        } else if !row.settlement_currency.trim().is_empty() {
            Some(row.settlement_currency.as_str())
        } else {
            fallback
        };
        let Some(currency) = currency.filter(|c| !c.trim().is_empty()) else {
            return Ok(failed("MissingCurrency", &id));
        };
        let mut fields = vec![
            id.into(),
            row.symbol.clone().into(),
            symbol.clone().into(),
            row.symbol.clone().into(),
            "FUT".into(),
            currency.trim().to_uppercase().into(),
            row.exchange.clone().into(),
            row.multiplier.unwrap_or(1).to_string().into(),
            (maturity.to_julian_day() - 1721426).into(),
            (index == 0).into(),
            true.into(),
        ];
        // Current resolver construction leaves appended reviewed-reference fields
        // absent/default. Preserve all keys rather than inventing review evidence.
        fields.extend((11..32).map(|key| {
            if matches!(key, 11 | 12 | 23) {
                Value::from(0)
            } else {
                Value::Nil
            }
        }));
        let value = Value::Array(fields);
        encoded_bytes = encoded_bytes
            .checked_add(crate::messaging::codec::encode(&value)?.len())
            .filter(|n| *n <= 2 * 1024 * 1024 - 1024)
            .ok_or("Native current-contract encoded capacity exceeded")?;
        contracts.push(value);
    }
    Ok(Outcome {
        contracts,
        failure: None,
        identity: symbol,
    })
}
