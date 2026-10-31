//! Presentation-only option selection, matching the hybrid's one-sided delta
//! and minimum-wing rules. Prices and risk totals remain backend calculations.
//! Decimal strike comparisons deliberately never pass through a binary float.
use ifm_ui_egui::backend::contracts::ExactDecimal;
use std::cmp::Ordering;

#[derive(Clone, Debug)]
pub struct Contract {
    pub id: String,
    pub strike: ExactDecimal,
    pub is_call: bool,
    pub delta: Option<f64>,
    pub stale: bool,
    pub greeks_valid: bool,
    pub selection_valid: bool,
}

/// A fixed scale magnitude holds all .NET decimals without rounding: at most
/// 29 integral plus 28 fractional digits. The extra digit holds a subtraction
/// across zero. This is selection arithmetic, never an order price formatter.
#[derive(Clone, Eq, PartialEq, Ord, PartialOrd)]
struct Magnitude([u8; 58]);
fn parts(value: &ExactDecimal) -> (bool, Magnitude) {
    let negative = value.as_str().starts_with('-');
    let unsigned = value.as_str().trim_start_matches('-');
    let (whole, fraction) = unsigned.split_once('.').unwrap_or((unsigned, ""));
    let mut digits = [0; 58];
    for (i, digit) in whole.trim_start_matches('0').bytes().rev().enumerate() {
        digits[29 - i] = digit - b'0';
    }
    for (i, digit) in fraction.bytes().enumerate() {
        digits[30 + i] = digit - b'0';
    }
    (
        negative && digits.iter().any(|v| *v != 0),
        Magnitude(digits),
    )
}
fn distance(a: &ExactDecimal, b: &ExactDecimal) -> Magnitude {
    let (an, mut a) = parts(a);
    let (bn, mut b) = parts(b);
    let mut result = [0; 58];
    if an != bn {
        let mut carry = 0;
        for i in (0..58).rev() {
            let sum = a.0[i] + b.0[i] + carry;
            result[i] = sum % 10;
            carry = sum / 10;
        }
    } else {
        if a < b {
            std::mem::swap(&mut a, &mut b);
        }
        let mut borrow = 0_i16;
        for i in (0..58).rev() {
            let digit = i16::from(a.0[i]) - i16::from(b.0[i]) - borrow;
            borrow = i16::from(digit < 0);
            result[i] = (digit + borrow * 10) as u8;
        }
    }
    Magnitude(result)
}
pub fn compare(a: &ExactDecimal, b: &ExactDecimal) -> Ordering {
    let (an, a) = parts(a);
    let (bn, b) = parts(b);
    match (an, bn) {
        (true, false) => Ordering::Less,
        (false, true) => Ordering::Greater,
        (true, true) => b.cmp(&a),
        _ => a.cmp(&b),
    }
}

/// The backend delta is a double; using a double here is intentional parity
/// with ShortCandidates. Strike distance and width retain decimal precision.
pub fn short_candidates<'a>(
    contracts: &'a [Contract],
    is_call: bool,
    maximum_delta: f64,
    underlying: Option<&ExactDecimal>,
    allow_frozen: bool,
) -> Vec<&'a Contract> {
    let mut values: Vec<_> = contracts
        .iter()
        .filter(|c| {
            c.is_call == is_call
                && (!c.stale || allow_frozen)
                && (c.greeks_valid || c.selection_valid)
                && c.delta.is_some_and(|d| {
                    d.is_finite()
                        && (if is_call { d > 0.0 } else { d < 0.0 })
                        && d.abs() < 0.5
                        && d.abs() <= maximum_delta
                })
        })
        .collect();
    values.sort_by(|a, b| {
        b.delta
            .unwrap()
            .abs()
            .total_cmp(&a.delta.unwrap().abs())
            .then_with(|| {
                underlying
                    .map(|u| distance(&a.strike, u).cmp(&distance(&b.strike, u)))
                    .unwrap_or(Ordering::Equal)
            })
            .then_with(|| ordinal(&a.id, &b.id))
    });
    values
}
// .NET StringComparer.Ordinal compares UTF-16 code units, including astral IDs.
fn ordinal(a: &str, b: &str) -> Ordering {
    a.encode_utf16().cmp(b.encode_utf16())
}
pub fn wing<'a>(
    contracts: &'a [Contract],
    short: &ExactDecimal,
    width: &ExactDecimal,
    upward: bool,
) -> Option<&'a Contract> {
    let (negative, minimum) = parts(width);
    if negative || minimum.0.iter().all(|d| *d == 0) {
        return None;
    }
    contracts
        .iter()
        .filter(|c| {
            let direction = compare(&c.strike, short);
            (if upward {
                direction == Ordering::Greater
            } else {
                direction == Ordering::Less
            }) && distance(&c.strike, short) >= minimum
        })
        .min_by(|a, b| {
            distance(&a.strike, short)
                .cmp(&distance(&b.strike, short))
                .then_with(|| ordinal(&a.id, &b.id))
        })
}

impl Contract {
    /// Consume canonical schema projections, not display labels or rounded cells.
    pub fn from_backend(row: &serde_json::Value) -> Result<Self, String> {
        let decimal = row["Strike"]
            .as_str()
            .map(str::to_string)
            .unwrap_or_else(|| row["Strike"].to_string());
        let flag = |name: &str| row[name].as_bool().ok_or(format!("Missing option {name}"));
        Ok(Self {
            id: row["ContractId"]
                .as_str()
                .filter(|s| !s.is_empty())
                .ok_or("Missing option identity")?
                .into(),
            strike: ExactDecimal::parse(&decimal)?,
            is_call: flag("IsCall")?,
            delta: row["Delta"].as_f64(),
            stale: flag("IsStale")?,
            greeks_valid: flag("GreeksValid")?,
            selection_valid: flag("SelectionValid")?,
        })
    }
}
