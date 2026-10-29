//! Exact current OptionSelectionValue projection. Selection carries no invented
//! gamma/vega/theta/rho; its retained IV keeps the original quote/context inputs.
use super::{coalesced::Calculation, context::Context};
use crate::{
    messaging::{codec, option_evidence::Quote},
    reference::semantic::{self, Value as Json},
};
use rmpv::Value;
/// Immutable selection-tier pricing and original IV provenance.
#[derive(Clone)]
pub struct Selection {
    price: f64,
    delta: f64,
    volatility: f64,
    underlying: Quote,
    option: Quote,
    iv_at: i64,
    at: i64,
    until: i64,
    policy: String,
    digest: String,
}
impl Selection {
    /// Maps a qualified complete selection. Financial/cache unavailability is
    /// None; malformed identities, nonfinite values and overflow are errors.
    /// This does no pricing, reference lookup, resource acquisition or IO.
    ///
    /// # Errors
    /// Invalid cached IV provenance requires native dataset recovery.
    pub fn from_calculation(
        context: &Context,
        calculation: &Calculation,
        policy: &str,
        iv_age_ms: u32,
    ) -> Result<Option<Self>, &'static str> {
        if calculation.failure.is_some() {
            return Ok(None);
        }
        let Some(value) = calculation.selection else {
            return Ok(None);
        };
        let Some(iv) = calculation.iv else {
            return Ok(None);
        };
        if calculation.source_generation == 0
            || iv.context != calculation.context
            || policy.trim().is_empty()
            || policy.len() > 1024
            || iv_age_ms == 0
            || iv_age_ms > 60000
        {
            return Err("Invalid native selection identity or policy");
        }
        if [value.price, value.delta, iv.volatility]
            .iter()
            .any(|n| !n.is_finite())
        {
            return Err("Nonfinite native selection value");
        }
        let until = [
            context.valid_until(),
            calculation
                .option
                .event
                .checked_add(50_000_000)
                .ok_or("Selection quote validity overflow")?,
            calculation
                .underlying
                .event
                .checked_add(50_000_000)
                .ok_or("Selection underlying validity overflow")?,
            iv.option
                .event
                .checked_add(i64::from(iv_age_ms) * 10_000)
                .ok_or("Selection IV validity overflow")?,
        ]
        .into_iter()
        .min()
        .ok_or("Empty selection validity")?;
        let convention = context.convention();
        let result = Self {
            price: value.price,
            delta: value.delta,
            volatility: iv.volatility,
            underlying: Quote {
                contract: convention.underlying().into(),
                quote: iv.underlying,
            },
            option: Quote {
                contract: convention.contract().into(),
                quote: iv.option,
            },
            iv_at: iv.at,
            at: calculation.at,
            until,
            policy: policy.into(),
            digest: context.digest()?,
        };
        result.value()?;
        Ok(Some(result))
    }
    /// Existing ten-key selection DTO, before outer transport compression.
    ///
    /// # Errors
    /// Malformed timestamps/quotes cannot enter a prepared snapshot.
    pub fn value(&self) -> Result<Value, &'static str> {
        Ok(Value::Array(vec![
            Value::F64(self.price),
            Value::F64(self.delta),
            Value::F64(self.volatility),
            self.underlying.value()?,
            self.option.value()?,
            codec::utc_offset(self.iv_at)?,
            codec::utc_offset(self.at)?,
            codec::utc_offset(self.until)?,
            self.policy.clone().into(),
            self.digest.clone().into(),
        ]))
    }
    /// Copies the current backend's window-estimator IV eligibility. This does
    /// not authorize a trade or extend the immutable selection's validity.
    pub fn browser_observation(&self) -> (f64, i64, i64) {
        (self.volatility, self.at, self.until)
    }
    /// Copies an eligible IV for an existing snapshot instant only.
    pub fn browser_iv(&self, at: i64) -> Option<f64> {
        (self.until > at && self.at <= at).then_some(self.volatility)
    }
    /// Existing typed JSON semantics used by composition snapshot hashing.
    ///
    /// # Errors
    /// Invalid timestamps or quote metadata never become provenance.
    pub fn semantic(&self) -> Result<Json, &'static str> {
        Ok(Json::Object(vec![
            ("Price".into(), Json::Double(self.price)),
            ("Delta".into(), Json::Double(self.delta)),
            ("ImpliedVolatility".into(), Json::Double(self.volatility)),
            ("IvUnderlying".into(), self.underlying.semantic()?),
            ("IvOption".into(), self.option.semantic()?),
            ("IvCalculatedAtUtc".into(), semantic::utc(self.iv_at)?),
            ("CalculatedAtUtc".into(), semantic::utc(self.at)?),
            ("ValidUntilUtc".into(), semantic::utc(self.until)?),
            ("PolicyVersion".into(), Json::String(self.policy.clone())),
            ("ContextDigest".into(), Json::String(self.digest.clone())),
        ]))
    }
    /// Validates retained IV provenance without replacing it with current quotes.
    /// The caller independently validates current source inputs and context age.
    ///
    /// # Errors
    /// An invalid immutable context cannot qualify cached selection values.
    pub fn qualifies(
        &self,
        context: &Context,
        generation: [u8; 16],
        at: i64,
    ) -> Result<bool, &'static str> {
        let same = |q: &Quote| {
            q.quote.generation_low
                == u64::from_le_bytes(generation[..8].try_into().expect("fixed generation"))
                && q.quote.generation_high
                    == u64::from_le_bytes(generation[8..].try_into().expect("fixed generation"))
        };
        Ok(self.digest == context.digest()?
            && [self.price, self.delta, self.volatility]
                .iter()
                .all(|v| v.is_finite())
            && self.at <= at
            && self.until > at
            && same(&self.option)
            && same(&self.underlying))
    }
    /// Earliest original quote/context/IV expiry, in authoritative API UTC ticks.
    pub fn valid_until(&self) -> i64 {
        self.until
    }
}
