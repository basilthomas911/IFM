//! Provider-free native resident snapshot validation. Reads never acquire,
//! renew or repair a subscription; every returned ranking row is revalidated.
use crate::{
    messaging::{codec, option_cache::Publication},
    reference::decimal,
};
use rmpv::Value;
use std::{cmp::Ordering, collections::BTreeSet};
/// Complete copied public cache request; UTC values are Unix-relative CLR ticks.
pub struct Request {
    /// Existing lookup scope.
    pub scope: String,
    /// Empty means the currently admitted generation.
    pub generation: [u8; 16],
    /// Authoritative API day number.
    pub date: i32,
    /// Existing Daily/Weekly/Monthly horizon.
    pub horizon: String,
    /// Caller deadline and original UTC offset.
    pub deadline: i64,
    /// UTC offset must be zero.
    pub deadline_offset: i64,
    /// Required exact global configuration hash.
    pub configuration: String,
    /// Optional pinned structure identity in CLR Guid formatting.
    pub strategy: String,
    /// Pinned catalog version, zero only when no identity was supplied.
    pub strategy_version: i64,
    /// Existing accepted root.
    pub root: String,
    /// Required coverage; never authorizes skipping validation of other rows.
    pub required: Vec<String>,
    /// Distinguishes an uninitialized immutable-array request from empty coverage.
    pub required_default: bool,
    /// Reserved overrides remain unsupported.
    pub fund_schema: Option<String>,
    /// Request maximum source age.
    pub age: u32,
    /// Request maximum source skew.
    pub skew: u32,
}
fn result(outcome: i32, reason: &str, version: i64, snapshot: Option<Value>) -> Value {
    Value::Array(vec![
        outcome.into(),
        reason.into(),
        snapshot.unwrap_or(Value::Nil),
        version.into(),
    ])
}
/// Returns the existing four-field result, including its exact reason precedence.
/// No current generation, source timestamp, quote or context is fabricated.
///
/// # Errors
/// An internally malformed snapshot cannot be encoded as an eligible result.
pub fn read(
    request: &Request,
    publication: Option<&Publication>,
    now: i64,
) -> Result<Value, &'static str> {
    if request.scope.trim().is_empty()
        || !(1..=3652058).contains(&request.date)
        || !matches!(request.horizon.as_str(), "Daily" | "Weekly" | "Monthly")
        || request.deadline_offset != 0
        || request.deadline <= now
        || request.configuration.trim().is_empty()
        || request.root != "ES"
        || request.strategy_version < 0
        || !request.strategy.is_empty() && request.strategy_version < 1
        || request.required_default
        || request.required.len() > 4
        || !(1..=5000).contains(&request.age)
        || request.skew > 2000
        || request.required.iter().any(|id| id.trim().is_empty())
        || request.required.iter().collect::<BTreeSet<_>>().len() != request.required.len()
    {
        return Ok(result(3, "InvalidSnapshotRequest", 0, None));
    }
    if request.fund_schema.as_ref().is_some_and(|s| !s.is_empty()) {
        return Ok(result(3, "UnsupportedFundPortfolioParameters", 0, None));
    }
    let Some(publication) = publication.filter(|p| {
        p.scope == request.scope
            && (request.generation == [0; 16] || request.generation == p.generation)
    }) else {
        return Ok(result(1, "ScopeNotReady", 0, None));
    };
    let snapshot = &publication.snapshot;
    let unavailable = |reason| Ok(result(1, reason, publication.version, None));
    if publication.date != request.date
        || publication.configuration != request.configuration
        || snapshot.horizon != request.horizon
    {
        return unavailable("PolicyNotReady");
    }
    if !request.strategy.is_empty() {
        let Some(ranking) = &snapshot.ranking else {
            return unavailable("StrategyNotReady");
        };
        let policy = ranking.parameters.value();
        let fields = policy
            .as_array()
            .ok_or("Malformed native strategy policy")?;
        if fields[3].as_str() != Some(request.strategy.as_str())
            || fields[4].as_i64() != Some(request.strategy_version)
            || ranking.parameters.root() != request.root
        {
            return unavailable("StrategyNotReady");
        }
    }
    if snapshot.until <= now || snapshot.at > now {
        return unavailable("SnapshotExpired");
    }
    if request.required.iter().any(|id| {
        !snapshot
            .instruments
            .iter()
            .any(|v| v.instrument.contract == *id)
    }) {
        return unavailable("CoverageIncomplete");
    }
    let zero = Default::default();
    let positive =
        |v| decimal::to_double(v).is_some() && v.compare(zero) == Some(Ordering::Greater);
    let mut earliest = i64::MAX;
    let mut latest = i64::MIN;
    let mut skew = request.skew;
    for row in &snapshot.instruments {
        let instrument = &row.instrument;
        let (Some(quote), Some(underlying), Some(context), Some(_valuation)) = (
            &instrument.quote,
            &instrument.underlying,
            &instrument.context,
            &row.valuation,
        ) else {
            return unavailable("ValuationUnavailable");
        };
        let contract = context.convention();
        let (age, context_skew, lead) = context.quote_limits();
        let convention = contract.value();
        let fields = convention.as_array().ok_or("Malformed native convention")?;
        // Existing reference qualification already validates timestamp encoding.
        let last_trading = super::convention::timestamp(&fields[13])
            .ok_or("Malformed last trading time")?
            .0;
        if contract.contract() != instrument.contract
            || quote.contract != instrument.contract
            || underlying.contract != contract.underlying()
            || !instrument.strike.is_some_and(positive)
            || instrument.call.is_none()
            || contract.expiration() <= now
            || last_trading <= now
            || !(1..=5000).contains(&age)
            || context_skew > 2000
            || lead > 2000
            || context.generation() != snapshot.generation
            || generation(&quote.quote) != snapshot.generation
            || generation(&underlying.quote) != snapshot.generation
            || context.valid_until() <= now
        {
            return unavailable("PricingContextUnavailable");
        }
        skew = skew.min(context_skew);
        let age = i128::from(request.age.min(age)) * 10000;
        let stale = |q: &super::qualification::Quote| {
            i128::from(q.event) > i128::from(now) + i128::from(lead) * 10000
                || q.received > now
                || i128::from(now) - i128::from(q.event) > age
                || i128::from(now) - i128::from(q.received) > age
        };
        if stale(&quote.quote) || stale(&underlying.quote) {
            return unavailable("QuoteStale");
        }
        let q = quote.quote;
        let u = underlying.quote;
        if q.bid.compare(zero) == Some(Ordering::Less)
            || !positive(q.ask)
            || q.ask.compare(q.bid) == Some(Ordering::Less)
            || !positive(q.bid_size)
            || !positive(q.ask_size)
            || q.event_offset != 0
            || q.received_offset != 0
            || u.event_offset != 0
            || u.received_offset != 0
            || !positive(u.bid)
            || u.ask.compare(u.bid) == Some(Ordering::Less)
            || !positive(u.bid_size)
            || !positive(u.ask_size)
        {
            return unavailable("InvalidQuote");
        }
        earliest = earliest.min(q.event).min(u.event);
        latest = latest.max(q.event).max(u.event);
    }
    if snapshot.instruments.is_empty() {
        return Ok(result(2, "NoEligibleCandidate", publication.version, None));
    }
    if i128::from(latest) - i128::from(earliest) > i128::from(skew) * 10000 {
        return unavailable("IncoherentQuotes");
    }
    Ok(result(0, "", publication.version, Some(snapshot.value()?)))
}
fn generation(quote: &super::qualification::Quote) -> [u8; 16] {
    let mut id = [0; 16];
    id[..8].copy_from_slice(&quote.generation_low.to_le_bytes());
    id[8..].copy_from_slice(&quote.generation_high.to_le_bytes());
    id
}
/// Existing six-field resident status. Expiry is reported without acquiring data.
///
/// # Errors
/// Internally invalid UTC timestamp encoding.
pub fn status(
    scope: &str,
    generation: [u8; 16],
    publication: Option<&Publication>,
    now: i64,
) -> Result<Value, &'static str> {
    let absent = |reason: &str| {
        Value::Array(vec![
            false.into(),
            0.into(),
            Value::Nil,
            Value::Nil,
            0.into(),
            reason.into(),
        ])
    };
    if scope.trim().is_empty() {
        return Ok(absent("InvalidScope"));
    }
    let Some(p) = publication.filter(|p| generation == [0; 16] || generation == p.generation)
    else {
        return Ok(absent("ScopeNotReady"));
    };
    Ok(Value::Array(vec![
        (p.until > now).into(),
        p.version.into(),
        codec::utc_offset(p.snapshot.at)?,
        codec::utc_offset(p.until)?,
        p.snapshot.instruments.len().into(),
        (if p.until <= now {
            "SnapshotExpired"
        } else {
            ""
        })
        .into(),
    ]))
}

