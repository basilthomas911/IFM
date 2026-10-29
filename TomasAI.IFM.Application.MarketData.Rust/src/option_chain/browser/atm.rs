//! Exact ATM-IV eligibility/grouping from the current backend estimator. This
//! is a window estimate only; no cached value qualifies executable pricing.
use crate::{
    option_chain::{composition::model::Snapshot, qualification::Decimal},
    reference::{decimal, decimal_arithmetic as math},
};
fn absolute(mut value: Decimal) -> Decimal {
    value.bits[3] &= 0x7fff_ffff;
    value
}
/// Finds the nearest quoted positive strike within 0.5% of forward, choosing
/// the first CLR-ordinal contract for each side and averaging their finite IVs.
///
/// # Errors
/// Invalid exact Decimal arithmetic is explicit rather than rounding a strike.
pub fn estimate(snapshot: &Snapshot, forward: Decimal) -> Result<Option<f64>, String> {
    let rows = snapshot
        .instruments
        .iter()
        .map(|row| Row {
            contract: &row.instrument.contract,
            strike: row.instrument.strike,
            call: row.instrument.call,
            quoted: row.instrument.quote.is_some(),
            valuation: row.valuation.as_ref().map(|risk| risk.values.volatility),
            selection: row
                .instrument
                .selection
                .as_ref()
                .map(|selection| selection.browser_observation()),
        })
        .collect::<Vec<_>>();
    estimate_rows(&rows, forward, snapshot.at)
}
/// Detached candidate fields used by the exact estimator, without a context,
/// callback, resource or newly calculated Greek.
pub struct Row<'a> {
    /// CLR-ordinal contract identity.
    pub contract: &'a str,
    /// Exact nullable strike.
    pub strike: Option<Decimal>,
    /// Nullable call/put group, preserved from the existing contract.
    pub call: Option<bool>,
    /// The existing estimator requires a quote object, not invented prices.
    pub quoted: bool,
    /// Full valuation IV takes precedence, even when ineligible.
    pub valuation: Option<f64>,
    /// Selection IV, calculated Unix ticks, validity-end Unix ticks.
    pub selection: Option<(f64, i64, i64)>,
}
/// Exact copied-row estimator shared by native snapshots and unchanged C#
/// oracle qualification. No clock or provider source is consulted here.
///
/// # Errors
/// Excessive candidate capacity or unrepresentable Decimal arithmetic.
pub fn estimate_rows(
    rows: &[Row<'_>],
    forward: Decimal,
    evaluated: i64,
) -> Result<Option<f64>, String> {
    if rows.len() > 2048 {
        return Err("Native ATM source capacity exceeded".into());
    }
    if forward.compare(Decimal::default()) != Some(std::cmp::Ordering::Greater) {
        return Ok(None);
    }
    let margin = math::multiply(forward, decimal::parse("0.005").expect("constant decimal"))
        .ok_or("ATM IV decimal overflow")?;
    type Side = (Option<bool>, String, f64);
    type StrikeGroup = (Decimal, Decimal, Vec<Side>);
    let mut nearest: Option<StrikeGroup> = None;
    for item in rows {
        let Some(strike) = item.strike.filter(|strike| {
            strike.compare(Decimal::default()) == Some(std::cmp::Ordering::Greater)
        }) else {
            continue;
        };
        if !item.quoted {
            continue;
        }
        let distance = absolute(math::subtract(strike, forward).ok_or("ATM IV decimal overflow")?);
        if distance.compare(margin) == Some(std::cmp::Ordering::Greater) {
            continue;
        }
        let iv = item.valuation.or_else(|| {
            item.selection
                .filter(|(_, at, until)| *until > evaluated && *at <= evaluated)
                .map(|(iv, _, _)| iv)
        });
        let Some(iv) = iv.filter(|iv| iv.is_finite() && *iv > 0.0) else {
            continue;
        };
        let better = nearest
            .as_ref()
            .is_none_or(|(old_distance, old_strike, _)| {
                distance.compare(*old_distance) == Some(std::cmp::Ordering::Less)
                    || (distance.compare(*old_distance) == Some(std::cmp::Ordering::Equal)
                        && strike.compare(*old_strike) == Some(std::cmp::Ordering::Less))
            });
        if better {
            nearest = Some((distance, strike, vec![]));
        }
        if let Some((_, selected, groups)) = &mut nearest
            && strike.compare(*selected) == Some(std::cmp::Ordering::Equal)
        {
            if let Some(group) = groups.iter_mut().find(|group| group.0 == item.call) {
                if item
                    .contract
                    .encode_utf16()
                    .cmp(group.1.encode_utf16())
                    .is_lt()
                {
                    *group = (item.call, item.contract.to_owned(), iv);
                }
            } else {
                groups.push((item.call, item.contract.to_owned(), iv));
            }
        }
    }
    Ok(nearest.map(|(_, _, groups)| {
        groups.iter().map(|group| group.2).sum::<f64>() / groups.len() as f64
    }))
}
