//! Complete immutable strategy policy identity and executable selection rules.
//! Imported data is validated once; no managed policy evaluator or renewal is
//! retained by this native configuration.
use super::candidates::{Policy, Range};
use crate::{
    messaging::codec,
    reference::{decimal, semantic::Value as Json},
};
use rmpv::Value;
use sha2::{Digest, Sha256};
const NAMES: [&str; 13] = [
    "ParameterSetId",
    "Version",
    "Name",
    "StrategyDefinitionId",
    "StrategyDefinitionVersion",
    "InstrumentRoot",
    "Environment",
    "Enabled",
    "BiasRows",
    "CacheRangeMultiplier",
    "MaximumContracts",
    "MaximumExpirations",
    "SchemaVersion",
];
const BIAS: [&str; 18] = [
    "MarketBias",
    "Enabled",
    "MinimumDte",
    "MaximumDte",
    "PreferredDte",
    "PutDelta",
    "CallDelta",
    "PutWingWidths",
    "CallWingWidths",
    "TargetNetDelta",
    "NetDeltaTolerance",
    "MaximumQuoteAgeMilliseconds",
    "MaximumQuoteSkewMilliseconds",
    "MinimumNetCredit",
    "MaximumNetDebit",
    "MinimumQuoteSize",
    "MaximumLegSpreadPoints",
    "MaximumCandidatesPerSide",
];
fn array(value: &Value, count: usize) -> Result<&[Value], &'static str> {
    value
        .as_array()
        .filter(|v| v.len() == count)
        .map(Vec::as_slice)
        .ok_or("Malformed strategy policy")
}
fn int(value: &Value) -> Result<i64, &'static str> {
    value
        .as_i64()
        .filter(|n| i32::try_from(*n).is_ok())
        .ok_or("Invalid strategy integer")
}
fn boolean(value: &Value) -> Result<bool, &'static str> {
    value.as_bool().ok_or("Invalid strategy boolean")
}
fn number(value: &Value) -> Result<super::qualification::Decimal, &'static str> {
    decimal::parse(value.as_str().ok_or("Invalid strategy decimal")?)
        .ok_or("Invalid strategy decimal")
}
fn bounded(value: &Value) -> Result<&str, &'static str> {
    value
        .as_str()
        .filter(|s| s.len() <= 4096)
        .ok_or("Invalid strategy string")
}
fn guid(value: &Value) -> Result<bool, &'static str> {
    let value = bounded(value)?;
    if value.len() != 36
        || value.bytes().enumerate().any(|(i, c)| {
            if [8, 13, 18, 23].contains(&i) {
                c != b'-'
            } else {
                !c.is_ascii_hexdigit()
            }
        })
    {
        return Err("Invalid strategy Guid");
    }
    Ok(value != "00000000-0000-0000-0000-000000000000")
}
fn range(value: &Value) -> Result<Range, &'static str> {
    let v = array(value, 3)?;
    Ok(Range {
        minimum: number(&v[0])?,
        target: number(&v[1])?,
        maximum: number(&v[2])?,
    })
}
fn widths(value: &Value) -> Result<Vec<super::qualification::Decimal>, &'static str> {
    value
        .as_array()
        .filter(|v| v.len() <= 16)
        .ok_or("Invalid strategy wings")?
        .iter()
        .map(number)
        .collect()
}
/// Validated native bias metadata and complete execution-quality rules.
pub struct Bias {
    /// Neutral=0, Bullish=1, Bearish=2.
    pub direction: u8,
    /// Background universe eligibility.
    pub enabled: bool,
    /// Inclusive minimum calendar DTE.
    pub minimum: i32,
    /// Inclusive maximum calendar DTE.
    pub maximum: i32,
    /// Preferred calendar DTE.
    pub preferred: i32,
    /// Exact executable selection policy.
    pub policy: Policy,
}
impl Bias {
    fn parse(value: &Value) -> Result<Self, &'static str> {
        let v = array(value, 18)?;
        let direction = int(&v[0])?;
        let minimum = int(&v[2])?;
        let maximum = int(&v[3])?;
        let preferred = int(&v[4])?;
        let compare = |index: usize, low: &str, high: &str| -> Result<bool, &'static str> {
            let n = number(&v[index])?;
            Ok(n.compare(decimal::parse(low).unwrap())
                .is_some_and(|x| !x.is_lt())
                && n.compare(decimal::parse(high).unwrap())
                    .is_some_and(|x| !x.is_gt()))
        };
        if !(0..=2).contains(&direction)
            || minimum < 1
            || maximum < minimum
            || maximum > 730
            || preferred < minimum
            || preferred > maximum
            || !compare(9, "-4", "4")?
            || !compare(10, "0", "4")?
            || number(&v[13])?
                .compare(Default::default())
                .is_none_or(|v| v.is_lt())
            || !v[14].is_nil()
                && number(&v[14])?.compare(Default::default()) != Some(std::cmp::Ordering::Greater)
        {
            return Err("Invalid strategy bias parameters");
        }
        let i32_value =
            |i: usize| i32::try_from(int(&v[i])?).map_err(|_| "Invalid strategy integer");
        let policy = Policy {
            put: range(&v[5])?,
            call: range(&v[6])?,
            put_widths: widths(&v[7])?,
            call_widths: widths(&v[8])?,
            age: i32_value(11)?,
            skew: i32_value(12)?,
            size: i32_value(15)?,
            spread: number(&v[16])?,
            maximum: usize::try_from(int(&v[17])?).map_err(|_| "Invalid strategy shortlist")?,
        };
        policy.validate()?;
        Ok(Self {
            direction: direction as u8,
            enabled: boolean(&v[1])?,
            minimum: minimum as i32,
            maximum: maximum as i32,
            preferred: preferred as i32,
            policy,
        })
    }
    /// Existing public direction spelling.
    pub fn name(&self) -> &'static str {
        match self.direction {
            0 => "Neutral",
            1 => "Bullish",
            _ => "Bearish",
        }
    }
}
/// Complete native strategy policy with original exact MessagePack identity.
pub struct Parameters {
    value: Value,
    /// Three explicitly validated directions, in configured order.
    pub biases: Vec<Bias>,
    /// Native physical coverage multiplier.
    pub multiplier: f64,
    /// Maximum complete option contracts.
    pub maximum_contracts: usize,
    /// Maximum distinct expiry dates.
    pub maximum_expirations: usize,
    /// Whether background preparation is desired.
    pub enabled: bool,
}
impl Parameters {
    /// Validates all current thirteen-key policy fields before accepting intent.
    ///
    /// # Errors
    /// Invalid identity, schema, business bounds or excessive encoded input.
    pub fn new(value: Value) -> Result<Self, &'static str> {
        if codec::encode(&value)?.len() > 64 * 1024 {
            return Err("Strategy policy capacity exceeded");
        }
        let v = array(&value, 13)?;
        let enabled = boolean(&v[7])?;
        let definition = guid(&v[3])?;
        let version = int(&v[4])?;
        let multiplier = v[9].as_f64().ok_or("Invalid strategy multiplier")?;
        if !guid(&v[0])?
            || int(&v[1])? < 1
            || bounded(&v[2])?.trim().is_empty()
            || bounded(&v[5])? != "ES"
            || !matches!(bounded(&v[6])?, "Development" | "Paper" | "Production")
            || !multiplier.is_finite()
            || !(1.0..=5.0).contains(&multiplier)
            || !(4..=2048).contains(&int(&v[10])?)
            || !(1..=16).contains(&int(&v[11])?)
            || int(&v[12])? != 1
            || enabled && (!definition || version < 1)
            || !enabled
                && if definition {
                    version < 1
                } else {
                    version != 0
                }
        {
            return Err("Invalid global strategy option-chain parameter set");
        }
        let biases = array(&v[8], 3)?
            .iter()
            .map(Bias::parse)
            .collect::<Result<Vec<_>, _>>()?;
        let directions: std::collections::BTreeSet<_> =
            biases.iter().map(|b| b.direction).collect();
        if directions.len() != 3 {
            return Err("All strategy biases are required");
        }
        let maximum_contracts = int(&v[10])? as usize;
        let maximum_expirations = int(&v[11])? as usize;
        Ok(Self {
            value,
            biases,
            multiplier,
            maximum_contracts,
            maximum_expirations,
            enabled,
        })
    }
    /// Canonical global parameter identity supplied by configuration.
    pub fn id(&self) -> &str {
        self.value.as_array().unwrap()[0].as_str().unwrap()
    }
    /// Immutable published configuration version.
    pub fn version(&self) -> i64 {
        self.value.as_array().unwrap()[1].as_i64().unwrap()
    }
    /// Exact configured provider product root.
    pub fn root(&self) -> &str {
        self.value.as_array().unwrap()[5].as_str().unwrap()
    }
    /// Copies existing typed policy DTO data, never resource handles.
    pub fn value(&self) -> Value {
        self.value.clone()
    }
    /// Exact existing SHA256 over ordinary MessagePack, not JSON or CLR names.
    ///
    /// # Errors
    /// Encoding failure is explicit.
    pub fn hash(&self) -> Result<String, &'static str> {
        Ok(format!("{:x}", Sha256::digest(codec::encode(&self.value)?)))
    }
    /// Current typed canonical pricing-hash graph.
    ///
    /// # Errors
    /// Malformed retained scalar data is explicit.
    pub fn semantic(&self) -> Result<Json, &'static str> {
        let v = array(&self.value, 13)?;
        let mut fields = Vec::new();
        for (i, name) in NAMES.iter().enumerate() {
            let value = match i {
                0 | 2 | 3 | 5 | 6 => Json::String(bounded(&v[i])?.into()),
                1 | 4 | 10 | 11 | 12 => Json::Integer(int(&v[i])?),
                7 => Json::Bool(boolean(&v[i])?),
                9 => Json::Double(self.multiplier),
                8 => Json::Array(
                    array(&v[8], 3)?
                        .iter()
                        .map(bias_semantic)
                        .collect::<Result<_, _>>()?,
                ),
                _ => unreachable!(),
            };
            fields.push(((*name).into(), value));
        }
        Ok(Json::Object(fields))
    }
}
fn bias_semantic(value: &Value) -> Result<Json, &'static str> {
    let v = array(value, 18)?;
    let mut fields = Vec::new();
    for (i, name) in BIAS.iter().enumerate() {
        let value = match i {
            1 => Json::Bool(boolean(&v[i])?),
            5 | 6 => Json::Object(
                array(&v[i], 3)?
                    .iter()
                    .zip(["Minimum", "Target", "Maximum"])
                    .map(|(n, k)| Ok((k.into(), Json::Decimal(number(n)?))))
                    .collect::<Result<_, &'static str>>()?,
            ),
            7 | 8 => Json::Array(
                v[i].as_array()
                    .ok_or("Invalid strategy wings")?
                    .iter()
                    .map(|n| Ok(Json::Decimal(number(n)?)))
                    .collect::<Result<_, &'static str>>()?,
            ),
            9 | 10 | 13 | 16 => Json::Decimal(number(&v[i])?),
            14 => {
                if v[i].is_nil() {
                    Json::Null
                } else {
                    Json::Decimal(number(&v[i])?)
                }
            }
            _ => Json::Integer(int(&v[i])?),
        };
        fields.push(((*name).into(), value));
    }
    Ok(Json::Object(fields))
}

