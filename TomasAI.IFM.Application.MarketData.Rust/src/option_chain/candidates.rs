//! Deterministic executable delta shortlist and exact protective-wing selection.
//! All quality checks and ranking execute inside Rust; readers consume results.
use super::qualification::{Decimal, Quote};
use crate::{reference::decimal, runtime::Deadline};
use std::collections::BTreeMap;
use tokio_util::sync::CancellationToken;
/// Absolute decimal delta eligibility and target.
#[derive(Clone, Copy)]
pub struct Range {
    /// Inclusive lower bound.
    pub minimum: Decimal,
    /// Preferred absolute delta.
    pub target: Decimal,
    /// Inclusive upper bound.
    pub maximum: Decimal,
}
/// Validated direction-specific executable candidate policy.
pub struct Policy {
    /// Put absolute deltas.
    pub put: Range,
    /// Call absolute deltas.
    pub call: Range,
    /// Exact put protection widths, in price points.
    pub put_widths: Vec<Decimal>,
    /// Exact call protection widths, in price points.
    pub call_widths: Vec<Decimal>,
    /// Maximum quote/underlying age, milliseconds.
    pub age: i32,
    /// Maximum cross-contract and underlying event skew, milliseconds.
    pub skew: i32,
    /// Conservative executable-side minimum size.
    pub size: i32,
    /// Maximum leg bid/ask spread in exact points.
    pub spread: Decimal,
    /// Maximum short candidates for each side before attaching wings.
    pub maximum: usize,
}
/// Borrowed frozen candidate. No source, lease or task ownership is retained.
pub struct Candidate<'a> {
    /// Canonical contract ID, sorted using CLR UTF-16 ordinal order.
    pub id: &'a str,
    /// Exact canonical underlying.
    pub underlying: &'a str,
    /// Exact expiration instant, never a date-only grouping.
    pub expiration: i64,
    /// Exact strike in points.
    pub strike: Decimal,
    /// Call versus put.
    pub call: bool,
    /// Full source-time calculated delta, absent for unusable valuation.
    pub delta: Option<f64>,
    /// Reference validity end, absent if no qualified context exists.
    pub until: Option<i64>,
    /// Frozen option quote.
    pub quote: Option<Quote>,
    /// Frozen underlying quote.
    pub future: Option<Quote>,
}
fn units(value: Decimal) -> Result<i128, &'static str> {
    decimal::scaled_28(value).ok_or("Candidate decimal exceeds native provider range")
}
fn range(value: Range) -> Result<(i128, i128, i128), &'static str> {
    let a = units(value.minimum)?;
    let b = units(value.target)?;
    let c = units(value.maximum)?;
    if a < 0 || a > b || b > c || c > 10i128.pow(28) {
        return Err("Invalid absolute option delta range");
    }
    Ok((a, b, c))
}
impl Policy {
    /// Validates bounded widths and executable-quality limits before any scan.
    ///
    /// # Errors
    /// Invalid policy never produces a partly selected chain.
    pub fn validate(&self) -> Result<(), &'static str> {
        range(self.put)?;
        range(self.call)?;
        if !(1..=5000).contains(&self.age)
            || !(0..=2000).contains(&self.skew)
            || self.size < 1
            || !(1..=32).contains(&self.maximum)
            || self.spread.compare(Decimal::default()) != Some(std::cmp::Ordering::Greater)
        {
            return Err("Invalid strategy bias parameters");
        }
        for widths in [&self.put_widths, &self.call_widths] {
            if widths.len() > 16 {
                return Err("Invalid strategy wing capacity");
            }
            let mut seen = std::collections::BTreeSet::new();
            for width in widths {
                let width = units(*width)?;
                if width <= 0 || width > 10000 * 10i128.pow(28) || !seen.insert(width) {
                    return Err("Invalid strategy wing widths");
                }
            }
        }
        Ok(())
    }
}
/// Returns source indices in existing CLR ordinal contract order. Missing wings
/// are never synthesized. A bounded pass checks cancellation/original deadline.
///
/// # Errors
/// Invalid input, capacity, arithmetic overflow or cancellation is explicit.
pub fn select(
    source: &[Candidate<'_>],
    policy: &Policy,
    now: i64,
    stop: &CancellationToken,
    deadline: Deadline,
) -> Result<Vec<usize>, &'static str> {
    policy.validate()?;
    if source.len() > 2048 {
        return Err("Candidate universe capacity exceeded");
    }
    let mut usable = Vec::new();
    let age = i128::from(policy.age) * 10000;
    let skew = i128::from(policy.skew) * 10000;
    let spread = decimal::scaled_28(policy.spread).unwrap_or(i128::MAX);
    for (index, row) in source.iter().enumerate() {
        if stop.is_cancelled() || deadline.remaining().is_zero() {
            return Err("Candidate selection original deadline expired");
        }
        if row.id.trim().is_empty()
            || row.id.len() > 1024
            || row.underlying.trim().is_empty()
            || row.underlying.len() > 1024
        {
            return Err("Invalid candidate identity");
        }
        let (Some(delta), Some(until), Some(q), Some(f)) =
            (row.delta, row.until, row.quote, row.future)
        else {
            continue;
        };
        if until <= now {
            continue;
        }
        let bid = units(q.bid)?;
        let ask = units(q.ask)?;
        let size = i128::from(policy.size) * 10i128.pow(28);
        if bid <= 0
            || ask < bid
            || ask.checked_sub(bid).ok_or("Candidate spread overflow")? > spread
            || units(q.bid_size)? < size
            || units(q.ask_size)? < size
            || q.received > now
            || f.received > now
        {
            continue;
        }
        let age_ok = [q.event, q.received, f.event, f.received]
            .iter()
            .all(|t| i128::from(now) - i128::from(*t) <= age);
        if !age_ok || (i128::from(q.event) - i128::from(f.event)).abs() > skew {
            continue;
        }
        let absolute = decimal::from_double(delta.abs())
            .ok_or("Candidate delta cannot convert to CLR decimal")?;
        usable.push((
            index,
            decimal::scaled_28(absolute).unwrap_or(i128::MAX),
            q.event.max(f.event),
        ));
    }
    let Some(latest) = usable.iter().map(|v| v.2).max() else {
        return Ok(vec![]);
    };
    usable.retain(|(index, _, _)| {
        let row = &source[*index];
        [row.quote.unwrap().event, row.future.unwrap().event]
            .iter()
            .all(|t| i128::from(latest) - i128::from(*t) <= skew)
    });
    let mut selected = BTreeMap::<Vec<u16>, usize>::new();
    for call in [false, true] {
        let (minimum, target, maximum) = range(if call { policy.call } else { policy.put })?;
        let widths = if call {
            &policy.call_widths
        } else {
            &policy.put_widths
        };
        let mut wings = BTreeMap::new();
        for (index, _, _) in &usable {
            let row = &source[*index];
            if row.call != call {
                continue;
            }
            let key = (units(row.strike)?, row.underlying, row.expiration);
            wings
                .entry(key)
                .and_modify(|old: &mut usize| {
                    if row
                        .id
                        .encode_utf16()
                        .cmp(source[*old].id.encode_utf16())
                        .is_lt()
                    {
                        *old = *index;
                    }
                })
                .or_insert(*index);
        }
        let mut shorts: Vec<_> = usable
            .iter()
            .filter(|(index, delta, _)| {
                source[*index].call == call && *delta >= minimum && *delta <= maximum
            })
            .collect();
        shorts.sort_by(|a, b| {
            (a.1 - target)
                .abs()
                .cmp(&(b.1 - target).abs())
                .then_with(|| {
                    source[a.0]
                        .id
                        .encode_utf16()
                        .cmp(source[b.0].id.encode_utf16())
                })
        });
        for (index, _, _) in shorts.into_iter().take(policy.maximum) {
            let row = &source[*index];
            for width in widths {
                let width = units(*width)?;
                let strike = units(row.strike)?
                    .checked_add(if call { width } else { -width })
                    .ok_or("Candidate strike overflow")?;
                if let Some(wing) = wings.get(&(strike, row.underlying, row.expiration)) {
                    selected
                        .entry(row.id.encode_utf16().collect())
                        .or_insert(*index);
                    selected
                        .entry(source[*wing].id.encode_utf16().collect())
                        .or_insert(*wing);
                }
            }
        }
    }
    Ok(selected.into_values().collect())
}

/// Applies the shortlist to a frozen complete native composition. The returned
/// values are copied DTOs; this function never acquires a provider subscription.
///
/// # Errors
/// Invalid policy or malformed required source metadata remains explicit.
pub fn select_composition(
    source: &[super::composition::model::Valued],
    policy: &Policy,
    now: i64,
    stop: &CancellationToken,
    deadline: Deadline,
) -> Result<Vec<super::composition::model::Valued>, &'static str> {
    let rows: Vec<_> = source
        .iter()
        .map(|row| {
            let item = &row.instrument;
            let context = item.context.as_deref();
            Candidate {
                id: &item.contract,
                underlying: context.map_or("unavailable", |c| c.convention().underlying()),
                expiration: context.map_or(0, |c| c.convention().expiration()),
                strike: item.strike.unwrap_or_default(),
                call: item.call.unwrap_or(false),
                delta: row.valuation.as_ref().map(|g| g.values.delta),
                until: context.map(|c| c.valid_until()),
                quote: item.quote.as_ref().map(|q| q.quote),
                future: item.underlying.as_ref().map(|q| q.quote),
            }
        })
        .collect();
    select(&rows, policy, now, stop, deadline)
        .map(|indices| indices.into_iter().map(|i| source[i].clone()).collect())
}
