//! Detached option trade evidence in the existing persistence wire contract.
//! Creating evidence does not acknowledge storage or publish an actor event.
//! That separate acknowledgment boundary must never own native dataset resources.

use super::{codec, option_source::Source, wire::guid};
use crate::{
    option_chain::{context::Context, qualification},
    pricing,
    reference::semantic::{self, Value as Json},
};
use rmpv::Value;
use std::sync::Arc;

/// Copied source-time quote and its application contract identity.
#[derive(Clone)]
pub struct Quote {
    /// Application futures or option contract identity.
    pub contract: String,
    /// Exact immutable numerical quote, including original UTC offsets.
    pub quote: qualification::Quote,
}
impl Quote {
    fn generation(&self) -> [u8; 16] {
        let mut result = [0; 16];
        result[..8].copy_from_slice(&self.quote.generation_low.to_le_bytes());
        result[8..].copy_from_slice(&self.quote.generation_high.to_le_bytes());
        result
    }
    /// Existing numeric-key quote DTO, not a resource-owning reader.
    ///
    /// # Errors
    /// Invalid decimals, offsets or timestamps fail before encoding.
    pub fn value(&self) -> Result<Value, &'static str> {
        let q = self.quote;
        if self.contract.len() > 4096 || q.event_offset != 0 || q.received_offset != 0 {
            return Err("Invalid native evidence quote identity or UTC offset");
        }
        Ok(Value::Array(vec![
            self.contract.clone().into(),
            codec::decimal_bits(q.bid)?,
            codec::decimal_bits(q.ask)?,
            codec::decimal_bits(q.bid_size)?,
            codec::decimal_bits(q.ask_size)?,
            codec::utc_offset(q.event)?,
            codec::utc_offset(q.received)?,
            q.sequence.into(),
            guid(self.generation()).into(),
        ]))
    }
    /// Existing typed quote provenance used by pricing-pass semantic hashes.
    ///
    /// # Errors
    /// Non-UTC or invalid scalar input cannot become pricing provenance.
    pub fn semantic(&self) -> Result<Json, &'static str> {
        self.value()?;
        let q = self.quote;
        Ok(Json::Object(vec![
            ("ContractId".into(), Json::String(self.contract.clone())),
            ("Bid".into(), Json::Decimal(q.bid)),
            ("Ask".into(), Json::Decimal(q.ask)),
            ("BidSize".into(), Json::Decimal(q.bid_size)),
            ("AskSize".into(), Json::Decimal(q.ask_size)),
            ("EventAtUtc".into(), semantic::utc(q.event)?),
            ("ReceivedAtUtc".into(), semantic::utc(q.received)?),
            ("Sequence".into(), Json::Integer(q.sequence)),
            ("GenerationId".into(), Json::String(guid(self.generation()))),
        ]))
    }
}
/// Stable failure data, never replaced with zero-valued usable Greeks.
#[derive(Clone)]
pub struct Failure {
    /// Existing domain failure code.
    pub code: String,
    /// Existing input identity.
    pub input: String,
    /// Option contract identity.
    pub contract: String,
    /// Full diagnostic description, bounded before wire encoding.
    pub detail: String,
    /// Existing contract retryable flag; recovery authority remains native.
    pub retryable: bool,
}
impl Failure {
    /// Existing five-key failure DTO, shared by native qualification and pricing.
    ///
    /// # Errors
    /// Oversized retained failure text is rejected before encoding.
    pub fn value(&self) -> Result<Value, &'static str> {
        if [&self.code, &self.input, &self.contract, &self.detail]
            .iter()
            .any(|s| s.len() > 65536)
        {
            return Err("Option evidence failure exceeds capacity");
        }
        Ok(Value::Array(vec![
            self.code.clone().into(),
            self.input.clone().into(),
            self.contract.clone().into(),
            self.detail.clone().into(),
            self.retryable.into(),
        ]))
    }
}
/// One complete finite numerical result and its exact source-context identity.
#[derive(Clone)]
pub struct Greeks {
    /// Native complete, unscaled per-option risk vector.
    pub values: pricing::Values,
    /// API-qualified year fraction used by the calculation.
    pub time: f64,
    /// Existing trade-basis semantic digest, not merely the reference digest.
    pub digest: String,
}
impl Greeks {
    /// Existing finite unscaled OptionPricingValue DTO.
    ///
    /// # Errors
    /// Invalid numbers or excessive digest text fail before publication.
    pub fn value(&self) -> Result<Value, &'static str> {
        let g = self.values;
        let numbers = [
            g.volatility,
            g.delta,
            g.gamma,
            g.theta,
            g.vega,
            g.rho,
            g.price,
            self.time,
        ];
        if numbers.iter().any(|v| !v.is_finite()) || self.digest.len() > 4096 {
            return Err("Successful trade evidence requires finite full Greeks");
        }
        let mut fields: Vec<Value> = numbers.into_iter().map(Value::F64).collect();
        fields.push(self.digest.clone().into());
        Ok(Value::Array(fields))
    }
}
impl Greeks {
    /// Typed valuation semantics in complete composition snapshot digests.
    ///
    /// # Errors
    /// Invalid full Greeks cannot become provenance.
    pub fn semantic(&self) -> Result<Json, &'static str> {
        self.value()?;
        let g = self.values;
        Ok(Json::Object(vec![
            ("ImpliedVolatility".into(), Json::Double(g.volatility)),
            ("Delta".into(), Json::Double(g.delta)),
            ("Gamma".into(), Json::Double(g.gamma)),
            ("Theta".into(), Json::Double(g.theta)),
            ("Vega".into(), Json::Double(g.vega)),
            ("Rho".into(), Json::Double(g.rho)),
            ("TheoreticalPrice".into(), Json::Double(g.price)),
            ("TimeToExpiry".into(), Json::Double(self.time)),
            ("ContextDigest".into(), Json::String(self.digest.clone())),
        ]))
    }
}
/// Exactly one complete result or one explicit failure.
#[derive(Clone)]
pub enum Outcome {
    /// Successful first pricing attempt.
    Success(Greeks),
    /// Failed first pricing attempt retained without usable Greek output.
    Failure(Failure),
}
impl Outcome {
    /// Existing two-key OptionPricingPassResult DTO for native cache queries.
    ///
    /// # Errors
    /// Nonfinite risk or oversized failure data never becomes usable output.
    pub fn value(&self) -> Result<Value, &'static str> {
        match self {
            Self::Success(g) => Ok(Value::Array(vec![g.value()?, Value::Nil])),
            Self::Failure(f) => Ok(Value::Array(vec![Value::Nil, f.value()?])),
        }
    }
}
/// Source and first immutable pricing attempt. All retained fields are copied
/// values; Arc references point only to immutable reference configuration.
#[derive(Clone)]
pub struct Evidence {
    /// Original provider trade.
    pub source: Source,
    /// Source-time reference context, absent if not qualified at ingestion.
    pub context: Option<Arc<Context>>,
    /// Source-time underlying, absent if unavailable at ingestion.
    pub underlying: Option<Quote>,
    /// API-controlled calculation instant, in Unix UTC ticks.
    pub at: i64,
    /// Exactly one pricing result or failure.
    pub outcome: Outcome,
}
impl Evidence {
    /// Validates the current persistence contract and produces its seven keys.
    /// This does not claim that any external writer has retained the evidence.
    ///
    /// # Errors
    /// Rejects missing identity, context disagreement, nonfinite results and
    /// success without references. No partial evidence is emitted.
    pub fn value(&self) -> Result<Value, &'static str> {
        let source = &self.source;
        if [
            (&source.dataset, 64),
            (&source.contract, 128),
            (&source.symbol, 256),
        ]
        .iter()
        .any(|(s, max)| s.trim().is_empty() || s.encode_utf16().count() > *max)
            || source.publisher == 0
            || source.instrument == 0
            || source.date.to_julian_day() == 1_721_426
            || source.generation == [0; 16]
        {
            return Err("An identified option trade source is required");
        }
        let context = self.context.as_ref().map(|c| c.value()).transpose()?;
        if let Some(context) = &context {
            let fields = context.as_array().ok_or("Invalid evidence context")?;
            let contract = fields[0].as_array().ok_or("Invalid evidence convention")?;
            if contract[1].as_str() != Some(&source.contract)
                || contract[2].as_str() != Some(&source.dataset)
                || contract[3].as_u64() != Some(u64::from(source.publisher))
                || contract[4].as_u64() != Some(u64::from(source.instrument))
                || contract[5].as_str() != Some(&source.symbol)
                || fields[4].as_str() != Some(&guid(source.generation))
            {
                return Err("Trade evidence and pricing context identity disagree");
            }
        }
        let (greeks, failure) = match &self.outcome {
            Outcome::Success(g) => {
                if context.is_none() || self.underlying.is_none() {
                    return Err("Successful trade evidence requires exact context and underlying");
                }
                (g.value()?, Value::Nil)
            }
            Outcome::Failure(f) => (Value::Nil, f.value()?),
        };
        Ok(Value::Array(vec![
            source.value()?,
            context.unwrap_or(Value::Nil),
            self.underlying
                .as_ref()
                .map(Quote::value)
                .transpose()?
                .unwrap_or(Value::Nil),
            codec::utc_offset(self.at)?,
            greeks,
            failure,
            "Trade".into(),
        ]))
    }
    /// Bounded detached persistence payload. The existing database contract caps
    /// each record at 128 KiB. A writer outage cannot retain a native lease.
    ///
    /// # Errors
    /// Rejects invalid or oversized evidence before external submission.
    pub fn encode(&self) -> Result<Vec<u8>, &'static str> {
        let bytes = codec::encode(&self.value()?)?;
        if bytes.len() > 131_072 {
            return Err("Option evidence exceeds persistence capacity");
        }
        Ok(bytes)
    }
}