/// Existing typed ranking metadata. Configuration is detached immutable data.
#[derive(Clone)]
pub struct Ranking {
    /// Complete validated policy used for this prepared chain.
    pub parameters: std::sync::Arc<Parameters>,
    /// Exact configured market direction.
    pub direction: u8,
    /// Authoritative API operational ValueDate.
    pub date: time::Date,
}
impl Ranking {
    /// Resolves a configured enabled direction without inventing a default.
    ///
    /// # Errors
    /// Missing/disabled direction or invalid ValueDate.
    pub fn bias(&self) -> Result<&Bias, &'static str> {
        if !(1..=9999).contains(&self.date.year())
            || self.date.to_julian_day() == 1721426
            || !self.parameters.enabled
        {
            return Err("Invalid strategy ranking intent");
        }
        self.parameters
            .biases
            .iter()
            .find(|b| b.direction == self.direction && b.enabled)
            .ok_or("Strategy bias unavailable")
    }
}
/// Replaces the raw chain with executable native candidate rows and recomputes
/// the complete existing snapshot identity, including typed policy provenance.
///
/// # Errors
/// No candidate coverage, expired evidence, invalid policy or cancellation.
pub fn prepare(
    raw: &super::composition::model::Snapshot,
    ranking: Ranking,
    now: i64,
    stop: &tokio_util::sync::CancellationToken,
    deadline: crate::runtime::Deadline,
) -> Result<super::composition::model::Snapshot, &'static str> {
    let bias = ranking.bias()?;
    let selected =
        super::candidates::select_composition(&raw.instruments, &bias.policy, now, stop, deadline)?;
    if selected.is_empty() {
        return Err("CandidateCoverageUnavailable");
    }
    let mut until = i64::MAX;
    for row in &selected {
        let context = row
            .instrument
            .context
            .as_ref()
            .ok_or("Candidate context unavailable")?;
        until = until.min(context.valid_until());
        let age = i64::from(context.quote_limits().0) * 10000;
        for quote in [&row.instrument.quote, &row.instrument.underlying] {
            let quote = quote.as_ref().ok_or("Candidate quote unavailable")?.quote;
            until = until
                .min(
                    quote
                        .event
                        .checked_add(age)
                        .ok_or("Candidate expiry overflow")?,
                )
                .min(
                    quote
                        .received
                        .checked_add(age)
                        .ok_or("Candidate expiry overflow")?,
                );
        }
    }
    if until <= now {
        return Err("QuoteStale");
    }
    let mut snapshot = super::composition::model::Snapshot {
        schema: raw.schema,
        id: raw.id,
        scope: raw.scope.clone(),
        token: raw.token.clone(),
        horizon: raw.horizon.clone(),
        generation: raw.generation,
        at: raw.at,
        until,
        instruments: selected,
        digest: String::new(),
        ranking: Some(ranking),
    };
    snapshot.seal()?;
    Ok(snapshot)
}

