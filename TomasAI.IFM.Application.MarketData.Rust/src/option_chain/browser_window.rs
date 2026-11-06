//! Exact current UI strike selection beneath the native dataset owner. Values
//! are detached copied facts; display membership is separate from buffered
//! subscriptions and never silently clipped to the physical capacity.
use super::qualification::Decimal;
use crate::{
    reference::{decimal, decimal_arithmetic as math},
    runtime::Deadline,
};
use std::{cmp::Ordering, collections::BTreeSet};
use tokio_util::sync::CancellationToken;
/// Copied legacy strike is intentionally retained: the baseline selectors cast
/// this Double to Decimal; substituting a different exact strike changes picks.
pub struct Row<'a> {
    /// Canonical identity; first duplicate wins as in DistinctBy.
    pub contract: &'a str,
    /// Stored legacy strike.
    pub strike: f64,
    /// Stored option type, sorted using CLR ordinal UTF-16 comparison.
    pub kind: &'a str,
}
/// Existing window policy with no database, clock or source ownership.
pub enum Method {
    /// Stored daily deviation/Bollinger width; absent deviation uses nearest 80.
    Bollinger {
        /// Raw selected deviation amount.
        deviation: Option<Decimal>,
        /// Positive finite multiplier.
        multiplier: f64,
    },
    /// Baseline expiry-specific IV estimate, not an assertion of exact delta.
    Implied {
        /// Positive annualized IV.
        volatility: f64,
        /// Positive actual seconds to expiry.
        seconds: f64,
        /// Positive normal score.
        z: f64,
        /// Additional width fraction from zero through one.
        buffer: f64,
    },
}
/// Selected original row indices with the existing displayed bounds/label.
#[derive(Clone)]
pub struct Window {
    /// Ordered original indices, preserving the first duplicate identity.
    pub rows: Vec<usize>,
    /// Selected or theoretical lower bound, excluding required outlier strikes.
    pub lower: Option<Decimal>,
    /// Selected or theoretical upper bound, excluding required outlier strikes.
    pub upper: Option<Decimal>,
    /// Existing method label.
    pub method: String,
}
fn compare(a: Decimal, b: Decimal) -> Ordering {
    a.compare(b).expect("validated native window decimal")
}
fn positive(value: Decimal) -> bool {
    value
        .compare(Decimal::default())
        .is_some_and(Ordering::is_gt)
}
fn absolute(mut value: Decimal) -> Decimal {
    value.bits[3] &= 0x7fffffff;
    value
}
fn interrupted(stop: &CancellationToken, deadline: Deadline) -> Result<(), &'static str> {
    if stop.is_cancelled() || deadline.remaining().is_zero() {
        Err("Native window cancelled/original deadline expired")
    } else {
        Ok(())
    }
}
fn number(result: Option<Decimal>) -> Result<Decimal, &'static str> {
    result.ok_or("Native window decimal overflow")
}
fn values(
    rows: &[Row<'_>],
    stop: &CancellationToken,
    deadline: Deadline,
) -> Result<Vec<(usize, Decimal)>, &'static str> {
    if rows.len() > 100000 {
        return Err("Native window source capacity exceeded");
    }
    let mut ids = BTreeSet::new();
    let mut out = Vec::new();
    for (i, row) in rows.iter().enumerate() {
        if i % 64 == 0 {
            interrupted(stop, deadline)?;
        }
        if row.strike > 0.0 && row.strike.is_finite() && ids.insert(row.contract) {
            out.push((i, number(decimal::from_double(row.strike))?));
        }
    }
    Ok(out)
}
fn ordered(indices: &mut [usize], rows: &[Row<'_>], kind: bool) {
    indices.sort_by(|a, b| {
        rows[*a]
            .strike
            .total_cmp(&rows[*b].strike)
            .then_with(|| {
                if kind {
                    rows[*a]
                        .kind
                        .encode_utf16()
                        .cmp(rows[*b].kind.encode_utf16())
                } else {
                    Ordering::Equal
                }
            })
            .then_with(|| {
                rows[*a]
                    .contract
                    .encode_utf16()
                    .cmp(rows[*b].contract.encode_utf16())
            })
    });
}

const NEAREST_STRIKES: usize = 80;

fn fixed_strike(value: Decimal) -> Option<u128> {
    let [lo, mid, hi, flags] = value.bits;
    let scale = flags >> 16;
    if scale > 9 || flags != scale << 16 {
        return None;
    }
    let coefficient = u128::from(lo) | (u128::from(mid) << 32) | (u128::from(hi) << 64);
    let fixed = coefficient * 10u128.pow(9 - scale);
    // Bounding both positive operands at a common scale also bounds their
    // difference. CLR decimal subtraction cannot round in this case.
    (fixed < 1u128 << 96).then_some(fixed)
}

fn nearest_exact(strikes: &[Decimal], centre: Decimal) -> Option<Vec<Decimal>> {
    let centre = fixed_strike(centre)?;
    if strikes.iter().any(|strike| fixed_strike(*strike).is_none()) {
        return None;
    }
    let pivot = strikes.partition_point(|strike| fixed_strike(*strike).unwrap() < centre);
    let (mut left, mut right) = (pivot, pivot);
    for _ in 0..NEAREST_STRIKES.min(strikes.len()) {
        if left > 0
            && (right == strikes.len()
                || centre - fixed_strike(strikes[left - 1]).unwrap()
                    <= fixed_strike(strikes[right]).unwrap() - centre)
        {
            // On equal distance the existing stable sort picks the lower strike.
            left -= 1;
        } else {
            right += 1;
        }
    }
    Some(strikes[left..right].to_vec())
}

fn nearest_decimal(strikes: &[Decimal], centre: Decimal) -> Result<Vec<Decimal>, &'static str> {
    let mut ranked = strikes
        .iter()
        .map(|s| Ok((*s, absolute(number(math::subtract(*s, centre))?))))
        .collect::<Result<Vec<_>, &'static str>>()?;
    ranked.sort_by(|a, b| compare(a.1, b.1));
    ranked.truncate(NEAREST_STRIKES);
    let mut selected = ranked.into_iter().map(|(s, _)| s).collect::<Vec<_>>();
    selected.sort_by(|a, b| compare(*a, *b));
    Ok(selected)
}

