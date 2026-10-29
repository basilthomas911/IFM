//! Exact current chain filter and canonical contract identity arithmetic.
//! The provider lookup is separate: a syntactically valid ID is not proof of an
//! unambiguous current instrument or a complete chain.
use super::{Definition, Failure};
use std::{collections::BTreeSet, sync::Arc};
/// Parsed canonical domain identifier, with strike retained in exact nanounits.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Identity {
    /// Canonical product root.
    pub ticker: String,
    /// UTC option/future expiration date, not API ValueDate.
    pub expiration: time::Date,
    /// Future 1, call 2, put 3.
    pub kind: u8,
    /// Futures have no strike.
    pub strike: Option<i64>,
}
fn tail(text: &str) -> bool {
    let Some((&right, number)) = text.as_bytes().split_first() else {
        return true;
    };
    if !matches!(right, b'C' | b'P') || number.is_empty() {
        return false;
    }
    let mut dot = false;
    let mut digits = 0;
    for b in number {
        if *b == b'.' && !dot && digits > 0 {
            dot = true;
            digits = 0;
        } else if b.is_ascii_digit() {
            digits += 1;
        } else {
            return false;
        }
    }
    digits > 0
}
// Decimal.TryParse rounds excess fractional precision to the 96-bit coefficient
// and scale<=28 before the existing nanounit check. Keep that behavior (including
// half-even ties); rejecting every source string with >9 places changes IDs.
fn strike(integer: &str, fraction: &str) -> Result<i64, Failure> {
    let source = format!("{integer}{fraction}");
    let digits = source.trim_start_matches('0');
    if digits.is_empty() {
        return Err(Failure::InvalidDefinition);
    }
    let mut discard = fraction
        .len()
        .saturating_sub(28)
        .max(digits.len().saturating_sub(29));
    let (coefficient, scale) = loop {
        if discard > fraction.len() {
            return Err(Failure::InvalidDefinition);
        }
        let keep = digits.len().saturating_sub(discard);
        let mut coefficient = if keep == 0 {
            0
        } else {
            digits[..keep]
                .parse::<u128>()
                .map_err(|_| Failure::InvalidDefinition)?
        };
        if discard > 0 && discard <= digits.len() {
            let removed = &digits[keep..];
            let first = removed.as_bytes()[0];
            if first > b'5'
                || first == b'5'
                    && (removed[1..].bytes().any(|b| b != b'0') || coefficient % 2 == 1)
            {
                coefficient += 1;
            }
        }
        if coefficient <= ((1u128 << 96) - 1) {
            break (coefficient, fraction.len() - discard);
        }
        discard += 1;
    };
    let nanounits = if scale > 9 {
        let factor = 10u128.pow((scale - 9) as u32);
        if !coefficient.is_multiple_of(factor) {
            return Err(Failure::InvalidDefinition);
        }
        coefficient / factor
    } else {
        coefficient
            .checked_mul(10u128.pow((9 - scale) as u32))
            .ok_or(Failure::InvalidDefinition)?
    };
    i64::try_from(nanounits)
        .ok()
        .filter(|v| *v > 0)
        .ok_or(Failure::InvalidDefinition)
}
/// Parses the existing greedy `SYMBOLyyyyMMdd[CPstrike]` grammar without regex,
/// floating point, locale-dependent decimal parsing or a wall-clock fallback.
pub fn parse(id: &str) -> Result<Identity, Failure> {
    if id.len() < 9 || id.len() > 256 || !id.is_ascii() || !id.as_bytes()[0].is_ascii_uppercase() {
        return Err(Failure::InvalidDefinition);
    }
    let bytes = id.as_bytes();
    let mut selected = None;
    for split in (1..=id.len() - 8).rev() {
        if bytes[..split]
            .iter()
            .all(|b| b.is_ascii_uppercase() || b.is_ascii_digit())
            && bytes[split..split + 8].iter().all(u8::is_ascii_digit)
            && tail(&id[split + 8..])
        {
            selected = Some(split);
            break;
        }
    }
    let split = selected.ok_or(Failure::InvalidDefinition)?;
    let date = &id[split..split + 8];
    let year = date[..4].parse().map_err(|_| Failure::InvalidDefinition)?;
    if !(1..=9999).contains(&year) {
        return Err(Failure::InvalidDefinition);
    }
    let month = date[4..6]
        .parse::<u8>()
        .ok()
        .and_then(|v| time::Month::try_from(v).ok())
        .ok_or(Failure::InvalidDefinition)?;
    let day = date[6..].parse().map_err(|_| Failure::InvalidDefinition)?;
    let expiration =
        time::Date::from_calendar_date(year, month, day).map_err(|_| Failure::InvalidDefinition)?;
    let suffix = &id[split + 8..];
    let (kind, strike) = if suffix.is_empty() {
        (1, None)
    } else {
        let (integer, fraction) = suffix[1..].split_once('.').unwrap_or((&suffix[1..], ""));
        let strike = strike(integer, fraction)?;
        (if suffix.starts_with('C') { 2 } else { 3 }, Some(strike))
    };
    Ok(Identity {
        ticker: id[..split].to_owned(),
        expiration,
        kind,
        strike,
    })
}
/// Formats a provider record using its exact expiry and price. Callers must also
/// resolve the returned ID uniquely against the current catalog before accepting
/// an instrument-to-domain mapping, preserving current round-trip validation.
pub fn format(d: &Definition) -> Result<String, Failure> {
    if !(1..=3).contains(&d.kind) || d.asset.trim().is_empty() {
        return Err(Failure::InvalidDefinition);
    }
    let date = d
        .expiration
        .and_then(|ns| time::OffsetDateTime::from_unix_timestamp((ns / 1_000_000_000) as i64).ok())
        .ok_or(Failure::InvalidDefinition)?
        .date();
    let prefix = format!(
        "{}{:04}{:02}{:02}",
        d.asset,
        date.year(),
        u8::from(date.month()),
        date.day()
    );
    if d.kind == 1 {
        return Ok(prefix);
    }
    let strike = d
        .strike
        .filter(|v| *v > 0)
        .ok_or(Failure::InvalidDefinition)?;
    let integer = strike / 1_000_000_000;
    let fraction = strike % 1_000_000_000;
    let price = if fraction == 0 {
        integer.to_string()
    } else {
        format!("{integer}.{:09}", fraction)
            .trim_end_matches('0')
            .to_owned()
    };
    Ok(format!(
        "{prefix}{}{price}",
        if d.kind == 2 { 'C' } else { 'P' }
    ))
}
/// Exact current contract-resolution predicates; no prefix/nearest-expiry match.
pub fn matches(id: &Identity, d: &Definition) -> bool {
    id.ticker == d.asset
        && id.kind == d.kind
        && d.expiration
            .and_then(|ns| {
                time::OffsetDateTime::from_unix_timestamp((ns / 1_000_000_000) as i64).ok()
            })
            .map(|v| v.date())
            == Some(id.expiration)
        && (id.kind == 1 || id.strike == d.strike)
}
/// Frozen selection request after application validation and root discovery.
pub struct Chain<'a> {
    /// Exact requested maturity.
    pub maturity: time::Date,
    /// Call bit 1, put bit 2.
    pub rights: u8,
    /// Existing policy numeric identity: parent 1, exact future 2, explicit roots 3.
    pub policy: u8,
    /// Requested underlying selector.
    pub underlying: &'a str,
    /// Resolved underlying when the existing policy resolves one.
    pub selected_underlying: Option<&'a Definition>,
}
/// Applies current filtering, stable ordering and instrument/raw-symbol duplicate
/// suppression. Input is a bounded downloaded universe, never an unbounded queue.
pub fn chain(
    request: &Chain<'_>,
    details: impl IntoIterator<Item = Arc<Definition>>,
) -> Vec<Arc<Definition>> {
    let mut candidates: Vec<_> = details
        .into_iter()
        .filter(|d| {
            let right = match d.kind {
                2 => 1,
                3 => 2,
                _ => 0,
            };
            right != 0
                && request.rights & right != 0
                && d.query_maturity == Some(request.maturity)
                && (request.policy == 1
                    || if let Some(selected) = request.selected_underlying {
                        d.underlying == selected.symbol
                            || d.underlying_instrument == selected.instrument
                    } else {
                        d.underlying == request.underlying
                            || d.underlying.starts_with(request.underlying)
                            || d.asset == request.underlying
                    })
        })
        .collect();
    candidates.sort_by(|a, b| {
        a.strike
            .cmp(&b.strike)
            .then(a.kind.cmp(&b.kind))
            .then_with(|| a.symbol.encode_utf16().cmp(b.symbol.encode_utf16()))
    });
    let mut ids = BTreeSet::new();
    let mut symbols = BTreeSet::new();
    candidates.retain(|d| {
        d.strike.is_some() && !ids.contains(&d.instrument) && !symbols.contains(&d.symbol) && {
            ids.insert(d.instrument);
            symbols.insert(d.symbol.clone());
            true
        }
    });
    candidates
}
