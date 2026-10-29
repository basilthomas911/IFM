//! Coherent snapshot qualification beneath the native owner. Inputs are detached
//! observations; no query retains a provider task, lease or connection. Numerical
//! work cooperatively observes the caller's original deadline and cancellation.
use super::model::{Instrument, Snapshot, Valued};
use crate::{
    messaging::{
        option_evidence::{Greeks, Quote},
        option_pricing::pass_digest,
    },
    option_chain::qualification::{self, Decimal},
    pricing::{self, Calculator},
    runtime::{Deadline, clock::Clock},
};
use std::{collections::BTreeSet, sync::Arc};
use tokio_util::sync::CancellationToken;

/// Copied command parameters. UTC instants are Unix ticks; no local wall clock is
/// consulted or inferred from the workstation's trading date.
#[derive(Clone)]
pub struct Request {
    /// Caller snapshot identity.
    pub id: [u8; 16],
    /// Native desired scope.
    pub scope: String,
    /// Required logical generation.
    pub generation: [u8; 16],
    /// Daily, Weekly or Monthly.
    pub horizon: String,
    /// Frozen authoritative valuation instant.
    pub at: i64,
    /// Caller supplied UTC result expiry.
    pub until: i64,
    /// Complete-scope capacity, at most 2,048.
    pub maximum: usize,
    /// Stricter current quote age in milliseconds.
    pub age: u32,
    /// Stricter source skew in milliseconds.
    pub skew: u32,
    /// Options rather than outright futures.
    pub options: bool,
    /// Cached selection tier rather than a new full-risk pass.
    pub selection: bool,
    /// Browsing may represent unavailable options without usable prices.
    pub allow_missing: bool,
    /// Explicit full-risk exceptions within a selection-only universe.
    pub risk: BTreeSet<String>,
}
/// One bounded source page with complete-scope identity and continuation proof.
pub struct Page {
    /// Required native logical generation.
    pub generation: [u8; 16],
    /// Immutable complete-scope configuration identity.
    pub token: String,
    /// Total complete membership, not just this page.
    pub total: usize,
    /// Copied native inputs and provenance.
    pub instruments: Vec<Instrument>,
    /// None only on the complete final page.
    pub continuation: Option<String>,
}
/// Stable existing snapshot failure code and affected input.
#[derive(Debug, PartialEq, Eq)]
pub struct Failure {
    /// Current C# contract failure code.
    pub code: String,
    /// Snapshot or affected contract/input.
    pub input: String,
}
fn fail(code: &str, input: &str) -> Failure {
    Failure {
        code: code.into(),
        input: input.into(),
    }
}
fn interrupted(stop: &CancellationToken, deadline: Deadline) -> Result<(), Failure> {
    if stop.is_cancelled() || deadline.remaining().is_zero() {
        return Err(fail("SnapshotTimeout", "Snapshot"));
    }
    Ok(())
}
fn same(quote: &Quote, generation: [u8; 16]) -> bool {
    quote.quote.generation_low
        == u64::from_le_bytes(generation[..8].try_into().expect("fixed generation"))
        && quote.quote.generation_high
            == u64::from_le_bytes(generation[8..].try_into().expect("fixed generation"))
}
fn prices(quote: &Quote) -> bool {
    use std::cmp::Ordering::{Greater, Less};
    let q = quote.quote;
    q.bid.compare(Decimal::default()) == Some(Greater)
        && matches!(
            q.ask.compare(q.bid),
            Some(Greater | std::cmp::Ordering::Equal)
        )
        && matches!(
            q.bid_size.compare(Decimal::default()),
            Some(Greater | std::cmp::Ordering::Equal)
        )
        && matches!(
            q.ask_size.compare(Decimal::default()),
            Some(Greater | std::cmp::Ordering::Equal)
        )
        && q.ask.compare(q.bid) != Some(Less)
}
fn validation(
    item: &Instrument,
    request: &Request,
    limit: bool,
) -> Result<qualification::Result, Failure> {
    let context = item
        .context
        .as_ref()
        .ok_or_else(|| fail("PricingContextUnavailable", "Snapshot"))?;
    let strike = item
        .strike
        .ok_or_else(|| fail("ContractMetadataUnavailable", "Snapshot"))?;
    let call = item
        .call
        .ok_or_else(|| fail("ContractMetadataUnavailable", "Snapshot"))?;
    let underlying = item
        .underlying
        .as_ref()
        .ok_or_else(|| fail("UnderlyingQuoteUnavailable", "Underlying"))?;
    let option = item
        .quote
        .as_ref()
        .ok_or_else(|| fail("QuoteUnavailable", &item.contract))?;
    context
        .validate_at(request.at)
        .map_err(|error| fail(error.code, error.input))?;
    let mut numeric = context
        .bind(request.at, 1, 1, strike, call)
        .map_err(|_| fail("ContractMetadataUnavailable", "Identity/Strike"))?
        .qualification;
    if limit {
        numeric.maximum_age = numeric.maximum_age.min(request.age);
        numeric.maximum_skew = numeric.maximum_skew.min(request.skew);
    }
    numeric.underlying = underlying.quote;
    numeric.option = option.quote;
    if underlying.contract != context.convention().underlying() {
        numeric.flags &= !1;
    }
    if option.contract != context.convention().contract() {
        numeric.flags &= !2;
    }
    qualification::validate(&numeric).ok_or_else(|| fail("InvalidQuote", &item.contract))
}
fn qualified(item: &Instrument, request: &Request, limit: bool) -> Result<(), Failure> {
    let result = validation(item, request, limit)?;
    let source = if result.input == 0 {
        item.underlying
            .as_ref()
            .map_or("Underlying", |q| q.contract.as_str())
    } else {
        &item.contract
    };
    match result.failure {
        0 => Ok(()),
        1 => Err(fail("Recovering", "Generation")),
        2 => Err(fail("ContractMetadataUnavailable", "Identity/Strike")),
        3 => Err(fail("InvalidQuote", source)),
        4 => Err(fail("StaleData", source)),
        5 => Err(fail("IncoherentQuotes", "SourceTimeSkew")),
        _ => Err(fail("InvalidQuote", source)),
    }
}
async fn risk(
    item: &Instrument,
    request: &Request,
    stop: &CancellationToken,
    deadline: Deadline,
    limit: bool,
) -> Result<Greeks, Failure> {
    qualified(item, request, limit)?;
    let context = item.context.as_ref().expect("qualified context");
    let underlying = item.underlying.as_ref().expect("qualified underlying");
    let option = item.quote.as_ref().expect("qualified option");
    let strike = item.strike.expect("qualified strike");
    let call = item.call.expect("qualified side");
    let mut model = context
        .bind(request.at, 1, 1, strike, call)
        .map_err(|_| fail("ContractMetadataUnavailable", "Identity/Strike"))?
        .model;
    model.time = context
        .convention()
        .year_fraction(request.at)
        .map_err(|_| fail("StaleData", "Snapshot"))?;
    let midpoint = |q: &Quote| -> Result<f64, Failure> {
        let bid = crate::messaging::option_pricing::raw_price(q.quote.bid)
            .ok_or_else(|| fail("InvalidQuote", &q.contract))?;
        let ask = crate::messaging::option_pricing::raw_price(q.quote.ask)
            .ok_or_else(|| fail("InvalidQuote", &q.contract))?;
        pricing::midpoint::midpoint(bid, ask).ok_or_else(|| fail("InvalidQuote", &q.contract))
    };
    model.forward = midpoint(underlying)?;
    let calculator = Calculator::new(Default::default())
        .map_err(|_| fail("GreeksCalculationFailed", "Policy"))?;
    let values = calculator
        .implied(model, midpoint(option)?, stop, deadline)
        .await
        .map_err(|error| match error {
            pricing::Failure::Interrupted(_) => fail("SnapshotTimeout", "Snapshot"),
            other => fail("GreeksCalculationFailed", &format!("{other:?}")),
        })?;
    let digest = pass_digest(
        context, underlying, option, strike, call, request.at, model.time,
    )
    .map_err(|_| fail("ContractMetadataUnavailable", "Snapshot"))?;
    Ok(Greeks {
        values,
        time: model.time,
        digest,
    })
}

