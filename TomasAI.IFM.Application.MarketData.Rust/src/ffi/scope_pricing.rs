//! Cold qualified context admission and fixed-width cached pricing reads. No
//! context JSON, provider I/O or numerical calculation occurs on the read path.
use super::{
    runtime::{Status, boundary, instance, valid},
    scopes,
};
use crate::option_chain::{
    coalesced::{Context, Failure, Refresh},
    owner::{PricingConfiguration, PricingRegistration},
    qualification::{Decimal, Quote, Request},
};
use serde_json::Value;
fn number(v: &Value, key: &str) -> Result<u32, Status> {
    u32::try_from(v[key].as_u64().ok_or(Status::InvalidInput)?).map_err(|_| Status::InvalidInput)
}
fn time(v: &Value, key: &str) -> Result<i64, Status> {
    v[key].as_i64().ok_or(Status::InvalidInput)
}
fn boolean(v: &Value, key: &str) -> Result<bool, Status> {
    v[key].as_bool().ok_or(Status::InvalidInput)
}
pub(super) fn risk_requirements(
    v: &Value,
) -> Result<(String, Vec<crate::option_chain::coalesced::RiskRequirement>), Status> {
    let digest = v["expected_digest"]
        .as_str()
        .filter(|d| d.len() == 64 && d.bytes().all(|b| b.is_ascii_hexdigit()))
        .ok_or(Status::InvalidInput)?;
    let rows = v["options"]
        .as_array()
        .filter(|rows| !rows.is_empty() && rows.len() <= 2048)
        .ok_or(Status::InvalidInput)?;
    let updates = rows
        .iter()
        .map(|row| {
            Ok(crate::option_chain::coalesced::RiskRequirement {
                instrument: number(row, "instrument")?,
                context: identity(&row["identity"])?,
                required: boolean(row, "required")?,
            })
        })
        .collect::<Result<Vec<_>, Status>>()?;
    Ok((digest.into(), updates))
}
fn identity(v: &Value) -> Result<u128, Status> {
    let pair = v
        .as_array()
        .filter(|v| v.len() == 2)
        .ok_or(Status::InvalidInput)?;
    Ok(u128::from(pair[0].as_u64().ok_or(Status::InvalidInput)?)
        | (u128::from(pair[1].as_u64().ok_or(Status::InvalidInput)?) << 64))
}
fn decimal(v: &Value) -> Result<Decimal, Status> {
    let words = v
        .as_array()
        .filter(|v| v.len() == 4)
        .ok_or(Status::InvalidInput)?;
    let mut bits = [0; 4];
    for (slot, word) in bits.iter_mut().zip(words) {
        *slot = u32::try_from(word.as_u64().ok_or(Status::InvalidInput)?)
            .map_err(|_| Status::InvalidInput)?;
    }
    Ok(Decimal { bits })
}
fn context(v: &Value) -> Result<Context, Status> {
    let generation = identity(&v["generation"])?;
    let flags = number(v, "flags")?;
    Ok(Context {
        identity: identity(&v["identity"])?,
        underlying: number(v, "underlying")?,
        expiration: time(v, "expiration")?,
        valid_until: time(v, "valid_until")?,
        days_in_year: u16::try_from(number(v, "days_in_year")?)
            .map_err(|_| Status::InvalidInput)?,
        qualification: Request {
            size: 304,
            flags,
            generation_low: generation as u64,
            generation_high: (generation >> 64) as u64,
            maximum_age: number(v, "maximum_age")?,
            maximum_skew: number(v, "maximum_skew")?,
            maximum_lead: number(v, "maximum_lead")?,
            strike: decimal(&v["strike"])?,
            definition_strike: decimal(&v["definition_strike"])?,
            ..Default::default()
        },
        model: crate::pricing::Request {
            forward: 0.0,
            time: 0.0,
            strike: v["strike_double"].as_f64().ok_or(Status::InvalidInput)?,
            rate: v["rate"].as_f64().ok_or(Status::InvalidInput)?,
            call: flags & 8 != 0,
            american: boolean(v, "american")?,
            futures_style: boolean(v, "futures_style")?,
        },
    })
}
fn optional_integer(v: &Value) -> Result<Option<i64>, Status> {
    if v.is_null() {
        Ok(None)
    } else {
        v.as_i64().map(Some).ok_or(Status::InvalidInput)
    }
}
fn optional_date(v: &Value) -> Result<Option<time::Date>, Status> {
    if v.is_null() {
        return Ok(None);
    }
    let n = v
        .as_u64()
        .filter(|value| *value >= 10101 && *value <= 99991231)
        .ok_or(Status::InvalidInput)?;
    time::Date::from_calendar_date(
        (n / 10000) as i32,
        time::Month::try_from((n / 100 % 100) as u8).map_err(|_| Status::InvalidInput)?,
        (n % 100) as u8,
    )
    .map(Some)
    .map_err(|_| Status::InvalidInput)
}
fn manifest(
    v: &Value,
    required: bool,
    budget: &std::sync::Arc<crate::option_chain::manifest::Budget>,
) -> Result<Option<std::sync::Arc<crate::option_chain::manifest::Manifest>>, Status> {
    if v["manifest"].is_null() {
        return if required {
            Err(Status::InvalidInput)
        } else {
            Ok(None)
        };
    }
    use base64::Engine;
    let encoded = v["manifest"]
        .as_str()
        .filter(|text| !text.is_empty() && text.len() <= 5_592_408)
        .ok_or(Status::InvalidInput)?;
    let estimate = encoded.len().div_ceil(4) * 3;
    let mut bytes = Vec::new();
    bytes
        .try_reserve_exact(estimate)
        .map_err(|_| Status::Capacity)?;
    bytes.resize(estimate, 0);
    let length = base64::engine::general_purpose::STANDARD
        .decode_slice(encoded, &mut bytes)
        .map_err(|_| Status::InvalidInput)?;
    bytes.truncate(length);
    budget
        .retain(identity(&v["identity"])?, &bytes)
        .map(Some)
        .map_err(|_| Status::Capacity)
}
pub(super) fn configuration(
    v: &Value,
    budget: &std::sync::Arc<crate::option_chain::manifest::Budget>,
) -> Result<PricingConfiguration, Status> {
    let rows = v["options"]
        .as_array()
        .filter(|rows| !rows.is_empty() && rows.len() <= 2048)
        .ok_or(Status::InvalidInput)?;
    let options = rows
        .iter()
        .map(|row| {
            Ok(PricingRegistration {
                instrument: number(row, "instrument")?,
                required: boolean(row, "required")?,
                context: context(row)?,
                manifest: manifest(row, v["trade_manifests"].as_bool().unwrap_or(false), budget)?,
            })
        })
        .collect::<Result<Vec<_>, Status>>()?;
    let statistics = statistics(&v["statistics"])?;
    Ok(PricingConfiguration {
        digest: v["digest"].as_str().ok_or(Status::InvalidInput)?.into(),
        policy_version: v["policy_version"]
            .as_str()
            .ok_or(Status::InvalidInput)?
            .into(),
        options,
        statistics,
        selection_ms: number(v, "selection_ms")?,
        risk_ms: number(v, "risk_ms")?,
        iv_ms: number(v, "iv_ms")?,
        batch: number(v, "batch")? as usize,
    })
}
pub(super) fn statistics(
    v: &Value,
) -> Result<Vec<(u32, crate::option_chain::statistics::Seed)>, Status> {
    let statistics = if v.is_null() {
        Vec::new()
    } else {
        v.as_array()
            .filter(|rows| rows.len() <= 2048)
            .ok_or(Status::InvalidInput)?
            .iter()
            .map(|row| {
                Ok((
                    number(row, "instrument")?,
                    crate::option_chain::statistics::Seed {
                        volume: optional_integer(&row["volume"])?,
                        volume_date: optional_date(&row["volume_date"])?,
                        open_interest: optional_integer(&row["open_interest"])?,
                        open_interest_date: optional_date(&row["open_interest_date"])?,
                    },
                ))
            })
            .collect::<Result<Vec<_>, Status>>()?
    };
    Ok(statistics)
}
pub(super) struct PreparedRefresh {
    pub(super) expected: String,
    pub(super) next: String,
    pub(super) updates: Vec<Refresh>,
    pub(super) manifests:
        std::collections::BTreeMap<u32, std::sync::Arc<crate::option_chain::manifest::Manifest>>,
}
pub(super) fn refresh(
    v: &Value,
    budget: &std::sync::Arc<crate::option_chain::manifest::Budget>,
) -> Result<PreparedRefresh, Status> {
    let rows = v["options"]
        .as_array()
        .filter(|rows| !rows.is_empty() && rows.len() <= 2048)
        .ok_or(Status::InvalidInput)?;
    let updates = rows
        .iter()
        .map(|row| {
            Ok(Refresh {
                instrument: number(row, "instrument")?,
                expected: identity(&row["expected"])?,
                context: context(row)?,
            })
        })
        .collect::<Result<Vec<_>, Status>>()?;
    let mut manifests = std::collections::BTreeMap::new();
    for row in rows {
        if let Some(manifest) =
            manifest(row, v["trade_manifests"].as_bool().unwrap_or(false), budget)?
            && manifests
                .insert(number(row, "instrument")?, manifest)
                .is_some()
        {
            return Err(Status::InvalidInput);
        }
    }
    Ok(PreparedRefresh {
        expected: v["expected_digest"]
            .as_str()
            .ok_or(Status::InvalidInput)?
            .into(),
        next: v["digest"].as_str().ok_or(Status::InvalidInput)?.into(),
        updates,
        manifests,
    })
}
/// Complete immutable 640-byte cached pricing/provenance record. Quote decimal
/// words and timestamps use the existing 112-byte qualification quote layout.
#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct NativeOptionCalculation {
    /// DTO size (640).
    pub size: u32,
    /// Present=1, selection=2, risk=4, retained IV=8, financial failure=16.
    pub flags: u32,
    /// Immutable current context identity, low/high bits.
    pub context: [u64; 2],
    /// Exact physical source generation, low/high bits.
    pub physical: [u64; 2],
    /// Captured API valuation time (Unix UTC ticks).
    pub at: i64,
    /// Exact numerical year fraction at valuation.
    pub time: f64,
    /// Current calculation's frozen underlying input.
    pub underlying: Quote,
    /// Current calculation's frozen option input.
    pub option: Quote,
    /// Price, volatility, delta, gamma, theta, vega, rho; meaningful by flags.
    pub values: [f64; 7],
    /// Retained IV's immutable context identity, low/high bits.
    pub iv_context: [u64; 2],
    /// Retained IV's original API calculation time.
    pub iv_at: i64,
    /// Retained solved volatility.
    pub iv_volatility: f64,
    /// Retained IV's underlying observation, never replaced by a later quote.
    pub iv_underlying: Quote,
    /// Retained IV's option observation.
    pub iv_option: Quote,
    /// 0 none; 1 underlying absent; 2 context expired; 3 quote; 4 numeric; 5 stale; 6 pending.
    pub failure_kind: u32,
    /// Quote validator or numerical failure code, meaningful by failure kind.
    pub failure_code: u32,
    /// Quote index for quote failures (0 underlying, 1 option).
    pub failure_input: u32,
    /// Zero.
    pub reserved: u32,
    /// SHA-256 of exact refresh-policy version, four little-endian words.
    pub policy: [u64; 4],
}
fn pair(value: u128) -> [u64; 2] {
    [value as u64, (value >> 64) as u64]
}
/// Reads the current prepared selection (risk=0) or full risk (risk=1). Root
/// generation mismatch, missing/retiring/unready scope or no observation returns
/// flags zero. This cannot acquire/renew a lease or return a replacement epoch's
/// cache through a stale reader. Invalid arguments preserve the entire output.
/// # Safety
/// Scope is readable for its bounded length; output is aligned exclusive storage
/// for one 640-byte DTO, independent of the scope bytes.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_scope_calculation_v1(
    handle: u64,
    scope: *const u8,
    length: u32,
    expected_low: u64,
    expected_high: u64,
    instrument: u32,
    risk: u32,
    size: u32,
    output: *mut NativeOptionCalculation,
) -> i32 {
    boundary(|| {
        if !valid(output) || instrument == 0 || risk > 1 {
            return Err(Status::InvalidInput);
        }
        if size != 640 {
            return Err(Status::AbiMismatch);
        }
        // SAFETY: Bounded borrowed scope bytes, never retained by the library.
        let id = unsafe { scopes::name(scope, length)? };
        let instance = instance(handle)?;
        let owner = scopes::owner(&instance.scopes)?;
        let expected = u128::from(expected_low) | (u128::from(expected_high) << 64);
        let mut result = NativeOptionCalculation {
            size: 640,
            ..Default::default()
        };
        if expected == 0 {
            return Err(Status::InvalidInput);
        }
        if let Some(owner) = owner
            && owner.generation() == expected
            && owner
                .status(id)
                .map_err(|_| Status::InternalFailure)?
                .is_some_and(|s| s.phase == crate::dataset::lanes::Phase::Running)
            && let Some(engine) = owner.pricing(id).map_err(|_| Status::InternalFailure)?
        {
            let clock = instance
                .live
                .lock()
                .map_err(|_| Status::InternalFailure)?
                .as_ref()
                .ok_or(Status::InvalidInput)?
                .clock
                .clone();
            let at = clock.utc_ticks().map_err(|_| Status::InternalFailure)?;
            if let Some(value) = engine
                .read(instrument, risk == 1, at)
                .map_err(|_| Status::InternalFailure)?
            {
                result.flags = 1;
                result.policy = engine.policy_identity();
                result.context = pair(value.context);
                result.physical = pair(value.source_generation);
                result.at = value.at;
                result.time = value.time;
                result.underlying = value.underlying;
                result.option = value.option;
                if let Some(selection) = value.selection {
                    result.flags |= 2;
                    result.values[0] = selection.price;
                    result.values[2] = selection.delta;
                }
                if let Some(risk) = value.risk {
                    result.flags |= 4;
                    result.values = [
                        risk.price,
                        risk.volatility,
                        risk.delta,
                        risk.gamma,
                        risk.theta,
                        risk.vega,
                        risk.rho,
                    ];
                }
                if let Some(iv) = value.iv {
                    result.flags |= 8;
                    result.iv_context = pair(iv.context);
                    result.iv_at = iv.at;
                    result.iv_volatility = iv.volatility;
                    result.iv_underlying = iv.underlying;
                    result.iv_option = iv.option;
                }
                if let Some(failure) = value.failure {
                    result.flags |= 16;
                    (
                        result.failure_kind,
                        result.failure_code,
                        result.failure_input,
                    ) = match failure {
                        Failure::UnderlyingUnavailable => (1, 0, 0),
                        Failure::ContextExpired => (2, 0, 0),
                        Failure::Quote(f) => (3, f.failure, f.input),
                        Failure::Numerical(f) => (4, super::pricing::failure_code(f), 0),
                        Failure::Stale => (5, 0, 0),
                        Failure::CalculationPending => (6, 0, 0),
                    };
                }
            }
        }
        // SAFETY: Aligned exclusive destination validated before any access.
        unsafe { output.write(result) };
        Ok(())
    })
}