impl PartialEq for Ranking {
    fn eq(&self, other: &Self) -> bool {
        self.direction == other.direction
            && self.date == other.date
            && self.parameters.value == other.parameters.value
    }
}
impl Eq for Ranking {}
/// Copies a bounded command JSON array into the existing typed policy model.
/// Decimal fields stay strings; the configured double retains its wire type.
///
/// # Errors
/// Structural, scalar, depth and business-policy bounds are enforced before use.
pub fn from_command(value: &serde_json::Value) -> Result<Parameters, &'static str> {
    fn copy(
        value: &serde_json::Value,
        depth: usize,
        nodes: &mut usize,
    ) -> Result<Value, &'static str> {
        *nodes += 1;
        if depth > 4 || *nodes > 512 {
            return Err("Strategy policy structural capacity exceeded");
        }
        Ok(match value {
            serde_json::Value::Null => Value::Nil,
            serde_json::Value::Bool(v) => (*v).into(),
            serde_json::Value::String(s) if s.len() <= 4096 => s.clone().into(),
            serde_json::Value::Number(n) => {
                if let Some(v) = n.as_i64() {
                    v.into()
                } else {
                    Value::F64(
                        n.as_f64()
                            .filter(|v| v.is_finite())
                            .ok_or("Invalid strategy number")?,
                    )
                }
            }
            serde_json::Value::Array(a) if a.len() <= 18 => Value::Array(
                a.iter()
                    .map(|v| copy(v, depth + 1, nodes))
                    .collect::<Result<_, _>>()?,
            ),
            _ => return Err("Invalid strategy policy command"),
        })
    }
    let mut raw = copy(value, 0, &mut 0)?;
    let Value::Array(ref mut fields) = raw else {
        return Err("Invalid strategy policy command");
    };
    if fields.len() != 13 {
        return Err("Invalid strategy policy command");
    }
    fields[9] = Value::F64(
        value[9]
            .as_f64()
            .filter(|v| v.is_finite())
            .ok_or("Invalid strategy multiplier")?,
    );
    Parameters::new(raw)
}

/// Existing policy/bias/date/horizon lookup identity, separate from source scope.
///
/// # Errors
/// Missing/disabled direction, invalid value date/horizon or semantic encoding.
pub fn scope_key(
    parameters: &Parameters,
    direction: u8,
    date: time::Date,
    horizon: &str,
) -> Result<String, &'static str> {
    let bias = parameters
        .biases
        .iter()
        .find(|b| b.enabled && b.direction == direction)
        .ok_or("Missing strategy bias")?;
    if date.to_julian_day() == 1721426 || !matches!(horizon, "Daily" | "Weekly" | "Monthly") {
        return Err("Invalid strategy scope identity");
    }
    crate::reference::semantic::compute(&Json::Object(vec![
        (
            "ParameterSetId".into(),
            Json::String(parameters.id().into()),
        ),
        ("Version".into(), Json::Integer(parameters.version())),
        ("ValueDate".into(), Json::String(date.to_string())),
        ("Bias".into(), Json::String(bias.name().into())),
        ("Horizon".into(), Json::String(horizon.into())),
    ]))
}