impl Request {
    /// Parses bounded copied command fields; native read performs public policy validation.
    ///
    /// # Errors
    /// Malformed field types or command capacities, before any native work.
    pub fn from_command(v: &serde_json::Value) -> Result<Self, &'static str> {
        let text = |key: &str, maximum: usize| {
            v[key]
                .as_str()
                .filter(|s| s.len() <= maximum)
                .map(str::to_owned)
                .ok_or("Invalid cache query string")
        };
        let number = |key: &str| v[key].as_i64().ok_or("Invalid cache query integer");
        let bytes = v["generation"]
            .as_array()
            .filter(|a| a.len() == 16)
            .ok_or("Invalid cache query generation")?;
        let mut generation = [0; 16];
        for (out, byte) in generation.iter_mut().zip(bytes) {
            *out = byte
                .as_u64()
                .and_then(|b| u8::try_from(b).ok())
                .ok_or("Invalid cache query generation")?;
        }
        let required_default = v["required"].is_null();
        let required = if required_default {
            vec![]
        } else {
            v["required"]
                .as_array()
                .filter(|a| a.len() <= 64)
                .ok_or("Invalid cache coverage capacity")?
                .iter()
                .map(|id| {
                    id.as_str()
                        .filter(|s| s.len() <= 1024)
                        .map(str::to_owned)
                        .ok_or("Invalid cache coverage identity")
                })
                .collect::<Result<_, _>>()?
        };
        let fund_schema = if v["fund_schema"].is_null() {
            None
        } else {
            Some(text("fund_schema", 16384)?)
        };
        Ok(Self {
            scope: text("scope", 512)?,
            generation,
            date: i32::try_from(number("date")?).map_err(|_| "Invalid cache date")?,
            horizon: text("horizon", 32)?,
            deadline: number("deadline")?,
            deadline_offset: number("deadline_offset")?,
            configuration: text("configuration", 512)?,
            strategy: text("strategy", 36)?,
            strategy_version: number("strategy_version")?,
            root: text("root", 64)?,
            required,
            required_default,
            fund_schema,
            age: u32::try_from(number("age")?).unwrap_or(0),
            skew: u32::try_from(number("skew")?).unwrap_or(u32::MAX),
        })
    }
}