#[cfg(test)]
mod nearest_tests {
    use super::*;

    fn strikes(count: usize) -> Vec<Decimal> {
        (0..count)
            .map(|index| decimal::from_double(2000.0 + index as f64 * 1.25).unwrap())
            .collect()
    }

    #[test]
    fn exact_strike_slice_matches_decimal_ranking_at_ties_and_bounds() {
        let values = strikes(256);
        for price in [100.0, 1999.5, 2000.0, 2063.125, 2160.0, 2318.75, 3000.0] {
            let centre = decimal::from_double(price).unwrap();
            assert_eq!(
                nearest_exact(&values, centre).unwrap(),
                nearest_decimal(&values, centre).unwrap()
            );
        }
        let precise = Decimal {
            bits: [1, 0, 0, 28 << 16],
        };
        assert!(nearest_exact(&[precise], values[0]).is_none());
        assert_eq!(nearest_decimal(&[precise], values[0]).unwrap(), [precise]);
    }

    #[test]
    #[ignore = "Explicit paired nearest-strike kernel benchmark"]
    fn nearest_strike_kernel_before_after_timings() {
        use std::{hint::black_box, time::Instant};
        for count in [128, 1024, 2048] {
            let values = strikes(count);
            let centre = values[count / 2];
            for pair in 1..=5 {
                for exact in if pair % 2 == 1 {
                    [false, true]
                } else {
                    [true, false]
                } {
                    let started = Instant::now();
                    for _ in 0..128 {
                        let result = if exact {
                            nearest_exact(black_box(&values), black_box(centre)).unwrap()
                        } else {
                            nearest_decimal(black_box(&values), black_box(centre)).unwrap()
                        };
                        black_box(result);
                    }
                    println!(
                        "NEAREST_STRIKES pair={pair} count={count} exact={exact} calls=128 elapsed_ns={}",
                        started.elapsed().as_nanos()
                    );
                }
            }
        }
    }
}

