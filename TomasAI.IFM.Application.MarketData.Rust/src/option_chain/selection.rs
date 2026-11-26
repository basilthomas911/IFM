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
    pub(crate) fn checkpoint(&self) -> Value {
        use super::composition::checkpoint::quote;
        Value::Array(vec![
            1.into(),
            self.price.to_bits().into(),
            self.delta.to_bits().into(),
            self.volatility.to_bits().into(),
            quote(&self.underlying),
            quote(&self.option),
            self.iv_at.into(),
            self.at.into(),
            self.until.into(),
            self.policy.clone().into(),
            self.digest.clone().into(),
        ])
    }
    pub(crate) fn restore_checkpoint(value: &Value) -> Result<Self, String> {
        use super::composition::checkpoint::read_quote;
        use crate::checkpoint as c;
        let f = c::fields(value, 11)?;
        if c::unsigned(&f[0])? != 1 {
            return Err("Selection checkpoint version invalid".into());
        }
        let result = Self {
            price: f64::from_bits(c::unsigned(&f[1])?),
            delta: f64::from_bits(c::unsigned(&f[2])?),
            volatility: f64::from_bits(c::unsigned(&f[3])?),
            underlying: read_quote(&f[4])?,
            option: read_quote(&f[5])?,
            iv_at: c::number(&f[6])?,
            at: c::number(&f[7])?,
            until: c::number(&f[8])?,
            policy: c::string(&f[9], 1024)?,
            digest: c::string(&f[10], 4096)?,
        };
        if [result.price, result.delta, result.volatility]
            .iter()
            .any(|v| !v.is_finite())
            || result.policy.trim().is_empty()
        {
            return Err("Original selection numeric/policy invalid".into());
        }
        result.value().map_err(str::to_owned)?;
        Ok(result)
    }
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

#[cfg(test)]
mod checkpoint_tests {
    use super::*;
    #[test]
    fn selection_checkpoint_preserves_original_iv_quotes_times_and_signed_zero() {
        let at = 638_000_000_000_000_000;
        let decimal = |v| crate::reference::decimal::parse(v).unwrap();
        let quote = Quote {
            contract: "ESZ2-C-4000".into(),
            quote: super::super::qualification::Quote {
                generation_low: 99,
                generation_high: 12,
                event: at,
                received: at + 7,
                sequence: 987654321,
                bid: decimal("12.25"),
                ask: decimal("12.50"),
                bid_size: decimal("7"),
                ask_size: decimal("9"),
                ..Default::default()
            },
        };
        let selection = Selection {
            price: 12.375,
            delta: -0.0,
            volatility: 0.2225,
            underlying: quote.clone(),
            option: quote,
            iv_at: at - 7,
            at: at + 15,
            until: at + 123456,
            policy: "original-policy-v19".into(),
            digest: "original-digest".into(),
        };
        let checkpoint = selection.checkpoint();
        let restored = Selection::restore_checkpoint(&checkpoint).unwrap();
        assert_eq!(checkpoint, restored.checkpoint());
        assert_eq!(
            crate::messaging::codec::encode(&selection.value().unwrap()).unwrap(),
            crate::messaging::codec::encode(&restored.value().unwrap()).unwrap()
        );
        assert_eq!(restored.delta.to_bits(), (-0.0f64).to_bits());
        let mut corrupted = checkpoint;
        let Value::Array(ref mut fields) = corrupted else {
            panic!()
        };
        fields[2] = f64::NAN.to_bits().into();
        assert!(Selection::restore_checkpoint(&corrupted).is_err());
    }
}
