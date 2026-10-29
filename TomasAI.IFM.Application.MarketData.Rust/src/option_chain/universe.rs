//! Complete definition-scope qualification, matching EuropeanOptionUniverse.
//! Exact mapping lookup happens as native-owned raw I/O before this pure pass;
//! absent mappings never fall back to a different or latest reviewed version.
use super::{convention::Convention, qualification::Decimal};
use crate::{
    messaging::{codec, option_evidence::Failure},
    runtime::Deadline,
};
use rmpv::Value;
use std::{cmp::Ordering, collections::BTreeSet};
use tokio_util::sync::CancellationToken;

/// Copied normalized provider definition plus its exact requested mapping.
#[derive(Clone)]
pub struct Candidate {
    /// Canonical application contract identity.
    pub contract: String,
    /// Requested immutable reviewed version.
    pub mapping_version: String,
    /// Expected original definition digest.
    pub definition_digest: String,
    /// Actual provider dataset.
    pub dataset: String,
    /// Exact physical publisher.
    pub publisher: u16,
    /// Exact physical instrument.
    pub instrument: u32,
    /// Actual raw symbol, separate from instrument-ID subscription input.
    pub raw_symbol: String,
    /// Canonical underlying dependency.
    pub underlying: String,
    /// Raw call=1 / put=2; other source values are invalid.
    pub right: u8,
    /// Exact normalized provider strike.
    pub strike: Decimal,
    /// Original source expiry; MAX, missing and sub-100ns values are invalid.
    pub expiration_ns: Option<u64>,
    /// Exact lookup result, never a latest-version replacement.
    pub mapping: Option<Convention>,
}
/// One eligible reviewed definition, with provider strike and call/put retained.
pub struct Qualified {
    /// Immutable exact reviewed mapping.
    pub convention: Convention,
    /// Original provider strike.
    pub strike: Decimal,
    /// Original provider call/put side.
    pub call: bool,
}
/// Complete qualification outcome; one invalid definition invalidates the scope.
pub struct Outcome {
    /// Canonically ordered accepted definitions.
    pub definitions: Vec<Qualified>,
    /// Supported exclusion reasons, in original canonical ordering.
    pub exclusions: Vec<Failure>,
    /// Explicit complete-scope failure; never partial usable definitions.
    pub failure: Option<Failure>,
}
fn failure(code: &str, input: &str, id: &str, detail: &str) -> Failure {
    Failure {
        code: code.into(),
        input: input.into(),
        contract: id.into(),
        detail: detail.into(),
        retryable: false,
    }
}
fn failed(code: &str, id: &str) -> Outcome {
    Outcome {
        definitions: vec![],
        exclusions: vec![],
        failure: Some(failure(
            code,
            "Definitions",
            id,
            "Complete qualified option scope is unavailable.",
        )),
    }
}
impl Outcome {
    /// Existing typed three-value qualification transfer, with exact convention
    /// and failure DTOs. No provider resource appears in these copied results.
    ///
    /// # Errors
    /// Invalid retained decimals or failure DTOs cannot become usable output.
    pub fn value(&self) -> Result<Value, &'static str> {
        Ok(Value::Array(vec![
            Value::Array(
                self.definitions
                    .iter()
                    .map(|d| {
                        Ok(Value::Array(vec![
                            d.convention.value(),
                            codec::decimal_bits(d.strike)?,
                            d.call.into(),
                        ]))
                    })
                    .collect::<Result<_, &'static str>>()?,
            ),
            Value::Array(
                self.exclusions
                    .iter()
                    .map(Failure::value)
                    .collect::<Result<_, _>>()?,
            ),
            self.failure
                .as_ref()
                .map_or(Ok(Value::Nil), Failure::value)?,
        ]))
    }
}
/// Applies the current complete-scope validation, duplicate detection, UTF-16
/// ordinal ordering, metadata matching and exclusion precedence in native code.
///
/// # Errors
/// Caller cancellation, expired original deadline or malformed internal DTO.
pub fn qualify(
    scope: &[Candidate],
    complete: bool,
    at: i64,
    offset_minutes: i64,
    stop: &CancellationToken,
    deadline: Deadline,
) -> Result<Outcome, &'static str> {
    if !complete {
        return Ok(failed("IncompleteChain", ""));
    }
    if scope.len() > 2048 {
        return Ok(failed("SnapshotLimit", ""));
    }
    let mut contracts = BTreeSet::new();
    let mut instruments = BTreeSet::new();
    if scope.iter().any(|d| {
        !contracts.insert(d.contract.as_str()) || !instruments.insert((d.publisher, d.instrument))
    }) {
        return Ok(failed("ConflictingDefinition", ""));
    }
    let mut ordered: Vec<_> = scope.iter().collect();
    ordered.sort_by(|a, b| a.contract.encode_utf16().cmp(b.contract.encode_utf16()));
    let mut output = Outcome {
        definitions: Vec::with_capacity(scope.len()),
        exclusions: vec![],
        failure: None,
    };
    for candidate in ordered {
        if stop.is_cancelled() {
            return Err("Native definition qualification cancelled");
        }
        if deadline.remaining().is_zero() {
            return Err("Native definition qualification deadline expired");
        }
        let Some(mapping) = candidate.mapping.as_ref() else {
            return Ok(failed("ContractMetadataUnavailable", &candidate.contract));
        };
        let value = mapping.value();
        let fields = value.as_array().ok_or("Malformed reviewed convention")?;
        let matches = mapping.contract() == candidate.contract
            && fields[20].as_str() == Some(candidate.mapping_version.as_str())
            && fields[19].as_str() == Some(candidate.definition_digest.as_str())
            && mapping.dataset() == candidate.dataset
            && mapping.instrument() == candidate.instrument
            && mapping.publisher() == candidate.publisher
            && (fields[0].as_i64() != Some(3)
                || fields[27]
                    .as_str()
                    .and_then(crate::reference::decimal::parse)
                    .is_some_and(|strike| {
                        strike.compare(candidate.strike) == Some(Ordering::Equal)
                    })
                    && fields[28].as_u64()
                        == Some(u64::from(if candidate.right == 1 { 1u8 } else { 2u8 })))
            && mapping.raw_symbol() == candidate.raw_symbol
            && mapping.underlying() == candidate.underlying
            && candidate.strike.compare(Decimal::default()) == Some(Ordering::Greater)
            && matches!(candidate.right, 1 | 2)
            && candidate.expiration_ns.is_some_and(|ns| {
                ns != u64::MAX
                    && ns % 100 == 0
                    && i64::try_from(ns / 100).ok() == Some(mapping.expiration())
            });
        if !matches {
            return Ok(failed("ContractMetadataUnavailable", &candidate.contract));
        }
        match mapping.qualify(at, offset_minutes) {
            Ok(()) => output.definitions.push(Qualified {
                convention: mapping.clone(),
                strike: candidate.strike,
                call: candidate.right == 1,
            }),
            Err(invalid)
                if matches!(invalid.code, "PricingModelUnsupported" | "ExpiredContract") =>
            {
                output.exclusions.push(failure(
                    invalid.code,
                    invalid.input,
                    &candidate.contract,
                    "Contract pricing qualification failed.",
                ))
            }
            Err(invalid) => return Ok(failed(invalid.code, &candidate.contract)),
        }
    }
    Ok(output)
}
