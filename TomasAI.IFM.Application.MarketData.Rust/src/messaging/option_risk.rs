//! Existing cached full-risk mapping and provenance beneath the native owner.
//! This performs no solver, context refresh, provider IO or managed callback.
use super::{
    option_evidence::{Failure, Greeks, Outcome, Quote},
    option_pricing::pass_digest,
};
use crate::option_chain::{
    coalesced::{Calculation, Context as Numeric, Failure as CachedFailure},
    context::Context,
    qualification,
};

fn pending(id: &str, code: &str) -> Outcome {
    Outcome::Failure(Failure {
        code: code.into(),
        input: "OptionPricing".into(),
        contract: id.into(),
        detail: "Qualified calculation is unavailable.".into(),
        retryable: false,
    })
}
fn failed(id: &str, code: &str, input: &str) -> Outcome {
    Outcome::Failure(Failure {
        code: code.into(),
        input: input.into(),
        contract: id.into(),
        detail: "Option pricing inputs do not satisfy the qualified context.".into(),
        retryable: false,
    })
}
/// Copies one coalesced result using its exact retained inputs and API time.
/// Superseded context identity returns None. Newer-source CalculationPending has
/// precedence over age/reference failures, matching the existing cache contract.
///
/// # Errors
/// Malformed internal identity, missing result/failure or lifecycle/numerical
/// resource errors escape to native recovery, rather than yielding zero Greeks.
pub fn read(
    context: &Context,
    numeric: &Numeric,
    calculation: &Calculation,
    current_at: i64,
) -> Result<Option<Outcome>, &'static str> {
    if calculation.context != numeric.identity {
        return Ok(None);
    }
    if calculation.source_generation == 0 || !calculation.time.is_finite() {
        return Err("Malformed native cached source or year fraction");
    }
    let contract = context.convention().value();
    let fields = contract
        .as_array()
        .ok_or("Malformed native risk convention")?;
    let id = context.convention().contract();
    let underlying = Quote {
        contract: fields[9]
            .as_str()
            .ok_or("Missing native underlying identity")?
            .into(),
        quote: calculation.underlying,
    };
    let option = Quote {
        contract: id.into(),
        quote: calculation.option,
    };
    let mut request = numeric.qualification;
    request.at = current_at;
    request.underlying = calculation.underlying;
    request.option = calculation.option;
    if calculation.failure != Some(CachedFailure::CalculationPending)
        && (context.validate_at(current_at).is_err()
            || qualification::validate(&request)
                .ok_or("Malformed cached quote qualification")?
                .failure
                != 0)
    {
        return Ok(Some(pending(id, "StaleData")));
    }
    let failure = match calculation.failure {
        None => None,
        Some(CachedFailure::UnderlyingUnavailable) => {
            Some(pending(id, "UnderlyingQuoteUnavailable"))
        }
        Some(CachedFailure::ContextExpired | CachedFailure::Stale) => {
            Some(pending(id, "StaleData"))
        }
        Some(CachedFailure::CalculationPending) => Some(pending(id, "CalculationPending")),
        Some(CachedFailure::Quote(q)) => {
            let source = if q.input == 0 {
                underlying.contract.as_str()
            } else {
                id
            };
            let (code, input) = match q.failure {
                1 => ("Recovering", "Generation"),
                2 => ("ContractMetadataUnavailable", "Identity/Strike"),
                3 => ("InvalidQuote", source),
                4 => ("StaleData", source),
                5 => ("IncoherentQuotes", "SourceTimeSkew"),
                _ => return Err("Malformed cached financial failure"),
            };
            Some(failed(id, code, input))
        }
        Some(CachedFailure::Numerical(
            crate::pricing::Failure::Interrupted(_) | crate::pricing::Failure::Capacity,
        )) => return Err("Native cached pricing lifecycle or resource failure"),
        Some(CachedFailure::Numerical(error)) => {
            Some(failed(id, "GreeksCalculationFailed", &format!("{error:?}")))
        }
    };
    if let Some(failure) = failure {
        return Ok(Some(failure));
    }
    let values = calculation
        .risk
        .ok_or("Native risk has neither full Greeks nor explicit failure")?;
    let digest = pass_digest(
        context,
        &underlying,
        &option,
        request.strike,
        numeric.model.call,
        calculation.at,
        calculation.time,
    )?;
    let outcome = Outcome::Success(Greeks {
        values,
        time: calculation.time,
        digest,
    });
    outcome.value()?;
    Ok(Some(outcome))
}
