//! Actual-trade pricing from frozen native references. No current hot-book read,
//! managed pricer, persistence callback or wall-clock lookup enters this pass.
use super::{
    option_evidence::{Evidence, Failure, Greeks, Outcome, Quote},
    option_source::Source,
};
use crate::{
    option_chain::{
        qualification::{self, Decimal},
        trade::Captured,
    },
    pricing::{self, Calculator},
    reference::{
        decimal,
        semantic::{self, Value as Json},
    },
    runtime::Deadline,
};
use tokio_util::sync::CancellationToken;

const INPUT_DETAIL: &str = "Option pricing inputs do not satisfy the qualified context.";
fn failed(source: &Source, code: &str, input: &str, detail: &str) -> Outcome {
    Outcome::Failure(Failure {
        code: code.into(),
        input: input.into(),
        contract: source.contract.clone(),
        detail: detail.into(),
        retryable: false,
    })
}
pub(crate) fn raw_price(price: Decimal) -> Option<i64> {
    let [lo, mid, hi, flags] = price.bits;
    let scale = (flags >> 16) & 255;
    if flags & 0xffff != 0 || flags >> 24 != 0 || scale > 28 {
        return None;
    }
    let mut n = u128::from(lo) | u128::from(mid) << 32 | u128::from(hi) << 64;
    if scale > 9 {
        let factor = 10_u128.pow(scale - 9);
        if !n.is_multiple_of(factor) {
            return None;
        }
        n /= factor;
    } else {
        n = n.checked_mul(10_u128.pow(9 - scale))?;
    }
    i64::try_from(n).ok()
}
fn policy() -> Json {
    Json::Object(vec![
        ("Steps".into(), Json::Integer(801)),
        ("MaximumIterations".into(), Json::Integer(100)),
        ("MaximumVolatility".into(), Json::Double(4.0)),
        ("PriceTolerance".into(), Json::Double(1e-8)),
        ("SpatialSteps".into(), Json::Integer(800)),
        ("MaximumSorIterations".into(), Json::Integer(2000)),
        ("SorTolerance".into(), Json::Double(1e-9)),
    ])
}
fn trade_semantic(source: &Source) -> Result<Json, &'static str> {
    Ok(Json::Object(vec![
        ("ContractId".into(), Json::String(source.contract.clone())),
        (
            "ValueDate".into(),
            Json::String(format!(
                "{:04}-{:02}-{:02}",
                source.date.year(),
                source.date.month() as u8,
                source.date.day()
            )),
        ),
        ("Price".into(), Json::Decimal(source.price)),
        ("Size".into(), Json::Unsigned(u64::from(source.size))),
        ("SourceSequence".into(), Json::Integer(source.sequence)),
        ("EventTimestamp".into(), semantic::utc(source.event / 100)?),
        (
            "ReceiveTimestamp".into(),
            semantic::utc(source.received / 100)?,
        ),
    ]))
}
/// Calculates and freezes the first actual-trade attempt. Returned evidence is
/// copied data, not storage acknowledgment. The caller must retain it through the
/// detached persistence protocol before publishing its actor event.
///
/// # Errors
/// Cancellation, original deadline expiry, malformed internal preparation and
/// resource failure escape to native recovery; financial failures become evidence.
pub async fn calculate(
    source: Source,
    captured: &Captured,
    stop: &CancellationToken,
    deadline: Deadline,
) -> Result<Evidence, &'static str> {
    if stop.is_cancelled() || deadline.remaining().is_zero() {
        return Err("Native trade pricing interrupted");
    }
    let context = captured.prepared.reference.clone();
    let mut evidence = Evidence {
        source,
        context,
        underlying: None,
        at: captured.at,
        outcome: Outcome::Failure(Failure {
            code: String::new(),
            input: String::new(),
            contract: String::new(),
            detail: String::new(),
            retryable: false,
        }),
    };
    let source = &evidence.source;
    let Some(context) = &evidence.context else {
        evidence.outcome = failed(
            source,
            "PricingContextUnavailable",
            "OptionPricing",
            "Qualified calculation is unavailable.",
        );
        return Ok(evidence);
    };
    let dto = context.value()?;
    let c = dto.as_array().ok_or("Invalid native trade context")?;
    let convention = c[0].as_array().ok_or("Invalid native trade convention")?;
    if convention[1].as_str() != Some(&source.contract)
        || convention[2].as_str() != Some(&source.dataset)
        || convention[3].as_u64() != Some(u64::from(source.publisher))
        || convention[4].as_u64() != Some(u64::from(source.instrument))
        || convention[5].as_str() != Some(&source.symbol)
        || c[4].as_str() != Some(&super::wire::guid(source.generation))
    {
        evidence.context = None;
        evidence.outcome = failed(
            source,
            "PricingContextUnavailable",
            "OptionPricing",
            "Qualified calculation is unavailable.",
        );
        return Ok(evidence);
    }
    let Some(underlying) = captured.underlying else {
        evidence.outcome = failed(
            source,
            "UnderlyingQuoteUnavailable",
            "Underlying",
            "Trade retained without a qualified underlying quote.",
        );
        return Ok(evidence);
    };
    let underlying = Quote {
        contract: convention[9]
            .as_str()
            .ok_or("Missing underlying identity")?
            .into(),
        quote: underlying,
    };
    evidence.underlying = Some(underlying.clone());
    if source.size == 0 || source.date.to_julian_day() == 1_721_426 {
        evidence.outcome = failed(
            source,
            "InvalidTrade",
            "Trade",
            "Trade size and value date are required.",
        );
        return Ok(evidence);
    }
    if let Err(f) = context.validate_at(captured.at) {
        evidence.outcome = failed(source, f.code, f.input, INPUT_DETAIL);
        return Ok(evidence);
    }
    let numeric = &captured.prepared.context;
    let quantity = Decimal {
        bits: [source.size, 0, 0, 0],
    };
    let option = Quote {
        contract: source.contract.clone(),
        quote: qualification::Quote {
            generation_low: numeric.qualification.generation_low,
            generation_high: numeric.qualification.generation_high,
            event: source.event / 100,
            received: source.received / 100,
            sequence: source.sequence,
            bid: source.price,
            ask: source.price,
            bid_size: quantity,
            ask_size: quantity,
            ..Default::default()
        },
    };
    let mut request = numeric.qualification;
    request.at = captured.at;
    request.underlying = underlying.quote;
    request.option = option.quote;
    let invalid =
        qualification::validate(&request).ok_or("Malformed native trade qualification")?;
    if invalid.failure != 0 {
        let input_quote = if invalid.input == 0 {
            underlying.contract.as_str()
        } else {
            source.contract.as_str()
        };
        let (code, input) = match invalid.failure {
            1 => ("Recovering", "Generation"),
            2 => ("ContractMetadataUnavailable", "Identity/Strike"),
            3 => ("InvalidQuote", input_quote),
            4 => ("StaleData", input_quote),
            5 => ("IncoherentQuotes", "SourceTimeSkew"),
            _ => return Err("Malformed native trade failure"),
        };
        evidence.outcome = failed(source, code, input, INPUT_DETAIL);
        return Ok(evidence);
    }
    let t = context
        .convention()
        .year_fraction(captured.at)
        .map_err(|_| "Unqualified native trade year fraction")?;
    let mut model = numeric.model;
    model.time = t;
    model.forward = pricing::midpoint::midpoint(
        raw_price(underlying.quote.bid).ok_or("Non-provider trade bid")?,
        raw_price(underlying.quote.ask).ok_or("Non-provider trade ask")?,
    )
    .ok_or("Invalid native trade midpoint")?;
    let mark = decimal::to_double(source.price).ok_or("Invalid native trade mark")?;
    let calculator =
        Calculator::new(Default::default()).map_err(|_| "Invalid native trade policy")?;
    let values = match calculator.implied(model, mark, stop, deadline).await {
        Ok(value) => value,
        Err(pricing::Failure::Interrupted(_) | pricing::Failure::Capacity) => {
            return Err("Native trade calculation interrupted or exhausted");
        }
        Err(error) => {
            evidence.outcome = failed(
                source,
                "GreeksCalculationFailed",
                &format!("{error:?}"),
                INPUT_DETAIL,
            );
            return Ok(evidence);
        }
    };
    let pass = pass_digest(
        context,
        &underlying,
        &option,
        request.strike,
        model.call,
        captured.at,
        t,
    )?;
    let digest = semantic::compute(&Json::Object(vec![
        ("Version".into(), Json::Integer(1)),
        ("Basis".into(), Json::String("Trade".into())),
        ("trade".into(), trade_semantic(source)?),
        ("context".into(), context.semantic()?),
        ("underlying".into(), underlying.semantic()?),
        ("calculatedAt".into(), semantic::utc(captured.at)?),
        ("ContextDigest".into(), Json::String(pass)),
    ]))?;
    evidence.outcome = Outcome::Success(Greeks {
        values,
        time: t,
        digest,
    });
    Ok(evidence)
}

/// Exact existing full-risk pass identity, shared by fresh trade calculations and
/// immutable coalesced quote-cache results. It performs no solver or provider IO.
///
/// # Errors
/// Invalid bounded scalar/provenance values fail before publishing usable risk.
#[allow(clippy::too_many_arguments)]
pub fn pass_digest(
    context: &crate::option_chain::context::Context,
    underlying: &Quote,
    option: &Quote,
    strike: Decimal,
    is_call: bool,
    at: i64,
    t: f64,
) -> Result<String, &'static str> {
    semantic::compute(&Json::Object(vec![
        ("Version".into(), Json::Integer(2)),
        ("context".into(), context.semantic()?),
        ("underlying".into(), underlying.semantic()?),
        ("option".into(), option.semantic()?),
        ("strike".into(), Json::Decimal(strike)),
        ("isCall".into(), Json::Bool(is_call)),
        ("at".into(), semantic::utc(at)?),
        ("t".into(), Json::Double(t)),
        (
            "EngineVersion".into(),
            Json::String(context.engine().into()),
        ),
        ("NumericalPolicy".into(), policy()),
        (
            "PolicyVersion".into(),
            Json::String("PricingPolicy/v2".into()),
        ),
    ]))
}