/// Selects the baseline nearest/Bollinger/IV window. Checks cancellation while
/// reading the bounded source; no raw reference is held across an await.
///
/// # Errors
/// Invalid scalar input, CLR decimal overflow, capacity or original cancellation.
pub fn select(
    rows: &[Row<'_>],
    centre: Option<Decimal>,
    method: Method,
    required: &BTreeSet<String>,
    stop: &CancellationToken,
    deadline: Deadline,
) -> Result<Window, &'static str> {
    interrupted(stop, deadline)?;
    // Baseline validates each mode's scalar arguments before empty membership.
    let (distance, label, nearest) = match &method {
        Method::Bollinger { multiplier, .. } if !multiplier.is_finite() || *multiplier <= 0.0 => {
            return Err("Invalid native window multiplier");
        }
        Method::Implied {
            volatility,
            seconds,
            z,
            buffer,
        } => {
            if !centre.is_some_and(positive)
                || !volatility.is_finite()
                || *volatility <= 0.0
                || !seconds.is_finite()
                || *seconds <= 0.0
                || !z.is_finite()
                || *z <= 0.0
                || !buffer.is_finite()
                || !(0.0..=1.0).contains(buffer)
            {
                return Err("Invalid native IV window inputs");
            }
            let f = decimal::to_double(centre.unwrap()).ok_or("Invalid native window centre")?;
            (
                Some(number(decimal::from_double(
                    f * volatility * (seconds / (365.0 * 86400.0)).sqrt() * z * (1.0 + buffer),
                ))?),
                "ImpliedVolatility5Delta".to_owned(),
                false,
            )
        }
        Method::Bollinger {
            deviation,
            multiplier,
        } => {
            let nearest = !deviation.is_some_and(positive);
            let label = if nearest {
                "Nearest80StrikesFallback".into()
            } else {
                let rounded = (multiplier * 100.0).round() / 100.0;
                let label = format!("{rounded:.2}");
                format!(
                    "Bollinger{}Sigma",
                    label.trim_end_matches('0').trim_end_matches('.')
                )
            };
            (None, label, nearest)
        }
    };
    let values = values(rows, stop, deadline)?;
    if values.is_empty() {
        return Ok(Window {
            rows: vec![],
            lower: None,
            upper: None,
            method: "Empty".into(),
        });
    }
    let Some(centre) = centre.filter(|c| positive(*c)) else {
        return Ok(Window {
            rows: vec![],
            lower: None,
            upper: None,
            method: "WindowInputsUnavailable".into(),
        });
    };
    let distance = match &method {
        Method::Bollinger {
            deviation: Some(deviation),
            multiplier,
        } if !nearest => Some(number(math::multiply(
            *deviation,
            number(decimal::from_double(*multiplier))?,
        ))?),
        _ => distance,
    };
    let mut strikes = values.iter().map(|(_, s)| *s).collect::<Vec<_>>();
    // IV fallback MinBy preserves first-source order for equidistant strikes.
    let closest = if matches!(method, Method::Implied { .. }) {
        let mut selected = None;
        for strike in &strikes {
            let distance = absolute(number(math::subtract(*strike, centre))?);
            if selected.is_none_or(|(_, prior)| compare(distance, prior).is_lt()) {
                selected = Some((*strike, distance));
            }
        }
        selected.map(|(strike, _)| strike)
    } else {
        None
    };
    strikes.sort_by(|a, b| compare(*a, *b));
    strikes.dedup_by(|a, b| compare(*a, *b).is_eq());
    let (lower, upper, selected) = if nearest {
        let selected = match nearest_exact(&strikes, centre) {
            Some(selected) => selected,
            None => nearest_decimal(&strikes, centre)?,
        };
        (
            selected.first().copied(),
            selected.last().copied(),
            selected,
        )
    } else {
        let distance = distance.unwrap();
        let lower = number(math::subtract(centre, distance))?;
        let upper = number(math::add(centre, distance))?;
        let mut selected = strikes
            .into_iter()
            .filter(|s| !compare(*s, lower).is_lt() && !compare(*s, upper).is_gt())
            .collect::<Vec<_>>();
        if selected.is_empty()
            && let Some(closest) = closest
        {
            selected.push(closest);
        }
        (
            Some(selected.first().copied().unwrap_or(lower)),
            Some(selected.last().copied().unwrap_or(upper)),
            selected,
        )
    };
    let mut indices = values
        .iter()
        .filter(|(i, s)| {
            selected.binary_search_by(|v| compare(*v, *s)).is_ok()
                || required.contains(rows[*i].contract)
        })
        .map(|(i, _)| *i)
        .collect::<Vec<_>>();
    ordered(&mut indices, rows, true);
    interrupted(stop, deadline)?;
    Ok(Window {
        rows: indices,
        lower,
        upper,
        method: label,
    })
}
/// Buffers the displayed range by max(wing, five points, 25 percent), including
/// required outliers. Over-capacity buffering returns the entire display, never
/// a truncated scope; the owner separately rejects over-capacity display demand.
///
/// # Errors
/// Invalid indices/decimals, cancellation, source capacity or arithmetic overflow.
pub fn buffer(
    rows: &[Row<'_>],
    window: &Window,
    wing: Decimal,
    stop: &CancellationToken,
    deadline: Deadline,
) -> Result<Vec<usize>, &'static str> {
    interrupted(stop, deadline)?;
    if window.rows.is_empty() {
        return Ok(vec![]);
    }
    if rows.len() > 100000 {
        return Err("Native window source capacity exceeded");
    }
    let mut ids = BTreeSet::new();
    let mut bounds = Vec::new();
    for i in &window.rows {
        let row = rows.get(*i).ok_or("Invalid native window row")?;
        ids.insert(row.contract);
        bounds.push(number(decimal::from_double(row.strike))?);
    }
    bounds.sort_by(|a, b| compare(*a, *b));
    let lower = bounds[0];
    let upper = *bounds.last().unwrap();
    let range = number(math::subtract(upper, lower))?;
    let margin = [
        Decimal::default(),
        wing,
        decimal::parse("5").unwrap(),
        number(math::multiply(range, decimal::parse("0.25").unwrap()))?,
    ]
    .into_iter()
    .max_by(|a, b| compare(*a, *b))
    .unwrap();
    let low = number(math::subtract(lower, margin))?;
    let high = number(math::add(upper, margin))?;
    let mut seen = BTreeSet::new();
    let mut out = Vec::new();
    // The baseline buffers before DistinctBy. A duplicate whose first row is
    // outside the envelope may therefore use a later in-envelope row.
    for (i, row) in rows.iter().enumerate() {
        if i % 64 == 0 {
            interrupted(stop, deadline)?;
        }
        if row.strike <= 0.0 || !row.strike.is_finite() {
            continue;
        }
        let strike = number(decimal::from_double(row.strike))?;
        if ((!compare(strike, low).is_lt() && !compare(strike, high).is_gt())
            || ids.contains(row.contract))
            && seen.insert(row.contract)
        {
            out.push(i);
        }
    }
    ordered(&mut out, rows, false);
    interrupted(stop, deadline)?;
    Ok(if out.len() <= 2048 {
        out
    } else {
        window.rows.clone()
    })
}
