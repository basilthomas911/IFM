//! Complete native pricing-reference preparation. All inputs are immutable copied
//! configuration or native-acquired observations. No managed callback, lease,
//! semaphore or wall-clock lookup participates in qualification or rebuilding.

use super::convention::{Convention, Failure};
use crate::{
    messaging::codec,
    reference::{
        calendar::Calendar,
        semantic::{self, Value as Json},
        treasury,
        treasury_policy::{Conversion, Observation, Publications, Rate},
    },
};
use rmpv::Value;

/// Frozen context owned by one native generation. It can be copied into evidence
/// without retaining any socket, provider task, scope control or managed owner.
#[derive(Clone)]
pub struct Context {
    contract: Convention,
    calendar: Calendar,
    rate: Rate,
    valid_until: i64,
    generation: [u8; 16],
    publication_version: String,
    maximum_lead: i32,
    maximum_age: u32,
    maximum_skew: u32,
    prepared_at: i64,
}

impl std::fmt::Debug for Context {
    fn fmt(&self, out: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        out.debug_struct("Context")
            .field("generation", &self.generation)
            .field("valid_until", &self.valid_until)
            .field("prepared_at", &self.prepared_at)
            .finish_non_exhaustive()
    }
}

fn failure(code: &'static str, input: &'static str) -> Failure {
    Failure { code, input }
}
impl Context {
    /// Frozen qualified annual continuous rate; querying never refreshes it.
    pub fn annual_rate(&self) -> f64 {
        self.rate.annual
    }
    /// Returns a detached context with stricter query qualification limits.
    /// Reference identity remains immutable; the changed limits enter its digest.
    ///
    /// # Errors
    /// Query limits must match the existing quote policy bounds.
    pub fn limited(&self, age: u32, skew: u32) -> Result<Self, &'static str> {
        if !(1..=5000).contains(&age) || skew > 2000 {
            return Err("Invalid composition quote limits");
        }
        let mut result = self.clone();
        result.maximum_age = result.maximum_age.min(age);
        result.maximum_skew = result.maximum_skew.min(skew);
        Ok(result)
    }
    /// Qualified age, skew and source-clock-lead limits in milliseconds.
    pub fn quote_limits(&self) -> (u32, u32, u32) {
        (
            self.maximum_age,
            self.maximum_skew,
            self.maximum_lead as u32,
        )
    }
    /// Frozen logical generation, independent of physical SDK reconnect attempts.
    pub fn generation(&self) -> [u8; 16] {
        self.generation
    }
    /// Immutable reviewed convention retained by this pricing generation.
    pub fn convention(&self) -> &Convention {
        &self.contract
    }
    /// Frozen economic conventions accompanying an enriched option quote. Model
    /// values are never rounded to the premium increment.
    ///
    /// # Errors
    /// Unreviewed premium rules or malformed exact prices fail before publication.
    pub fn pricing_inputs(
        &self,
        bid: super::qualification::Decimal,
        ask: super::qualification::Decimal,
    ) -> Result<Value, &'static str> {
        let contract = self.contract.value();
        let fields = contract
            .as_array()
            .ok_or("Invalid native pricing convention")?;
        Ok(Value::Array(vec![
            fields[8].clone(),
            fields[16].clone(),
            fields[12].clone(),
            (if fields[14].as_i64() == Some(2) {
                360
            } else {
                365
            })
            .into(),
            Value::F64(self.rate.annual),
            (if fields[10].as_i64() == Some(2) {
                "American"
            } else {
                "European"
            })
            .into(),
            (if fields[0].as_i64() == Some(3) && fields[25].as_i64() == Some(2) {
                "FuturesStyle"
            } else {
                "PaidUpfront"
            })
            .into(),
            codec::decimal_bits(self.contract.premium_increment(bid, true)?)?,
            codec::decimal_bits(self.contract.premium_increment(ask, true)?)?,
            self.engine().into(),
        ]))
    }
    /// Prepares the existing OptionPricingContext contract from native references.
    /// The supplied UTC business instant is independent of the monotonic recovery
    /// deadline. Observation retrieval and retry remain owned by the native caller.
    ///
    /// # Errors
    /// Returns stable existing failure codes in the same validation order.
    #[allow(clippy::too_many_arguments)]
    pub fn prepare(
        contract: Convention,
        calendar: Calendar,
        publications: &Publications,
        conversion: &Conversion,
        observation: Option<&Observation>,
        generation: [u8; 16],
        engine: &str,
        at: i64,
        maximum_lead: i32,
    ) -> Result<Self, Failure> {
        contract.qualify(at, 0)?;
        if generation == [0; 16] || engine != contract.engine() {
            return Err(failure("PricingModelUnsupported", "Generation/Pricer"));
        }
        let count = calendar
            .count(contract.calendar_version(), at, contract.expiration())
            .map_err(|_| failure("CalendarCoverageUnavailable", "Calendar"))?;
        let tenor = treasury::select_tenor(count).ok_or(failure(
            if count < 0 {
                "InvalidTradingDayCount"
            } else {
                "TreasuryHorizonUnsupported"
            },
            "Treasury",
        ))?;
        let required = publications
            .required(at)
            .map_err(|e| failure(e, "Treasury"))?;
        let valuation = publications
            .valuation_date(at)
            .map_err(|e| failure(e, "Treasury"))?;
        let observation = observation.ok_or(failure("TreasuryUnavailable", "Treasury"))?;
        if observation.curve.date < required
            || observation.curve.date > valuation
            || observation.curve.retrieved > at
        {
            return Err(failure("TreasuryStale", "Treasury"));
        }
        let rate =
            Rate::convert(observation, tenor, conversion).map_err(|e| failure(e, "Treasury"))?;
        let valid_until = publications.valid_until(at).min(contract.valid_until());
        if valid_until <= at {
            return Err(failure("ExpiredContract", "Validity"));
        }
        if maximum_lead < 0 {
            return Err(failure("PricingModelUnsupported", "SourceClockPolicy"));
        }
        Ok(Self {
            maximum_age: 5000,
            maximum_skew: 2000,
            contract,
            calendar,
            rate,
            valid_until,
            generation,
            publication_version: publications.version().into(),
            maximum_lead,
            prepared_at: at,
        })
    }
    /// API UTC business instant at which native preparation completed.
    pub fn prepared_at(&self) -> i64 {
        self.prepared_at
    }
    /// Exclusive bound for reference use. A later preparation must reselect the
    /// tenor from the exchange calendar rather than reuse an expired template.
    pub fn valid_until(&self) -> i64 {
        self.valid_until
    }
    /// Exact current engine name, including its historical Managed version suffix.
    pub fn engine(&self) -> &'static str {
        self.contract.engine()
    }
    /// Rejects expired references and tenor cutovers even when no publication
    /// boundary occurred. This check belongs before every native pricing pass.
    ///
    /// # Errors
    /// Uncovered calendars or changed tenor require native reference preparation.
    pub fn validate_at(&self, at: i64) -> Result<(), Failure> {
        self.contract.qualify(at, 0)?;
        let utc_date = time::OffsetDateTime::from_unix_timestamp_nanos(i128::from(at) * 100)
            .map_err(|_| failure("TreasuryStale", "ReferenceValidity"))?
            .date();
        if at >= self.valid_until || self.rate.retrieved > at || self.rate.date > utc_date {
            return Err(failure("TreasuryStale", "ReferenceValidity"));
        }
        let count = self
            .calendar
            .count(
                self.contract.calendar_version(),
                at,
                self.contract.expiration(),
            )
            .map_err(|_| failure("CalendarCoverageUnavailable", "Calendar"))?;
        let tenor = treasury::select_tenor(count)
            .ok_or(failure("TreasuryHorizonUnsupported", "TradingDays"))?;
        if tenor != self.rate.tenor {
            return Err(failure("TreasuryContextChanged", "Tenor"));
        }
        Ok(())
    }
    /// Existing numeric-key context DTO, suitable for command/query snapshots.
    ///
    /// # Errors
    /// Invalid metadata cannot be serialized into usable pricing evidence.
    pub fn value(&self) -> Result<Value, &'static str> {
        Ok(Value::Array(vec![
            self.contract.value(),
            self.calendar.value(),
            self.rate.value()?,
            codec::utc_offset(self.valid_until)?,
            guid(self.generation).into(),
            self.engine().into(),
            self.maximum_age.into(),
            self.maximum_skew.into(),
            self.publication_version.clone().into(),
            self.maximum_lead.into(),
        ]))
    }
    /// Existing typed semantic identity, independent of Rust memory addresses.
    ///
    /// # Errors
    /// Rejects malformed imported scalar data and bounded canonical encoding errors.
    pub fn digest(&self) -> Result<String, &'static str> {
        semantic::compute(&self.semantic()?)
    }
    /// Typed reference provenance for native trade evidence. This is copied data,
    /// and cannot retain a provider, renewal timer, lock or managed callback.
    ///
    /// # Errors
    /// Returns malformed scalar data rather than emitting a different identity.
    pub fn semantic(&self) -> Result<Json, &'static str> {
        Ok(Json::Object(vec![
            ("Contract".into(), self.contract.semantic()?),
            ("Calendar".into(), self.calendar.semantic()),
            ("Rate".into(), self.rate.semantic()?),
            ("ValidUntilUtc".into(), semantic::utc(self.valid_until)?),
            ("GenerationId".into(), Json::String(guid(self.generation))),
            ("PricerVersion".into(), Json::String(self.engine().into())),
            (
                "MaximumQuoteAgeMilliseconds".into(),
                Json::Integer(i64::from(self.maximum_age)),
            ),
            (
                "MaximumQuoteSkewMilliseconds".into(),
                Json::Integer(i64::from(self.maximum_skew)),
            ),
            (
                "PublicationPolicyVersion".into(),
                Json::String(self.publication_version.clone()),
            ),
            (
                "MaximumSourceClockLeadMilliseconds".into(),
                Json::Integer(i64::from(self.maximum_lead)),
            ),
        ]))
    }
    /// Existing compressed WorkerOptionDefinition manifest. Financial values and
    /// context identity are native-produced; no opaque managed template is needed.
    ///
    /// # Errors
    /// Invalid strike or encoding capacity never becomes a queued trade manifest.
    pub fn manifest(
        &self,
        strike: super::qualification::Decimal,
        is_call: bool,
    ) -> Result<Vec<u8>, &'static str> {
        self.validate_terms(strike, is_call)?;
        let strike = codec::decimal_bits(strike)?;
        let value = Value::Array(vec![self.value()?, strike, is_call.into()]);
        codec::compress(codec::encode(&value)?)
    }
    fn validate_terms(
        &self,
        strike: super::qualification::Decimal,
        is_call: bool,
    ) -> Result<(), &'static str> {
        if strike.compare(super::qualification::Decimal::default())
            != Some(std::cmp::Ordering::Greater)
        {
            return Err("Invalid option strike");
        }
        let value = self.contract.value();
        let fields = value.as_array().ok_or("Invalid contract DTO")?;
        if fields[0].as_i64() == Some(3) {
            let expected = fields[27]
                .as_str()
                .and_then(crate::reference::decimal::parse)
                .ok_or("Missing definition strike")?;
            if strike.compare(expected) != Some(std::cmp::Ordering::Equal)
                || (fields[28].as_i64() == Some(1)) != is_call
            {
                return Err("Option definition mismatch");
            }
        }
        Ok(())
    }
    /// Builds the hot numerical template directly from the qualified native
    /// reference context. The owner must call `validate_at` before each pass and
    /// replace this template when its reference/tenor qualification changes.
    ///
    /// # Errors
    /// Invalid identity, definition, strike or expired reference fails admission.
    pub fn bind(
        &self,
        at: i64,
        identity: u128,
        underlying: u32,
        strike: super::qualification::Decimal,
        is_call: bool,
    ) -> Result<super::coalesced::Context, &'static str> {
        self.validate_at(at)
            .map_err(|_| "Pricing context requires refresh")?;
        self.validate_terms(strike, is_call)?;
        if identity == 0 || underlying == 0 || self.maximum_lead > 2000 {
            return Err("Invalid native binding identity or clock policy");
        }
        let value = self.contract.value();
        let fields = value.as_array().ok_or("Invalid contract DTO")?;
        let schema3 = fields[0].as_i64() == Some(3);
        let call = fields[28].as_i64() == Some(1);
        let generation_low = u64::from_le_bytes(
            self.generation[..8]
                .try_into()
                .expect("fixed generation word"),
        );
        let generation_high = u64::from_le_bytes(
            self.generation[8..]
                .try_into()
                .expect("fixed generation word"),
        );
        Ok(super::coalesced::Context {
            identity,
            underlying,
            expiration: self.contract.expiration(),
            valid_until: self.valid_until,
            days_in_year: if fields[14].as_i64() == Some(1) {
                365
            } else {
                360
            },
            qualification: super::qualification::Request {
                size: 304,
                flags: 3
                    | if schema3 { 4 } else { 0 }
                    | if is_call { 8 } else { 0 }
                    | if call { 16 } else { 0 },
                generation_low,
                generation_high,
                at,
                maximum_age: self.maximum_age,
                maximum_skew: self.maximum_skew,
                maximum_lead: self.maximum_lead as u32,
                strike,
                definition_strike: fields[27]
                    .as_str()
                    .and_then(crate::reference::decimal::parse)
                    .unwrap_or_default(),
                ..Default::default()
            },
            model: crate::pricing::Request {
                forward: 1.0,
                strike: crate::reference::decimal::to_double(strike).ok_or("Invalid strike")?,
                time: 1.0,
                rate: self.rate.annual,
                call: is_call,
                american: fields[10].as_i64() == Some(2),
                // The existing CLR mapper ignores appended premium-style fields
                // on schemas 1/2, even when those fields happen to be populated.
                futures_style: schema3 && fields[25].as_i64() == Some(2),
            },
        })
    }
}

/// .NET Guid formatter uses little-endian first three words in native ABI bytes.
fn guid(bytes: [u8; 16]) -> String {
    format!(
        "{:08x}-{:04x}-{:04x}-{:02x}{:02x}-{:02x}{:02x}{:02x}{:02x}{:02x}{:02x}",
        u32::from_le_bytes(bytes[..4].try_into().expect("fixed Guid word")),
        u16::from_le_bytes([bytes[4], bytes[5]]),
        u16::from_le_bytes([bytes[6], bytes[7]]),
        bytes[8],
        bytes[9],
        bytes[10],
        bytes[11],
        bytes[12],
        bytes[13],
        bytes[14],
        bytes[15]
    )
}