/// Validates public request shape before source lookup or numerical work.
///
/// # Errors
/// Invalid identity, limits, timestamps or unsupported horizon are typed failures.
pub fn validate_request(request: &Request, clock: &Clock) -> Result<i64, Failure> {
    let now = clock
        .utc_ticks()
        .map_err(|_| fail("InvalidSnapshotRequest", "Snapshot"))?;
    if request.id == [0; 16]
        || request.generation == [0; 16]
        || request.scope.trim().is_empty()
        || request.scope.encode_utf16().count() > 128
        || !matches!(request.horizon.as_str(), "Daily" | "Weekly" | "Monthly")
        || !(1..=2048).contains(&request.maximum)
        || !(1..=5000).contains(&request.age)
        || request.skew > 2000
        || request.at > now
        || request.until <= now
        || request.risk.len() > 2048
    {
        return Err(fail("InvalidSnapshotRequest", "Snapshot"));
    }
    Ok(now)
}
/// Captures a complete copied scope at one authoritative instant. Page identity,
/// cardinality, quote coherence and result bounds are checked before publication.
/// No ten-second inner recovery timeout or managed waiter is introduced.
///
/// # Errors
/// Returns existing typed snapshot failures; missing browsing prices remain
/// explicitly unavailable and are never represented as zero-valued usable risk.
pub async fn capture(
    request: Request,
    pages: Vec<Page>,
    clock: &Clock,
    stop: &CancellationToken,
    deadline: Deadline,
) -> Result<Snapshot, Failure> {
    let now = validate_request(&request, clock)?;
    if pages.is_empty() || pages.len() > 2048 {
        return Err(fail("IncompleteChain", "Snapshot"));
    }
    let mut values = Vec::new();
    let mut seen = BTreeSet::new();
    let mut cursors = BTreeSet::new();
    let mut token = None;
    let mut total = None;
    let mut prior_continues = true;
    let mut until = request.until;
    let mut skew = request.skew;
    let mut times = Vec::new();
    for page in pages {
        interrupted(stop, deadline)?;
        if !prior_continues {
            return Err(fail("IncompleteChain", "Snapshot"));
        }
        if page.generation != request.generation {
            return Err(fail("Recovering", "Snapshot"));
        }
        if page.token.trim().is_empty()
            || page.token.encode_utf16().count() > 128
            || token.as_ref().is_some_and(|t| t != &page.token)
            || total.is_some_and(|t| t != page.total)
        {
            return Err(fail("IncompleteChain", "Snapshot"));
        }
        if page.total > request.maximum
            || page.instruments.len() > request.maximum
            || values.len() + page.instruments.len() > page.total
        {
            return Err(fail("SnapshotLimit", "Snapshot"));
        }
        let empty = page.instruments.is_empty();
        token = Some(page.token);
        total = Some(page.total);
        for mut item in page.instruments {
            interrupted(stop, deadline)?;
            if !seen.insert(item.contract.clone())
                || item
                    .quote
                    .as_ref()
                    .is_some_and(|q| q.contract != item.contract)
            {
                return Err(fail("ConflictingDefinition", "Snapshot"));
            }
            let browsing = request.allow_missing
                && request.options
                && item.context.is_some()
                && item.strike.is_some()
                && item.call.is_some();
            let Some(quote) = item.quote.as_ref() else {
                if !browsing {
                    return Err(fail("QuoteUnavailable", &item.contract));
                }
                item.selection = None;
                values.push(Valued {
                    instrument: item,
                    valuation: None,
                });
                continue;
            };
            if browsing {
                let context = item.context.as_ref().expect("browsing context");
                let (age, _, lead) = context.quote_limits();
                let reason = if !same(quote, request.generation) {
                    Some("QuoteGenerationMismatch")
                } else if !prices(quote) {
                    Some("InvalidQuote")
                } else if quote.quote.received > request.at
                    || i128::from(quote.quote.event)
                        > i128::from(request.at) + i128::from(lead) * 10000
                {
                    Some("QuoteClockLead")
                } else if i128::from(request.at) - i128::from(quote.quote.event)
                    > i128::from(age.min(request.age)) * 10000
                {
                    Some("QuoteAgeExceeded")
                } else {
                    None
                };
                if let Some(reason) = reason {
                    item.last_event = Some(quote.quote.event);
                    item.last_received = Some(quote.quote.received);
                    item.unavailable = Some(reason.into());
                    item.quote = None;
                    item.selection = None;
                    values.push(Valued {
                        instrument: item,
                        valuation: None,
                    });
                    continue;
                }
                let valuation = if request.selection && !request.risk.contains(&item.contract) {
                    let valid = qualified(&item, &request, true).is_ok()
                        && item.selection.as_ref().is_some_and(|s| {
                            s.qualifies(context, request.generation, request.at)
                                .unwrap_or(false)
                        });
                    if !valid {
                        item.selection = None;
                    }
                    None
                } else {
                    let value = match risk(&item, &request, stop, deadline, false).await {
                        Ok(value) => Some(value),
                        Err(error) if error.code == "SnapshotTimeout" => return Err(error),
                        Err(_) => None,
                    };
                    item.selection = None;
                    value
                };
                values.push(Valued {
                    instrument: item,
                    valuation,
                });
                continue;
            }
            let mut age = request.age;
            let valuation = if let Some(context) = &item.context {
                if !request.options
                    || item.strike.is_none()
                    || item.call.is_none()
                    || item.underlying.is_none()
                {
                    return Err(fail("ContractMetadataUnavailable", "Snapshot"));
                }
                let limited = Arc::new(
                    context
                        .limited(request.age, request.skew)
                        .map_err(|_| fail("InvalidSnapshotRequest", "Snapshot"))?,
                );
                let limits = limited.quote_limits();
                age = limits.0;
                skew = skew.min(limits.1);
                qualified(&item, &request, true)?;
                until = until.min(limited.valid_until());
                if request.selection {
                    let selection = item
                        .selection
                        .as_ref()
                        .filter(|s| {
                            s.qualifies(context, request.generation, request.at)
                                .unwrap_or(false)
                        })
                        .ok_or_else(|| fail("SelectionCalculationUnavailable", "Snapshot"))?;
                    until = until.min(selection.valid_until());
                    None
                } else {
                    item.context = Some(limited);
                    let value = risk(&item, &request, stop, deadline, true).await?;
                    item.selection = None;
                    Some(value)
                }
            } else {
                if request.options || item.strike.is_some() || item.call.is_some() {
                    return Err(fail("PricingContextUnavailable", "Snapshot"));
                }
                let future = item
                    .future
                    .as_ref()
                    .ok_or_else(|| fail("ContractMetadataUnavailable", "Snapshot"))?;
                if future.contract != item.contract
                    || future.root != "ES"
                    || future.dataset != "GLBX.MDP3"
                    || future.currency != "USD"
                    || future.exchange.trim().is_empty()
                    || future.last_trading <= request.at
                    || future.digest.len() != 64
                    || future.multiplier.compare(Decimal::default())
                        != Some(std::cmp::Ordering::Greater)
                    || future.tick.compare(Decimal::default()) != Some(std::cmp::Ordering::Greater)
                {
                    return Err(fail("ContractMetadataUnavailable", "Snapshot"));
                }
                until = until.min(future.last_trading);
                None
            };
            for quote in item.quote.iter().chain(item.underlying.iter()) {
                if !same(quote, request.generation) {
                    return Err(fail("Recovering", "Snapshot"));
                }
                let lead = item.context.as_ref().map_or(0, |c| c.quote_limits().2);
                if !prices(quote)
                    || quote.quote.event_offset != 0
                    || quote.quote.received_offset != 0
                    || quote.quote.received > request.at
                    || i128::from(quote.quote.event)
                        > i128::from(request.at) + i128::from(lead) * 10000
                {
                    return Err(fail("InvalidQuote", &quote.contract));
                }
                let expiry = quote
                    .quote
                    .event
                    .checked_add(i64::from(age) * 10000)
                    .ok_or_else(|| fail("InvalidQuote", &quote.contract))?;
                if expiry < request.at {
                    return Err(fail("StaleData", &quote.contract));
                }
                until = until.min(expiry);
                times.push(quote.quote.event);
            }
            values.push(Valued {
                instrument: item,
                valuation,
            });
        }
        prior_continues = page.continuation.is_some();
        if let Some(cursor) = page.continuation
            && (cursor.is_empty()
                || cursor.encode_utf16().count() > 2048
                || empty
                || !cursors.insert(cursor))
        {
            return Err(fail("IncompleteChain", "Snapshot"));
        }
        tokio::task::yield_now().await;
    }
    if prior_continues || Some(values.len()) != total {
        return Err(fail("IncompleteChain", "Snapshot"));
    }
    if let (Some(first), Some(last)) = (times.iter().min(), times.iter().max())
        && i128::from(*last) - i128::from(*first) > i128::from(skew) * 10000
    {
        return Err(fail("IncoherentQuotes", "Snapshot"));
    }
    let mut result = Snapshot {
        schema: if request.selection { 2 } else { 1 },
        id: request.id,
        scope: request.scope,
        token: token.expect("validated pages"),
        horizon: request.horizon,
        generation: request.generation,
        at: request.at,
        until,
        instruments: values,
        digest: String::new(),
        ranking: None,
    };
    result
        .seal()
        .map_err(|_| fail("SnapshotLimit", "Snapshot"))?;
    interrupted(stop, deadline)?;
    if clock
        .utc_ticks()
        .map_err(|_| fail("StaleData", "Snapshot"))?
        >= until
        || now >= until
    {
        return Err(fail("StaleData", "Snapshot"));
    }
    Ok(result)
}
