//! Native option-universe coverage planning with exact CLR decimal envelopes.
//! Persistent buffering is bounded to 512 semantic keys; source work checks the
//! dataset cancellation token and original deadline, never a managed callback.
use super::{qualification::Decimal, strategy::Parameters};
use crate::{
    reference::{
        calendar::Zone,
        decimal, decimal_arithmetic as arithmetic,
        semantic::{self, Value as Json},
    },
    runtime::Deadline,
};
use std::{
    collections::{BTreeMap, BTreeSet},
    sync::Mutex,
};
use tokio_util::sync::CancellationToken;
/// Source-exact normalized option identity used for coverage planning.
pub struct Definition<'a> {
    /// Canonical contract ID.
    pub contract: &'a str,
    /// Actual provider ticker/root.
    pub root: &'a str,
    /// Actual normalized underlying contract.
    pub underlying: &'a str,
    /// Exact strike in price points.
    pub strike: Decimal,
    /// Provider expiry nanoseconds; absent/invalid values are excluded.
    pub expiration: Option<u64>,
}
/// One complete desired prepared universe. Indices borrow only source data.
pub struct Universe {
    /// Existing semantic complete-scope identity.
    pub scope: String,
    /// Exact global configuration hash.
    pub digest: String,
    /// Selected exchange-calendar expiration date.
    pub expiration: time::Date,
    /// Actual underlying dependency.
    pub underlying: String,
    /// Complete canonically ordered source indices; no silent truncation.
    pub contracts: Vec<usize>,
}
/// Existing all-or-nothing universe planning result.
pub struct ResultSet {
    /// Complete universes, empty for any business rejection.
    pub universes: Vec<Universe>,
    /// Existing reason code, empty on success.
    pub reason: &'static str,
}
fn failed(reason: &'static str) -> ResultSet {
    ResultSet {
        universes: vec![],
        reason,
    }
}
fn positive(n: Decimal) -> bool {
    n.compare(Decimal::default()) == Some(std::cmp::Ordering::Greater)
}
/// Exact existing F * IV * sqrt(DTE/365) * multiplier + maximum wing width.
///
/// # Errors
/// Invalid inputs, decimal overflow or nonfinite numerical coverage.
pub fn distance(
    forward: Decimal,
    volatility: f64,
    dte: i32,
    multiplier: f64,
    wing: Decimal,
) -> Result<Decimal, &'static str> {
    if !positive(forward)
        || dte <= 0
        || !volatility.is_finite()
        || volatility <= 0.0
        || !multiplier.is_finite()
        || !(1.0..=5.0).contains(&multiplier)
        || wing.compare(Decimal::default()).is_none_or(|v| v.is_lt())
    {
        return Err("Invalid volatility coverage inputs");
    }
    let value = decimal::to_double(forward).ok_or("Invalid coverage forward")?
        * volatility
        * (f64::from(dte) / 365.0).sqrt()
        * multiplier;
    let value = decimal::from_double(value).ok_or("Volatility coverage decimal overflow")?;
    arithmetic::add(value, wing).ok_or("Volatility coverage decimal overflow")
}
/// Bounded independently owned planner. Envelopes are copied values, not leases.
#[derive(Default)]
pub struct Planner {
    envelopes: Mutex<BTreeMap<String, (Decimal, Decimal)>>,
}
impl Planner {
    fn envelope(
        &self,
        key: &str,
        forward: Decimal,
        distance: Decimal,
    ) -> Result<(Decimal, Decimal), &'static str> {
        let lower =
            arithmetic::subtract(forward, distance).ok_or("Coverage lower bound overflow")?;
        let upper = arithmetic::add(forward, distance).ok_or("Coverage upper bound overflow")?;
        let mut envelopes = self
            .envelopes
            .lock()
            .map_err(|_| "Native coverage writer interrupted")?;
        if let Some((old_lower, old_upper)) = envelopes.get(key)
            && lower.compare(*old_lower).is_some_and(|v| !v.is_lt())
            && upper.compare(*old_upper).is_some_and(|v| !v.is_gt())
        {
            return Ok((*old_lower, *old_upper));
        }
        if envelopes.len() >= 512 {
            envelopes.clear();
        }
        let buffered = arithmetic::multiply(distance, decimal::parse("1.2").unwrap())
            .ok_or("Coverage buffer overflow")?;
        let result = (
            arithmetic::subtract(forward, buffered).ok_or("Coverage lower bound overflow")?,
            arithmetic::add(forward, buffered).ok_or("Coverage upper bound overflow")?,
        );
        envelopes.insert(key.into(), result);
        Ok(result)
    }
    /// Mirrors complete-scope expiry preference, volatility buffering, optional
    /// per-underlying forward coverage and fallback when the buffer exceeds limits.
    ///
    /// # Errors
    /// Arithmetic, cancellation, capacity or interrupted internal state is explicit.
    #[allow(clippy::too_many_arguments)]
    pub fn plan(
        &self,
        parameters: &Parameters,
        date: time::Date,
        definitions: &[Definition<'_>],
        complete: bool,
        forward: Decimal,
        volatility: f64,
        zone: Zone,
        underlying_prices: Option<&BTreeMap<String, Decimal>>,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<ResultSet, &'static str> {
        if !parameters.enabled || !parameters.biases.iter().any(|b| b.enabled) {
            return Ok(failed("StrategyDisabled"));
        }
        if !complete {
            return Ok(failed("IncompleteDefinitions"));
        }
        if date.to_julian_day() == 1721426
            || !positive(forward)
            || !volatility.is_finite()
            || volatility <= 0.0
            || volatility > 5.0
        {
            return Ok(failed("CoverageInputsUnavailable"));
        }
        if definitions.len() > 100000 {
            return Ok(failed("ConflictingDefinitions"));
        }
        let mut ids = BTreeSet::new();
        let mut groups = BTreeMap::<(time::Date, Vec<u16>), (String, Vec<usize>)>::new();
        let active = parameters
            .biases
            .iter()
            .filter(|b| b.enabled)
            .collect::<Vec<_>>();
        for (index, row) in definitions.iter().enumerate() {
            if stop.is_cancelled() || deadline.remaining().is_zero() {
                return Err("Coverage planning original deadline expired");
            }
            if row.contract.is_empty() || row.contract.len() > 1024 || row.underlying.len() > 1024 {
                return Err("Invalid coverage definition identity");
            }
            if !ids.insert(row.contract) {
                return Ok(failed("ConflictingDefinitions"));
            }
            if row.root != parameters.root() {
                continue;
            }
            let Some(ns) = row
                .expiration
                .filter(|n| *n != u64::MAX && n % 100 == 0 && n / 100 <= 2_534_023_007_999_999_999)
            else {
                continue;
            };
            let Ok(expiration) = zone.calendar_date((ns / 100) as i64) else {
                continue;
            };
            let dte = expiration.to_julian_day() - date.to_julian_day();
            if !active.iter().any(|b| dte >= b.minimum && dte <= b.maximum) {
                continue;
            }
            groups
                .entry((expiration, row.underlying.encode_utf16().collect()))
                .or_insert_with(|| (row.underlying.into(), vec![]))
                .1
                .push(index);
        }
        if groups.is_empty() {
            return Ok(failed("NoEligibleExpiration"));
        }
        let mut selected = BTreeSet::new();
        for bias in &active {
            let best = groups
                .keys()
                .filter(|(expiry, _)| {
                    let dte = expiry.to_julian_day() - date.to_julian_day();
                    dte >= bias.minimum && dte <= bias.maximum
                })
                .min_by_key(|(expiry, underlying)| {
                    (
                        (expiry.to_julian_day() - date.to_julian_day() - bias.preferred).abs(),
                        *expiry,
                        underlying,
                    )
                });
            let Some((date, _)) = best else {
                return Ok(failed("BiasExpirationUnavailable"));
            };
            selected.insert(*date);
        }
        if selected.len() > parameters.maximum_expirations {
            return Ok(failed("ExpirationCapacityExceeded"));
        }
        let mut ordered: Vec<_> = groups.iter().collect();
        ordered.sort_by_key(|((expiry, underlying), _)| {
            (
                active
                    .iter()
                    .map(|b| (expiry.to_julian_day() - date.to_julian_day() - b.preferred).abs())
                    .min()
                    .unwrap(),
                *expiry,
                underlying,
            )
        });
        for ((expiry, _), _) in &ordered {
            if selected.len() < parameters.maximum_expirations {
                selected.insert(*expiry);
            }
        }
        let digest = parameters.hash()?;
        let mut universes = Vec::new();
        let mut count = 0usize;
        for ((expiration, _), (underlying, indices)) in ordered
            .into_iter()
            .filter(|((expiry, _), _)| selected.contains(expiry))
        {
            if stop.is_cancelled() || deadline.remaining().is_zero() {
                return Err("Coverage planning original deadline expired");
            }
            let dte = expiration.to_julian_day() - date.to_julian_day();
            let mut wing = Decimal::default();
            for bias in active
                .iter()
                .filter(|b| dte >= b.minimum && dte <= b.maximum)
            {
                for width in bias
                    .policy
                    .put_widths
                    .iter()
                    .chain(&bias.policy.call_widths)
                {
                    if width.compare(wing).is_some_and(|v| v.is_gt()) {
                        wing = *width;
                    }
                }
            }
            let forward = if let Some(prices) = underlying_prices {
                let Some(price) = prices.get(underlying).copied().filter(|n| positive(*n)) else {
                    return Ok(failed("CoverageInputsUnavailable"));
                };
                price
            } else {
                forward
            };
            let distance = distance(forward, volatility, dte, parameters.multiplier, wing)?;
            let key = semantic::compute(&Json::Object(vec![
                (
                    "ParameterSetId".into(),
                    Json::String(parameters.id().into()),
                ),
                ("Version".into(), Json::Integer(parameters.version())),
                ("Hash".into(), Json::String(digest.clone())),
                ("valueDate".into(), Json::String(date.to_string())),
                ("Expiry".into(), Json::String(expiration.to_string())),
                ("Underlying".into(), Json::String(underlying.clone())),
            ]))?;
            let (lower, upper) = self.envelope(&key, forward, distance)?;
            let inside = |index: &&usize| {
                let strike = definitions[**index].strike;
                strike.compare(lower).is_some_and(|v| !v.is_lt())
                    && strike.compare(upper).is_some_and(|v| !v.is_gt())
            };
            let mut contracts = indices.iter().filter(inside).copied().collect::<Vec<_>>();
            if count + contracts.len() > parameters.maximum_contracts {
                contracts.clear();
                for index in indices {
                    let mut difference = arithmetic::subtract(definitions[*index].strike, forward)
                        .ok_or("Coverage strike difference overflow")?;
                    difference.bits[3] &= 0x7fffffff;
                    if difference
                        .compare(distance)
                        .ok_or("Invalid coverage decimal")?
                        .is_le()
                    {
                        contracts.push(*index);
                    }
                }
                self.envelopes
                    .lock()
                    .map_err(|_| "Native coverage writer interrupted")?
                    .remove(&key);
            }
            if contracts.is_empty() {
                return Ok(failed("StrikeCoverageUnavailable"));
            }
            count += contracts.len();
            if count > parameters.maximum_contracts {
                return Ok(failed("ContractCapacityExceeded"));
            }
            contracts.sort_by(|a, b| {
                definitions[*a]
                    .contract
                    .encode_utf16()
                    .cmp(definitions[*b].contract.encode_utf16())
            });
            let scope = semantic::compute(&Json::Object(vec![
                (
                    "ParameterSetId".into(),
                    Json::String(parameters.id().into()),
                ),
                ("Version".into(), Json::Integer(parameters.version())),
                ("Expiry".into(), Json::String(expiration.to_string())),
                ("Underlying".into(), Json::String(underlying.clone())),
                ("Digest".into(), Json::String(digest.clone())),
                (
                    "Contracts".into(),
                    Json::Array(
                        contracts
                            .iter()
                            .map(|index| Json::String(definitions[*index].contract.into()))
                            .collect(),
                    ),
                ),
            ]))?;
            universes.push(Universe {
                scope,
                digest: digest.clone(),
                expiration: *expiration,
                underlying: underlying.clone(),
                contracts,
            });
        }
        Ok(ResultSet {
            universes,
            reason: "",
        })
    }
}
